"""Dump an MTGA prefab bundle's GameObject hierarchy offline (no game launch).

Prints one line per GameObject, indented by depth: the object name, its active flag,
its component script names, and any TMP/Localize text it carries.

Usage:
    py tools/dump-prefab.py <bundle.mtga> [<bundle.mtga> ...]
    py tools/dump-prefab.py --find Gathering        # list bundles whose scripts match

Bundles live in <MTGA>/MTGA_Data/Downloads/AssetBundle/Bucket_Prefab.<Name>Prefab_0_<hash>.mtga.

Gotchas (see memory tool_unitypy_prefab_dump):
- Bundles carry no Unity version string, so FALLBACK_UNITY_VERSION must be set.
- MonoBehaviour.read() can segfault the interpreter. The script name is resolved from the
  raw bytes instead (PPtr m_Script at offset 16), and read_typetree() is only called on
  text components.
"""
import glob
import os
import struct
import sys

import UnityPy
from UnityPy.classes import PPtr

UnityPy.config.FALLBACK_UNITY_VERSION = "6000.3.14f1"

# Prefab texts carry characters like U+200B that the Windows console code page can't encode.
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ASSET_DIR = r"C:\Program Files\Wizards of the Coast\MTGA\MTGA_Data\Downloads\AssetBundle"
TEXT_SCRIPTS = {"TextMeshProUGUI", "TextMeshPro", "Localize", "TMP_InputField"}


def script_name(obj, cache):
    """Resolve a MonoBehaviour's script class name from its raw m_Script PPtr."""
    key = (id(obj.assets_file), obj.path_id)
    if key in cache:
        return cache[key]
    name = "?"
    try:
        raw = obj.get_raw_data()
        file_id, path_id = struct.unpack_from("<iq", raw, 16)
        pptr = PPtr.__new__(PPtr)
        pptr.m_FileID = file_id
        pptr.m_PathID = path_id
        pptr.assetsfile = obj.assets_file
        name = pptr.read().m_ClassName
    except Exception:
        pass
    cache[key] = name
    return name


def text_of(obj, sname):
    if sname not in TEXT_SCRIPTS:
        return None
    try:
        tree = obj.read_typetree()
    except Exception:
        return None
    for key in ("m_text", "m_Text", "_locKey", "m_LocKey", "locKey"):
        value = tree.get(key)
        if isinstance(value, str) and value.strip():
            return value.strip().replace("\n", " ")[:120]
        if isinstance(value, dict):
            inner = value.get("mTerm") or value.get("Key") or value.get("key")
            if isinstance(inner, str) and inner.strip():
                return "loc:" + inner.strip()
    return None


def dump(path):
    env = UnityPy.load(path)
    cache = {}
    gos = {}
    transforms = {}
    for obj in env.objects:
        if obj.type.name == "GameObject":
            gos[obj.path_id] = obj
        elif obj.type.name in ("Transform", "RectTransform"):
            transforms[obj.path_id] = obj

    go_data = {}
    for pid, obj in gos.items():
        go = obj.read()
        comps = []
        texts = []
        transform_id = None
        for entry in go.m_Components:
            # Newer UnityPy yields ComponentPair (.component); some builds yield the PPtr itself.
            ref = getattr(entry, "component", entry)
            try:
                cobj = ref.deref()
            except Exception:
                continue
            tname = cobj.type.name
            if tname in ("Transform", "RectTransform"):
                transform_id = cobj.path_id
                continue
            if tname == "MonoBehaviour":
                sname = script_name(cobj, cache)
                comps.append(sname)
                text = text_of(cobj, sname)
                if text:
                    texts.append(text)
            elif tname not in ("CanvasRenderer",):
                comps.append(tname)
        go_data[pid] = (go.m_Name, go.m_IsActive, comps, texts, transform_id)

    tr_to_go = {d[4]: pid for pid, d in go_data.items() if d[4] is not None}
    children = {}
    has_parent = set()
    for tid, tobj in transforms.items():
        tr = tobj.read()
        kids = []
        for child in tr.m_Children:
            kids.append(child.path_id)
            has_parent.add(child.path_id)
        children[tid] = kids

    def walk(tid, depth):
        pid = tr_to_go.get(tid)
        if pid is None:
            return
        name, active, comps, texts, _ = go_data[pid]
        line = "  " * depth + name
        if not active:
            line += " [inactive]"
        if comps:
            line += " {" + ", ".join(comps) + "}"
        if texts:
            line += " \"" + " | ".join(texts) + "\""
        print(line)
        for kid in children.get(tid, []):
            walk(kid, depth + 1)

    print("=== " + os.path.basename(path))
    for tid in transforms:
        if tid not in has_parent:
            walk(tid, 0)


def find(pattern):
    pattern = pattern.lower()
    for path in sorted(glob.glob(os.path.join(ASSET_DIR, "Bucket_Prefab.*.mtga"))):
        try:
            env = UnityPy.load(path)
        except Exception:
            continue
        cache = {}
        hits = set()
        for obj in env.objects:
            if obj.type.name == "MonoBehaviour":
                name = script_name(obj, cache)
                if pattern in name.lower():
                    hits.add(name)
        if hits:
            print(os.path.basename(path) + ": " + ", ".join(sorted(hits)))


if __name__ == "__main__":
    if len(sys.argv) >= 3 and sys.argv[1] == "--find":
        find(sys.argv[2])
    else:
        for arg in sys.argv[1:]:
            dump(arg)
