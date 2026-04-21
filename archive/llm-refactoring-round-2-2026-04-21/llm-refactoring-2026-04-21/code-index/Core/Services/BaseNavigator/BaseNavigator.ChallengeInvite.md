# BaseNavigator.ChallengeInvite.cs
Path: src/Core/Services/BaseNavigator/BaseNavigator.ChallengeInvite.cs
Lines: 216

## Top-level comments
- Challenge invite popup discovery: friend tile toggles and already-invited read-only entries.
- Uses reflection to extract Player.PlayerName and _inviteToggle from tile components; detects tile boundaries to avoid duplicate text block discovery.

## public partial class BaseNavigator (line 20)
### Methods
- private void DiscoverChallengeInviteTiles(GameObject popup, HashSet<GameObject> addedObjects, List<Transform> skipTransforms) (line 29) — 2-pass: invited entries then friend tiles
- private static string GetChallengeInvitePlayerName(MonoBehaviour tile) (line 112) — reflection on Player.PlayerName
- private static Toggle GetChallengeInviteToggle(MonoBehaviour tile) (line 128) — reflection on _inviteToggle field
- private static bool IsInsideChallengeInviteTile(Transform child, Transform stopAt) (line 140) — detect tile boundaries
- private static GameObject FindInvitedSectionHeading(GameObject popup, string headingText) (line 163) — find by content match
- private static GameObject GetChallengeInviteRecentDropdown(GameObject popup) (line 181) — return _recentChallengesDropDown
- private static string RefreshChallengeInviteToggleLabel(GameObject obj, string label) (line 200)
