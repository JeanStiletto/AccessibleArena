#!/usr/bin/env python3
"""Accessible Arena speech bridge for Linux.

MTG Arena runs under Wine or Proton on Linux, and Windows code inside Wine cannot open
the Unix socket speech-dispatcher listens on. This bridge listens on a local TCP port and
forwards every connection to speech-dispatcher's Unix socket, so the mod can speak through
the same speech server Orca uses.

The mod starts this script on its own when nothing answers on the port. It can also run
as a systemd user service (see accessible-arena-speech.service), which is the reliable
choice for Steam/Proton, where the game runs inside a container.

Only the Python standard library is used.
"""

import argparse
import os
import selectors
import shutil
import socket
import subprocess
import sys
import threading
import time

DEFAULT_PORT = 6560


def log(message):
    print(f"[aa-speechd-bridge] {message}", file=sys.stderr, flush=True)


def speechd_socket_path():
    """Path of speech-dispatcher's Unix socket, following its own lookup rules."""
    address = os.environ.get("SPEECHD_ADDRESS", "")
    if address.startswith("unix_socket:"):
        path = address.split(":", 1)[1]
        if path:
            return path

    runtime_dir = os.environ.get("XDG_RUNTIME_DIR") or f"/run/user/{os.getuid()}"
    return os.path.join(runtime_dir, "speech-dispatcher", "speechd.sock")


def ensure_speechd_running(path):
    """Starts speech-dispatcher if its socket is missing. Returns True when it is there."""
    if os.path.exists(path):
        return True

    binary = shutil.which("speech-dispatcher")
    if binary is None:
        log("speech-dispatcher is not installed and its socket is missing: " + path)
        return False

    log("speech-dispatcher socket missing, spawning it")
    try:
        subprocess.run([binary, "--spawn"], timeout=10,
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    except Exception as exc:  # noqa: BLE001 - report and carry on
        log(f"could not spawn speech-dispatcher: {exc}")

    for _ in range(50):
        if os.path.exists(path):
            return True
        time.sleep(0.1)
    log("speech-dispatcher did not create its socket: " + path)
    return False


def pump(client, upstream):
    """Copies bytes both ways until either side closes."""
    sel = selectors.DefaultSelector()
    sel.register(client, selectors.EVENT_READ, upstream)
    sel.register(upstream, selectors.EVENT_READ, client)
    try:
        while True:
            for key, _ in sel.select():
                data = key.fileobj.recv(65536)
                if not data:
                    return
                key.data.sendall(data)
    except OSError:
        return
    finally:
        sel.close()
        for s in (client, upstream):
            try:
                s.close()
            except OSError:
                pass


class Bridge:
    def __init__(self, host, port, idle_exit):
        self.host = host
        self.port = port
        self.idle_exit = idle_exit
        self.active = 0
        self.last_activity = time.monotonic()
        self.lock = threading.Lock()

    def handle(self, client):
        path = speechd_socket_path()
        if not ensure_speechd_running(path):
            client.close()
            return

        upstream = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
        try:
            upstream.connect(path)
        except OSError as exc:
            log(f"cannot connect to {path}: {exc}")
            upstream.close()
            client.close()
            return

        with self.lock:
            self.active += 1
        try:
            pump(client, upstream)
        finally:
            with self.lock:
                self.active -= 1
                self.last_activity = time.monotonic()

    def idle_watchdog(self, server):
        while True:
            time.sleep(1)
            with self.lock:
                idle = self.active == 0 and time.monotonic() - self.last_activity > self.idle_exit
            if idle:
                log(f"no client for {self.idle_exit}s, exiting")
                server.close()
                os._exit(0)

    def serve(self):
        server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        try:
            server.bind((self.host, self.port))
        except OSError as exc:
            log(f"cannot listen on {self.host}:{self.port}: {exc}")
            return 1
        server.listen(8)
        log(f"listening on {self.host}:{self.port}, forwarding to {speechd_socket_path()}")

        if self.idle_exit > 0:
            threading.Thread(target=self.idle_watchdog, args=(server,), daemon=True).start()

        while True:
            try:
                client, _ = server.accept()
            except OSError:
                return 0
            with self.lock:
                self.last_activity = time.monotonic()
            threading.Thread(target=self.handle, args=(client,), daemon=True).start()


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--host", default="127.0.0.1",
                        help="address to listen on (default: 127.0.0.1, local only)")
    parser.add_argument("--port", type=int, default=DEFAULT_PORT,
                        help=f"TCP port to listen on (default: {DEFAULT_PORT})")
    parser.add_argument("--idle-exit", type=int, default=0, metavar="SECONDS",
                        help="exit after this many seconds without a client (0 = never)")
    args = parser.parse_args()
    return Bridge(args.host, args.port, args.idle_exit).serve()


if __name__ == "__main__":
    sys.exit(main())
