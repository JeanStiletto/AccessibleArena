# Prism (vendored)

The speech library the mod talks to. Prism reaches NVDA, JAWS, Narrator/OneCore, UIA, ZDSR,
PC-Talker, BoyPC Reader, Sense Reader, ZoomText and SAPI through one C ABI, and it replaced Tolk
in v1.4.6.

- Upstream: <https://github.com/ethindp/prism>, MPL-2.0 (see `LICENSE`)
- Version here: **0.16.5**, plus the local patches described below
- `x64/prism.dll` — the shipped build. This is the file the installer embeds and the one that
  ends up in the MTGA root folder.
- `include/prism.h` — the C ABI these bindings were written against. `src/Core/Speech/PrismInterop.cs`
  must stay in step with it.
- `prism-local-patches.patch` — the local changes carried on top of upstream 0.16.5.

Nothing links against Prism at build time. The mod resolves it at runtime, so a missing
`prism.dll` costs speech, not stability.

## The shipped DLL is patched — do not swap in a stock build

Three local changes ride on top of upstream 0.16.5. They came from the KOTOR accessibility mod,
where they were found by shipping to real users:

1. **`backend_registry.cpp` — SEH-guarded `initialize()` inside `acquire_best` / `create_best`.**
   Several backends load a vendor DLL during initialisation (ZDSR → `ZDSRAPI_x64.dll`, PC-Talker →
   `PCTKUSR.dll`, BoyPC → `byctrl-x64.dll`). When a user has that reader installed at a version
   whose exports do not match, the loader raises a structured exception from inside `initialize()`.
   Unguarded, that takes the game down at startup before a word is spoken — reported by a real
   beta tester with ZDSR. Guarded, the faulting backend is skipped and selection walks on.

   **This one cannot be replicated on our side.** C# has no `__try`/`__except`, and Unity's Mono
   will not survive the fault. The protection only exists if the shipped `prism.dll` carries this
   patch.

2. **ZDSR / PC-Talker / BoyPC client DLLs resolved at runtime** rather than through a `.def` import
   library plus `/delayload`.

3. **`CMAKE_C_STANDARD` pinned to 17** — build-environment only, no ABI change.

## Rebuilding

The patched source tree lives in the KOTOR project at `../kotor/third_party/prism`. Build 64-bit
Release with static CRT (no VC++ redistributable needed by users):

```
cmake -S <prism-source> -B <build-dir> -G Ninja -DCMAKE_BUILD_TYPE=Release ^
      -DPRISM_ENABLE_TESTS=OFF -DPRISM_ENABLE_DEMOS=OFF ^
      -DPRISM_ENABLE_GDEXTENSION=OFF -DPRISM_ENABLE_SHIMS=OFF
cmake --build <build-dir> -j 4
```

Run it from a `vcvars64` shell (VS 2022 Build Tools + CMake + Ninja). Then copy the resulting
`prism.dll` over both `x64/prism.dll` and `installer/AccessibleArenaInstaller/Resources/prism.dll`.

After any upstream update, re-apply `prism-local-patches.patch` first and check that
`seh_safe_initialize` is present in `source/backends/backend_registry.cpp` before shipping the
result. A stock upstream or vcpkg build will look fine on a developer machine and crash on a
user's.

`PRISM_ENABLE_LEGACY_BACKENDS` is left off, matching the configuration KOTOR ships. Turning it on
adds System Access and Window-Eyes.
