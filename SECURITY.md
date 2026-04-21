# Security Policy

## Scope

Accessible Arena is a local accessibility mod for MTG Arena. It:

- Reads the game's in-memory UI state to expose it to a screen reader
- Sends no telemetry, no user data, no logs anywhere
- Makes exactly one outbound network call: a request to the GitHub Releases API to check whether a newer version of the mod is available (can be disabled in the F2 settings menu)

There is no server component, no account system, and no credentials handled by the mod.

## Reporting a vulnerability

If you find a security issue, please report it privately before posting publicly, so it can be fixed before bad actors notice:

- Email: fabian@nordwiesen30.de

Use "AccessibleArena security" in the subject. You can expect a first reply within a few days. I will credit you in the release notes when the fix ships, unless you prefer to stay anonymous.

## Realistic attack surface

The one place where a security researcher might find something worth reporting is the **auto-update flow** (F5 from a menu screen):

1. The mod queries GitHub Releases for the latest published version.
2. If a newer DLL is available, it is downloaded over HTTPS.
3. An elevated batch script copies the new DLL into the MTGA Mods folder and relaunches the game.

If any of those steps could be tricked into downloading or executing a file that isn't the legitimate release, that would be a real issue and I want to hear about it.

## Things that are explicitly out of scope

- The mod cannot send your data anywhere — there is no code that does so.
- The mod cannot bypass MTG Arena's own anti-cheat or account systems — it only reads UI state that the game has already rendered.
- Windows SmartScreen warnings on the unsigned installer/DLL are a code-signing cost issue, not a security vulnerability. Every release publishes SHA256 checksums so you can verify integrity.
