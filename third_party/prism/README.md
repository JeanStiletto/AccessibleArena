# Prism (vendored)

The speech library the mod talks to. Prism reaches NVDA, JAWS, Narrator/OneCore, UIA, ZDSR,
PC-Talker, BoyPC Reader, Sense Reader, ZoomText and SAPI through one C ABI, and it replaced Tolk
in v1.4.6.

- Upstream: <https://github.com/ethindp/prism>, MPL-2.0 (see `LICENSE`)
- Version here: **0.17.3**, plus the local patches described below
- `x64/prism.dll` — the shipped build. This is the file the installer embeds and the one that
  ends up in the MTGA root folder.
- `include/prism.h` — the C ABI these bindings were written against. `src/Core/Speech/PrismInterop.cs`
  must stay in step with it.
- `prism-local-patches.patch` — the local changes carried on top of upstream 0.17.3.

Nothing links against Prism at build time. The mod resolves it at runtime, so a missing
`prism.dll` costs speech, not stability.

## The shipped DLL is patched — do not swap in a stock build

Three local changes ride on top of upstream 0.17.3. Upstream has taken none of them, and checking
again at 0.17.3 confirmed it: `__except` appears nowhere in that tree, and the `.def` files,
`source/delayimp.cpp` and the `/delayload` wiring are all still in place. They came from the KOTOR
accessibility mod, where they were found by shipping to real users:

1. **SEH-guarded `initialize()`.** Several backends reach a vendor DLL during initialisation
   (ZDSR → `ZDSRAPI_x64.dll`, PC-Talker → `PCTKUSR.dll`, BoyPC → `byctrl-x64.dll`). When a user has
   that reader installed at a version whose exports do not match, the loader raises a structured
   exception from inside `initialize()`. Unguarded, that takes the game down at startup before a
   word is spoken — reported by a real beta tester with ZDSR. Guarded, the faulting backend is
   skipped and selection walks on.

   Applied in two places. `source/frozen_registry.cpp` covers `acquire_best` / `create_best` — this
   was `backend_registry.cpp` before 0.17.x moved it. `source/prism.cpp` covers
   `prism_backend_initialize` at the C boundary, which matters because the mod does its own
   priority walk (see `ScreenReaderOutput.AcquireFirstLive`) and so never passes through the
   registry's guarded path.

   **This one cannot be replicated on our side.** C# has no `__try`/`__except`, and Unity's Mono
   will not survive the fault. The protection only exists if the shipped `prism.dll` carries this
   patch.

2. **ZDSR / PC-Talker / BoyPC client DLLs resolved at runtime** rather than through a `.def` import
   library plus `/delayload`. Verify with `dumpbin /dependents x64/prism.dll`: none of
   `ZDSRAPI_x64.dll`, `byctrl-x64.dll` or `PCTKUSR.dll` may appear.

   Two adaptations were needed at 0.17.3, both because runtime resolution turns a missing export
   into a null pointer rather than a link error:
   - ZDSR gained a `Braille` export upstream. It is resolved *optionally*, and `braille()` reports
     `NotImplemented` when it is absent, so an older `ZDSRAPI` keeps speaking rather than failing
     to bind at all. `SUPPORTS_BRAILLE` is advertised only when the export is really there.
   - `BoyCtrlSetAnyKeyStopSpeaking` was renamed `BoyCtrlSetAnyKeyBreak` upstream. No backend method
     calls it, so it is resolved optionally under both names and is not allowed to decide whether
     the library loaded — requiring the wrong spelling would silently disable BoyPC entirely.

3. **`CMAKE_C_STANDARD` pinned to 17** — build-environment only, no ABI change. Upstream sets C23
   in `cmake/PrismOptions.cmake`, which this MSVC/CMake pair rejects outright ("Target prism
   requires the language dialect C23"). Prism's C code only declares `c_std_17` anyway.

## The mod passes a NULL config on purpose

`PrismConfig` is version-specific in a way no single binding survives: one byte in 0.16.5 with an
exact-match version check, eight fields in 0.17.3 with a `>` check. `prism_init` reads nothing at
all from a NULL config and falls back to the global registry, which is what the mod wants, so
`PrismInterop` binds `prism_init(IntPtr)` and never binds `prism_config_init`. Keep it that way
unless the mod actually needs a config field — see the comment on the declaration.

## Rebuilding

The patched source tree lives in the KOTOR project at `../kotor/third_party/prism`. Run
`_build_arena_x64.bat` there: it calls `vcvars64`, puts the VS-bundled CMake and Ninja on PATH, and
configures 64-bit Release with the static CRT (no VC++ redistributable needed by users) and every
`PRISM_ENABLE_*` off. Then copy the resulting `build_arena_x64/prism.dll` over both
`x64/prism.dll` and `installer/AccessibleArenaInstaller/Resources/prism.dll`, and refresh
`include/prism.h` from the same tree.

After any upstream update, re-apply `prism-local-patches.patch` first, then check all four before
shipping — a stock upstream or vcpkg build will look fine on a developer machine and crash on a
user's:

- `seh_safe_initialize` present in `source/frozen_registry.cpp`
- `seh_safe_backend_initialize` present in `source/prism.cpp`
- `dumpbin /dependents` on the built DLL lists no `ZDSRAPI_x64.dll`, `byctrl-x64.dll` or
  `PCTKUSR.dll`, and no `vcruntime140.dll` / `msvcp140.dll`
- `dumpbin /exports` still covers every entry point `PrismInterop.cs` declares

Then run `powershell -NoProfile -File third_party\prism\probe-prism.ps1` against the deployed DLL. It lists
every backend with its `IS_SUPPORTED_AT_RUNTIME`, `SUPPORTS_SPEAK`, `SUPPORTS_BRAILLE` and
`SUPPORTS_OUTPUT` bits and what `initialize()` returns — the fastest way to confirm a new build
behaves before it reaches a user.

`PRISM_ENABLE_LEGACY_BACKENDS` is left off, matching the configuration KOTOR ships. Turning it on
adds System Access and Window-Eyes.
