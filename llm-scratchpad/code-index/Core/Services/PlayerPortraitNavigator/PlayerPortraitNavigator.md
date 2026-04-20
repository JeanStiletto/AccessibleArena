# PlayerPortraitNavigator.cs (partial)
Path: src/Core/Services/PlayerPortraitNavigator/PlayerPortraitNavigator.cs
Lines: 335

## Top-level comments
- Core partial of PlayerPortraitNavigator. Owns the state machine (NavigationState enum), zone entry/exit, focus management, and top-level input routing. V enters the player info zone; L announces life totals; arrows cycle properties (via Properties partial) or emote (via Emotes partial). Not a BaseNavigator subclass — owned by DuelNavigator.

## public partial class PlayerPortraitNavigator (line 15)

### Nested types
- private enum NavigationState (line 21) — Inactive, PlayerNavigation, EmoteNavigation

### Fields
- private readonly IAnnouncementService _announcer (line 17)
- private bool _isActive (line 18)
- private NavigationState _navigationState (line 22)
- private int _currentPlayerIndex (line 23) — 0 = You, 1 = Opponent
- private int _currentPropertyIndex (line 24)
- private GameObject _previousFocus (line 27)
- private GameObject _playerZoneFocusElement (line 28)

### Methods
- public PlayerPortraitNavigator(IAnnouncementService announcer) (line 30)
- public void Activate() (line 35) — Note: calls DiscoverTimerElements + SubscribeLowTimeWarnings (Timer partial)
- public void Deactivate() (line 43) — Note: clears all per-partial state including timer fields and _emoteButtons
- public bool IsInPlayerInfoZone { get; } (line 61) — expression-bodied: `_navigationState != NavigationState.Inactive`
- public void OnFocusChanged(GameObject newFocus) (line 68) — Note: auto-exits zone when focus moves outside IsPlayerZoneElement
- private bool IsPlayerZoneElement(GameObject obj) (line 84) — Note: walks up 8 parents looking for MatchTimer/PlayerPortrait/AvatarView/PortraitButton/EmoteOptionsPanel/CommunicationOptionsPanel/EmoteView names
- public bool HandleInput() (line 119) — Note: V enters zone, L announces life totals, delegates to HandleEmoteNavigation/HandlePlayerNavigation based on state
- private void EnterPlayerInfoZone() (line 156) — Note: stores previous focus, sets focus to player zone element, announces first property
- public void ExitPlayerInfoZone() (line 185) — Note: restores previous focus
- private GameObject FindPlayerZoneFocusElement() (line 207) — Note: prefers PortraitButton from local AvatarView; falls back to local timer's HoverArea
- private bool HandlePlayerNavigation() (line 234) — Note: Backspace exits; Left/Right switches player; Up/Down skips to next visible property; Enter opens local emote wheel
