using System;
using System.Text;

namespace AccessibleArena.Core.Speech
{
    /// <summary>
    /// Pure helpers for the nvda2speechd server (https://github.com/RastislavKish/nvda2speechd),
    /// a Linux program many blind Wine users already run: it takes requests on a local
    /// WebSocket (default <c>ws://127.0.0.1:3457</c>) and speaks them through speech-dispatcher.
    ///
    /// Requests are binary WebSocket messages holding the MessagePack (rmp-serde) encoding of
    /// its Rust enum: <c>SpeakText(text)</c> is a one-entry map <c>{"SpeakText": text}</c>,
    /// <c>CancelSpeech</c> the bare string <c>"CancelSpeech"</c>. Byte layouts were checked
    /// against rmp-serde 1.3, the version nvda2speechd builds with.
    /// </summary>
    internal static class Nvda2SpeechdProtocol
    {
        internal const int DefaultPort = 3457;

        internal static byte[] EncodeSpeak(string text)
        {
            byte[] key = MsgPackString("SpeakText");
            byte[] value = MsgPackString(text ?? string.Empty);
            var result = new byte[1 + key.Length + value.Length];
            result[0] = 0x81; // fixmap, one entry
            Buffer.BlockCopy(key, 0, result, 1, key.Length);
            Buffer.BlockCopy(value, 0, result, 1 + key.Length, value.Length);
            return result;
        }

        internal static byte[] EncodeCancel()
        {
            return MsgPackString("CancelSpeech");
        }

        /// <summary>MessagePack str in the smallest form that fits.</summary>
        internal static byte[] MsgPackString(string text)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(text);
            int n = utf8.Length;
            byte[] header;
            if (n < 32)
                header = new[] { (byte)(0xA0 | n) };
            else if (n < 0x100)
                header = new[] { (byte)0xD9, (byte)n };
            else if (n < 0x10000)
                header = new[] { (byte)0xDA, (byte)(n >> 8), (byte)n };
            else
                header = new[] { (byte)0xDB, (byte)(n >> 24), (byte)(n >> 16), (byte)(n >> 8), (byte)n };

            var result = new byte[header.Length + n];
            Buffer.BlockCopy(header, 0, result, 0, header.Length);
            Buffer.BlockCopy(utf8, 0, result, header.Length, n);
            return result;
        }

        /// <summary>
        /// One final, masked WebSocket frame (RFC 6455 §5.2). Clients must mask every frame;
        /// <paramref name="mask"/> is the 4-byte masking key.
        /// </summary>
        internal static byte[] BuildClientFrame(byte opcode, byte[] payload, byte[] mask)
        {
            if (mask == null || mask.Length != 4)
                throw new ArgumentException("mask must be 4 bytes", nameof(mask));

            payload = payload ?? new byte[0];
            int n = payload.Length;
            int lengthBytes = n < 126 ? 0 : (n < 0x10000 ? 2 : 8);
            var frame = new byte[2 + lengthBytes + 4 + n];

            frame[0] = (byte)(0x80 | (opcode & 0x0F));
            int pos = 2;
            if (lengthBytes == 0)
            {
                frame[1] = (byte)(0x80 | n);
            }
            else if (lengthBytes == 2)
            {
                frame[1] = 0x80 | 126;
                frame[pos++] = (byte)(n >> 8);
                frame[pos++] = (byte)n;
            }
            else
            {
                frame[1] = 0x80 | 127;
                ulong len = (ulong)n;
                for (int shift = 56; shift >= 0; shift -= 8)
                    frame[pos++] = (byte)(len >> shift);
            }

            Buffer.BlockCopy(mask, 0, frame, pos, 4);
            pos += 4;
            for (int i = 0; i < n; i++)
                frame[pos + i] = (byte)(payload[i] ^ mask[i & 3]);
            return frame;
        }

        /// <summary>HTTP upgrade request opening the WebSocket.</summary>
        internal static string BuildHandshake(string host, int port, string path, string base64Key)
        {
            return $"GET {(string.IsNullOrEmpty(path) ? "/" : path)} HTTP/1.1\r\n" +
                   $"Host: {host}:{port}\r\n" +
                   "Upgrade: websocket\r\n" +
                   "Connection: Upgrade\r\n" +
                   $"Sec-WebSocket-Key: {base64Key}\r\n" +
                   "Sec-WebSocket-Version: 13\r\n\r\n";
        }

        /// <summary>
        /// Reads host, port and path from an <c>NVDA2SPEECHD_HOST</c> value such as
        /// <c>ws://localhost:3457</c>, the variable nvda2speechd itself uses.
        /// </summary>
        internal static bool TryParseHost(string value, out string host, out int port, out string path)
        {
            host = "127.0.0.1";
            port = DefaultPort;
            path = "/";

            Uri uri;
            if (string.IsNullOrEmpty(value) || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out uri))
                return false;
            if (!string.Equals(uri.Scheme, "ws", StringComparison.OrdinalIgnoreCase))
                return false;

            host = uri.Host;
            // "ws" is not a known scheme on .NET Framework, so a missing port reads as -1 there.
            if (!uri.IsDefaultPort && uri.Port > 0)
                port = uri.Port;
            path = string.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery;
            return true;
        }
    }
}
