# Prism (vendored)

The speech library the mod talks to. Prism reaches NVDA, JAWS, Narrator/OneCore, UIA, ZDSR,
PC-Talker, BoyPC Reader, Sense Reader, ZoomText and SAPI through one C ABI, and it replaced Tolk
in v1.4.6.

- Upstream: <https://github.com/ethindp/prism>, MPL-2.0 (see `LICENSE`)
- Version here: **0.18.3**, plus the local patches described below
- `x64/prism.dll` — the shipped build. This is the file the installer embeds and the one that
  ends up in the MTGA root folder.
- `include/prism.h`, `include/prism_version.h` — the C ABI these bindings were written against.
  `src/Core/Speech/PrismInterop.cs` must stay in step with it.
- `prism-local-patches.patch` — the local changes carried on top of upstream 0.18.3, as a
  `git format-patch` of the patch commit (see Rebuilding).
- `build-prism.bat` — builds Arena's DLL from the shared Prism checkout.

Nothing links against Prism at build time. The mod resolves it at runtime, so a missing
`prism.dll` costs speech, not stability.

## The shipped DLL is patched — do not swap in a stock build

Two local changes ride on top of upstream 0.18.3. They came from the KOTOR accessibility mod,
where they were found by shipping to real users:

1. **SEH-guarded `initialize()`.** Several backends reach a vendor DLL during initialisation
   (ZDSR → `ZDSRAPI_x64.dll`, PC-Talker → `PCTKUSR.dll`, BoyPC → `byctrl-x64.dll`). When a user has
   that reader installed at a version whose exports do not match, the loader can raise a
   structured exception from inside `initialize()`. Unguarded, that takes the game down at startup
   before a word is spoken — reported by a real beta tester with ZDSR. Guarded, the faulting
   backend reports itself unavailable and selection walks on.

   Since 0.18.x every initialisation goes through `TextToSpeechBackend::initialize_tracked()` or
   `ensure_initialized()` in `source/backend.h`, so the guard sits there once
   (`seh_guarded_initialize`) and covers both the C boundary (`prism_backend_initialize`, which the
   mod's own priority walk in `ScreenReaderOutput.AcquireFirstLive` uses) and the registry's
   `*_best()` walks. Upstream still has no `__except` anywhere.

   **This one cannot be replicated on our side.** C# has no `__try`/`__except`, and Unity's Mono
   will not survive the fault. The protection only exists if the shipped `prism.dll` carries this
   patch.

2. **`CMAKE_C_STANDARD` pinned to 17** — build-environment only, no ABI change. Upstream sets C23
   in `cmake/PrismOptions.cmake`, which this MSVC/CMake pair rejects outright ("Target prism
   requires the language dialect C23"). Prism's C code only declares `c_std_17` anyway.

The former third patch, resolving the ZDSR / PC-Talker / BoyPC client DLLs at runtime instead of
through `/delayload`, is upstream since 0.18.3 (ethindp/prism#146), together with the Wine bridges
below. `dumpbin /dependents` must still list none of those DLLs.

## Linux: Wine bridges

Since 0.18.3 the Windows `prism.dll` contains Orca, Speech Dispatcher and Spiel backends that work
under Wine or Proton by calling a bridge. On Windows they report `IS_SUPPORTED_AT_RUNTIME` false and
cost nothing. Under Wine they need the bridge payload from the upstream `prism-linux-x64.zip`
release asset (`dynamic/release/wine/`), deployed as the upstream `doc/src/wine-bridges.md`
describes: the placeholder `.dll` files next to `prism.dll`, the `.dll.so` files in a Unix folder
named in `WINEDLLPATH`. `linux/install.sh` does that. Bridge payload and `prism.dll` must come from
the same Prism release. See docs/LINUX.md.

## The mod passes a NULL config on purpose

`PrismConfig` is version-specific in a way no single binding survives: one byte in 0.16.5 with an
exact-match version check, eight fields in 0.17.3 with a `>` check. `prism_init` reads nothing at
all from a NULL config and falls back to the global registry, which is what the mod wants, so
`PrismInterop` binds `prism_init(IntPtr)` and never binds `prism_config_init`. Keep it that way
unless the mod actually needs a config field — see the comment on the declaration.

## Rebuilding

The patched source lives in its own checkout, `Dev\prism` next to this repository: a clone of
github.com/ethindp/prism with the local patches as one commit on branch `arena-patched`, on top of
the release tag. KOTOR builds from the same checkout with its own script and settings; each mod
keeps its own copy of the built DLL, so updating one never changes the other.

Run `third_party\prism\build-prism.bat` (optionally with the checkout path as argument). It calls
`vcvars64`, puts the VS-bundled CMake and Ninja on PATH, and configures 64-bit Release with the
static CRT (no VC++ redistributable needed by users) and every optional `PRISM_ENABLE_*` off. Then
copy `<prism>\build_arena_x64\prism.dll` over both `x64/prism.dll` and
`installer/AccessibleArenaInstaller/Resources/prism.dll`, and refresh `include/prism.h` and
`include/prism_version.h` (from `build_arena_x64/generated/include`).

To move to a new upstream release: `git fetch --tags`, then `git rebase --onto <new tag> <old tag>
arena-patched`, rebuild, and export the patch again with `git format-patch -1 --stdout >
prism-local-patches.patch`.

After any upstream update, check all four before shipping — a stock upstream or vcpkg build will
look fine on a developer machine and crash on a user's:

- `seh_guarded_initialize` present in `source/backend.h`, and both `initialize_tracked()` and
  `ensure_initialized()` calling `guarded_initialize()` rather than `initialize()` (upstream may
  move these again; the guard must wrap every path into a backend's `initialize()`)
- `CMAKE_C_STANDARD 17` in `cmake/PrismOptions.cmake`
- `dumpbin /dependents` on the built DLL lists no `ZDSRAPI_x64.dll`, `byctrl-x64.dll` or
  `PCTKUSR.dll`, and no `vcruntime140.dll` / `msvcp140.dll`
- `dumpbin /exports` still covers every entry point `PrismInterop.cs` declares

Then run `powershell -NoProfile -File third_party\prism\probe-prism.ps1` against the deployed DLL. It lists
every backend with its `IS_SUPPORTED_AT_RUNTIME`, `SUPPORTS_SPEAK`, `SUPPORTS_BRAILLE` and
`SUPPORTS_OUTPUT` bits and what `initialize()` returns — the fastest way to confirm a new build
behaves before it reaches a user.

`PRISM_ENABLE_LEGACY_BACKENDS` is left off, matching the configuration KOTOR ships. Turning it on
adds System Access and Window-Eyes.
