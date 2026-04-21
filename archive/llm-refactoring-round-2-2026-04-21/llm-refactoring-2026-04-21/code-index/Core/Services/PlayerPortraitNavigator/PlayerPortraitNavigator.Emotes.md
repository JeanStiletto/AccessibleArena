# PlayerPortraitNavigator.Emotes.cs (partial)
Path: src/Core/Services/PlayerPortraitNavigator/PlayerPortraitNavigator.Emotes.cs
Lines: 371

## Top-level comments
- Emotes partial. Owns emote wheel opening via PortraitButton, discovery of EmoteView children under EmoteOptionsPanel/CommunicationOptionsPanel, navigation within the wheel, and emote selection. Also owns avatar reflection cache (used by both FindPlayerZoneFocusElement in Core and TriggerEmoteMenu here).

## public partial class PlayerPortraitNavigator (line 16)

### Fields
- private List<GameObject> _emoteButtons (line 19)
- private int _currentEmoteIndex (line 20)

### Avatar reflection cache
- private static System.Type _avatarViewType (line 23)
- private static PropertyInfo _isLocalPlayerProp (line 24)
- private static FieldInfo _portraitButtonField (line 25)
- private static bool _avatarReflectionInitialized (line 26)

### Methods
- private bool HandleEmoteNavigation() (line 31) — Note: modal — returns true for ALL keys while emote menu is open; Up/Down navigates, Enter selects, Backspace cancels
- private void OpenEmoteWheel() (line 82) — Note: TriggerEmoteMenu then DiscoverEmoteButtons; switches state to EmoteNavigation if buttons found
- private void CloseEmoteWheel() (line 106) — Note: just resets state; wheel typically closes on its own
- private void DiscoverEmoteButtons() (line 118) — Note: scans GameObjects for EmoteOptionsPanel/CommunicationOptionsPanel; recurses via SearchForEmoteButtons; sorts alphabetically by name
- private void SearchForEmoteButtons(Transform parent, int depth) (line 165) — Note: recursion capped at depth 5; skips NavArrow and Mute Container; adds EmoteView children that have non-empty text
- private string ExtractEmoteNameFromTransform(Transform t) (line 207) — Note: returns first non-empty TMP text
- private void AnnounceCurrentEmote() (line 223)
- private string ExtractEmoteName(GameObject emoteObj) (line 235) — Note: falls back to parsing object name (e.g., "EmoteButton_Hello" → "Hello")
- private void SelectCurrentEmote() (line 261) — Note: UIActivator.SimulatePointerClick; returns state to PlayerNavigation
- private static void InitializeAvatarReflection(System.Type avatarType) (line 290) — Note: caches IsLocalPlayer property and PortraitButton field
- private MonoBehaviour FindAvatarView(bool isLocal) (line 322) — Note: scans for DuelScene_AvatarView MonoBehaviours; also called from Core (FindPlayerZoneFocusElement)
- public void TriggerEmoteMenu(bool opponent = false) (line 343) — Note: simulates pointer click on PortraitButton to open/close the wheel
