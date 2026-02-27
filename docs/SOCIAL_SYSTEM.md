# Social System (Friends Panel)

Accessible navigation for MTGA's social/friends panel. Provides hierarchical group navigation with per-friend action sub-navigation via screen reader.

**Trigger:** F4 key (toggle) or NavBar social button
**Close:** Backspace

---

## Navigation Structure

The friends panel uses the element grouping system with overlay filtering. When open, all non-social elements are hidden.

### Groups (Tab / Up/Down at group level)

- **Challenge** - Standalone button to create a challenge
- **Add Friend** - Standalone button to send a friend request
- **Friends** - List of accepted friends (navigable section)
- **Sent Requests** - List of outgoing friend invites (navigable section)
- **Incoming Requests** - List of incoming friend invites (navigable section)
- **Blocked** - List of blocked users (navigable section)
- **Your Profile** - Standalone element showing your full username#number and status

Only sections with entries appear. Empty sections are absent.

### Within a Friend Section

- **Up/Down:** Navigate between friend entries
- **Left/Right:** Cycle available actions for the current friend
- **Enter:** Activate the currently selected action
- **Backspace:** Exit section, return to group level

### Announcements

- Entering a section: "1 of 3. wuternst, Online"
- Cycling actions: "Chat, 1 of 4" / "Challenge, 2 of 4"
- Activating: action is executed (button click or callback invocation)

---

## Available Actions Per Tile Type

### FriendTile (accepted friends)
- **Chat** - Opens chat window (available when friend is online or has chat history)
- **Challenge** - Sends a game challenge (available when challenge is enabled)
- **Unfriend** - Removes friend (always available)
- **Block** - Blocks user (always available)

### InviteOutgoingTile (sent requests)
- **Revoke** - Cancels the outgoing request

### InviteIncomingTile (incoming requests)
- **Accept** - Accepts friend request
- **Decline** - Declines friend request
- **Block** - Blocks the requesting user

### BlockTile (blocked users)
- **Unblock** - Removes the block

---

## Friend Entry Labels

Each friend entry displays: **"name, status"**

- FriendTile: name from `_labelName` (TMP_Text) + status from `_labelStatus` (Localize component)
- InviteOutgoingTile: name from `_labelName` (TMP_Text) + date from `_labelDateSent` (Localize component)

The `Localize` component is not a TMP_Text. To read its displayed text, find the TMP_Text on the same GameObject or its children.

---

## Unity Hierarchy (Runtime)

```
SocialUI_V2_Desktop_16x9(Clone)
  MobileSafeArea
    FriendsWidget_*
      Button_TopBarDismiss
      ChallengeWidget_Base
        Button_AddChallenge
          Backer_Hitbox        ← navigable element (Challenge group)
      Button_AddFriend
        Backer_Hitbox          ← navigable element (Add Friend group)
      StatusButton             ← navigable element (Your Profile group, label: "name#number, status")
      Bucket_Friends_CONTAINER
        SocialEntittiesListItem_0    ← note: double-t typo in game code
          [FriendTile component]
          Backer_Hitbox        ← navigable element (Friends section)
        SocialEntittiesListItem_1
          ...
      Bucket_SentRequests_CONTAINER
        SocialEntittiesListItem_0
          [InviteOutgoingTile component]
          Backer_Hitbox        ← navigable element (Sent Requests section)
      Bucket_IncomingRequests_CONTAINER
        ...
      Bucket_Blocked_CONTAINER
        ...
```

Navigable elements are `Backer_Hitbox` (CustomButton) children. The tile component (`FriendTile`, `InviteOutgoingTile`, etc.) is on the parent `SocialEntittiesListItem_*` GameObject.

---

## Technical Implementation

### Files

- **`ElementGroup.cs`** - 7 enum values: `FriendsPanelChallenge`, `FriendsPanelAddFriend`, `FriendsPanelProfile`, `FriendSectionFriends`, `FriendSectionIncoming`, `FriendSectionOutgoing`, `FriendSectionBlocked`
- **`ElementGroupAssigner.cs`** - `DetermineFriendPanelGroup()` maps elements to groups via parentPath bucket detection + profile button instance ID matching
- **`GroupedNavigator.cs`** - Friend section groups exempt from single-element standalone rule
- **`OverlayDetector.cs`** - `FriendsPanel` overlay when social panel is open
- **`FriendInfoProvider.cs`** - Reads tile data and actions via reflection on Core.dll types; also reads local player profile (StatusButton, FullName, StatusText) from FriendsWidget
- **`GeneralMenuNavigator.cs`** - Friend sub-navigation (Left/Right actions, F4 toggle, panel open/close); discovers StatusButton and overrides label with full username#number
- **`MenuScreenDetector.cs`** - `IsSocialPanelOpen()` detects active friends widget
- **`Strings.cs`** - Localized group names and action labels (15 keys total)

### Element Group Assignment

Elements inside the social panel are assigned groups based on parentPath patterns:

- `Button_AddChallenge` in path → `FriendsPanelChallenge`
- `Button_AddFriend` in path → `FriendsPanelAddFriend`
- StatusButton instance ID match → `FriendsPanelProfile`
- `SocialEntittiesListItem` + `Bucket_Friends` → `FriendSectionFriends`
- `SocialEntittiesListItem` + `Bucket_SentRequests` → `FriendSectionOutgoing`
- `SocialEntittiesListItem` + `Bucket_IncomingRequests` → `FriendSectionIncoming`
- `SocialEntittiesListItem` + `Bucket_Blocked` → `FriendSectionBlocked`

Unmatched social panel elements return `Unknown` (hidden via fallthrough guard).

**Sub-button filtering:** Each social tile (FriendTile, InviteIncomingTile, etc.) contains action sub-buttons (_buttonAccept, _buttonReject, etc.) as standard Unity Buttons. The general element scan (`FindObjectsOfType<Button>`) picks these up alongside the main `Backer_Hitbox` CustomButton. `IsPrimarySocialTileElement()` filters them out: if a `SocialEntittiesListItem` has a `Backer_Hitbox` child, only that element is accepted as navigable. Sub-buttons are excluded because their actions are handled via left/right cycling in `FriendInfoProvider`. For tiles without `Backer_Hitbox` (e.g. BlockTile), any child element is accepted as fallback.

**Important:** `ChallengeWidget_Base` in the friends panel contains the challenge button hierarchy. `IsChallengeContainer()` must NOT match `ChallengeWidget` — that pattern would hijack friends panel elements into the `ChallengeMain` group. The actual challenge screen uses `ChallengeOptions`, `FriendChallengeBladeWidget`, `Popout_Play`, and `UnifiedChallenges`.

### Reflection Details

Social tile types live in **Core.dll** (no namespace), NOT Assembly-CSharp.dll.

**FriendTile fields:**
- `_labelName` (TMP_Text) - friend display name
- `_labelStatus` (Localize) - status text (Online/Offline/Away/Busy)
- `_challengeEnabled` (bool) - whether challenge button is active
- `_buttonRemoveFriend` (Button) - unfriend action
- `_buttonBlockFriend` (Button) - block action
- `_buttonChallengeFriend` (Button) - challenge action
- `Callback_OpenChat` (Action\<SocialEntity\>) - chat callback, needs Friend as parameter
- `Friend` (SocialEntity property) - the friend entity with `IsOnline`, `HasChatHistory`, `DisplayName`

**InviteOutgoingTile fields:**
- `_labelName` (TMP_Text) - invitee display name
- `_labelDateSent` (Localize) - date sent text
- `_buttonCancel` (Button) - revoke/cancel action
- `Callback_Reject` (Action\<Invite\>) - reject callback, needs Invite as parameter
- `Invite` (Invite property) - the invite entity

**InviteIncomingTile fields:**
- `_labelName` (TMP_Text) - requester display name
- `_contextClickButton` (CustomButton) - the main clickable element
- `_buttonAccept` (Button) - accept friend request
- `_buttonReject` (Button) - decline friend request
- `_buttonBlock` (Button) - block the requester
- `Callback_Accept`, `Callback_Reject`, `Callback_Block` (Action\<Invite\>)
- `Invite` (Invite property) - the invite entity

**BlockTile fields:**
- `_labelName` (TMP_Text) - blocked user display name
- `_buttonRemoveBlock` (Button) - unblock action
- `Callback_RemoveBlock` (Action\<Block\>) - unblock callback
- `Block` (Block property) - the block entity with `BlockedPlayer.DisplayName`

**FriendsWidget fields (for local player profile):**
- `StatusButton` (CustomButton) - toggles presence status dropdown; identified by instance ID for `FriendsPanelProfile` group
- `UsernameText` (TMP_Text) - shows `_socialManager.LocalPlayer.DisplayName` (name without #number)
- `StatusText` (TMP_Text) - localized status text (Online/Busy/Offline)
- `_socialManager` (ISocialManager, private) - access to `LocalPlayer` (SocialEntity)

**SocialEntity (MTGA.Social) key properties:**
- `FullName` (string) - complete name with discriminator (e.g. "jean stiletto#89866")
- `DisplayName` (string) - name without #number
- `NameHash` (string) - the "#number" suffix
- `Status` (PresenceStatus) - Online/Busy/Offline/Away

### Virtualized Scroll View

The `FriendsWidget` uses a **virtualized scroll view** for performance:
- Tiles are only instantiated for entries within the visible viewport
- `SectionBlocks.IsOpen = false` by default (collapsed)
- The mod force-creates BlockTile instances via reflection on FriendsWidget
- BlockTile has NO CustomButton/Backer_Hitbox - discovered via fallback tile scan

### Panel Toggle

Open: `SocialUI.ShowSocialEntitiesList()` via reflection
Close: `SocialUI.CloseFriendsWidget()` via reflection
Detection: `GameObject.Find("SocialUI_V2_Desktop_16x9(Clone)")` + active `FriendsWidget_*` child

### Important Notes

- `SocialEntittiesListItem` has a double-t typo in the game code - match both spellings
- Button onClick handlers internally pass the entity parameter to callbacks, so `ClickButton` works for unfriend/block/revoke
- For Chat, the callback must be invoked directly with the `Friend` entity as parameter (not wired to a button)
- `_challengeEnabled` controls challenge availability; `Friend.IsOnline` / `Friend.HasChatHistory` controls chat availability
- The `Localize` component type is from `Wotc.Mtga.Loc` namespace

---

# Challenge Screen (Direct Challenge / Friend Challenge)

Accessible navigation for MTGA's challenge screen. Provides flat navigation of spinners, buttons, and player status.

**Trigger:** Challenge action on a friend tile, or "Challenge" button in social panel
**Close:** Backspace from main level

---

## Navigation Structure

Two-level navigation using the element grouping system:

### Level 1: ChallengeMain (flat list)

All spinners + buttons on the main challenge screen:
- **Mode spinner** (always present) - e.g. "Pioneer-Turnier-Match"
- **Additional spinners** (mode-dependent) - Deck Type, Format, Coin Flip (appear for some modes like "Herausforderungs-Match")
- **Select Deck / Deck display** - `NoDeck` when no deck selected, or the deck name (e.g. "mono blau") when one is selected. Enter on either opens the deck selector.
- **Invite** button (in enemy player card, when no opponent invited)
- **Status** button (`UnifiedChallenge_MainButton`) - shows ready/waiting/invalid deck status, prefixed with local player name

**Hidden:** `MainButton_Leave` is filtered from navigation in `ShouldShowElement` - Backspace handles leaving via `GameObject.Find`.

### Level 2: Deck Selection (folder-based)

Reuses PlayBladeFolders infrastructure:
- Folder toggles (Meine Decks, Starterdecks, Brawl-Beispieldecks)
- Deck entries within folders
- NewDeck and EditDeck buttons (added as extra elements in folder group)

### Invite Popup

Handled by existing Popup overlay detection (PopupBase). Contains text input, dropdown, friend checkboxes.

---

## Key Behaviors

### Spinner Changes
- Left/Right arrows invoke OnNextValue/OnPreviousValue on spinner
- Game auto-opens DeckSelectBlade on spinner change (game behavior, not mod)
- Mod closes DeckSelectBlade via `PlayBladeController.HideDeckSelector()` after spinner change
- This properly closes the blade AND reactivates the challenge display (Leave + Invite buttons)
- Mod preserves position via `RequestChallengeMainEntryAtIndex()`

### Deck Selection Flow
- Enter on Select Deck or deck display -> DeckSelectBlade opens -> folders/decks appear
- Enter on deck in selector -> deck selected -> `CloseDeckSelectBlade()` reactivates display -> auto-return to ChallengeMain
- Backspace from folders -> `CloseDeckSelectBlade()` + return to ChallengeMain

**Deck display vs deck entry:** The deck display in `ContextDisplay` (`NoDeck` or `DeckView_Base`) opens the deck selector when activated. Actual deck entries in the `DeckSelectBlade` select a deck and return to ChallengeMain. `IsDeckSelectionButton()` detects the display (including `DeckView_Base` inside `ContextDisplay`), and `HandleEnter` routes it to `RequestFoldersEntry()`. The post-activation `HandleDeckSelected` check is guarded by `challengeResult == NotHandled` to avoid closing the deck selector that was just opened.

**Important:** When the game processes deck selection, it calls `DeckSelectBlade.Hide()` directly (not `HideDeckSelector()`). This closes the blade but leaves `_unifiedChallengeDisplay` deactivated. `HandleDeckSelected()` must call `CloseDeckSelectBlade()` to reactivate it, otherwise Invite button remains invisible.

### Player Status Announcement
- On entering challenge: "Direkte Herausforderung. Du: PlayerName, Status. Gegner: Not invited/PlayerName"
- Local player name extracted from `UnifiedChallengeDisplay._localPlayerDisplay._playerName` (TMP_Text, stripped of rich text tags)
- Main button label enhanced with player name prefix (e.g. "jean stiletto: Ungültiges Deck")

---

## Resolved Issue: Button Deactivation on DeckSelectBlade

### Problem (Fixed)
When DeckSelectBlade opens (either via Select Deck or auto-opened by spinner change), the game deactivates `MainButton_Leave` and `Invite Button` by setting their parent containers inactive. `FindObjectsOfType<CustomButton>()` only finds active objects, so these buttons disappear from our element list.

### Root Cause (Confirmed via Decompilation)
`PlayBladeController` has two methods that work as a pair:
```
ShowDeckSelector() {
    DeckSelector.Show(...)                              // opens blade
    _unifiedChallengeDisplay.gameObject.SetActive(false) // hides ENTIRE challenge display
}
HideDeckSelector() {
    DeckSelector.Hide()                                  // closes blade
    _unifiedChallengeDisplay.gameObject.SetActive(true)  // restores challenge display
}
```
The `_unifiedChallengeDisplay` GameObject is the parent of `Invite Button` (and `MainButton_Leave`, which is hidden from navigation). When it's deactivated, all children become `!activeInHierarchy` and invisible to `FindObjectsOfType`.

Our mod was calling `DeckSelectBlade.Hide()` directly, which only does the first half. The `_unifiedChallengeDisplay` was never reactivated, so Leave and Invite stayed invisible.

**Spinner change flow in game code:**
1. `OnChallengeTypeChanged` / `OnDeckTypeChanged` calls `RefreshDeckSelector(allowRefresh: true)`
2. `RefreshDeckSelector` calls `_playBlade.ShowDeckSelector(...)` which opens blade AND hides challenge display
3. Sighted users see the deck selector overlay (challenge display hidden behind it)
4. When done, `HideDeckSelector()` is called, which closes blade AND restores challenge display

### Fix (Implemented)
`ChallengeNavigationHelper.CloseDeckSelectBlade()` calls `PlayBladeController.HideDeckSelector()` instead of `DeckSelectBlade.Hide()` directly. This ensures both the blade closure and the challenge display reactivation happen together.

Called in three places:
1. **Spinner changes** - `RescanAfterSpinnerChange()` in GeneralMenuNavigator calls `CloseDeckSelectBlade()` before inline rescan
2. **Backspace from folders** - `HandleBackspace()` in ChallengeNavigationHelper calls `CloseDeckSelectBlade()` when returning from deck selection to ChallengeMain
3. **Deck selection** - `HandleDeckSelected()` in ChallengeNavigationHelper calls `CloseDeckSelectBlade()` to reactivate the display after the game's `DeckSelectBlade.Hide()` left it inactive

Note: `CloseDeckSelectBlade()` calls `HideDeckSelector()` unconditionally (no `IsShowing` guard). This is safe because `Hide()` is a no-op when the blade is already hidden, and the critical part is reactivating `_unifiedChallengeDisplay`.

---

## Unity Hierarchy (Runtime)

```
ContentController - Popout_Play_Desktop_16x9(Clone)
  Popout
    BladeView_CONTAINER
      FriendChallengeBladeWidget
        VerticalLayoutGroup
          ChallengeOptions
            Backer
              Content
                Popout_ModeMasterParameter     <- Spinner (mode)
                [additional spinners per mode]
        ContextDisplay
          NoDeck                               <- Select Deck (no deck chosen)
          DeckBoxParent/DeckView_Base(Clone)/UI <- Deck display (deck chosen, Enter reopens selector)
        UnifiedChallengesCONTAINER
          Menu
            MainButtons
              MainButton_Leave                 <- Leave button (hidden from nav, used by Backspace)
          EnemyCard_Challenges
            No Player
              Invite Button                    <- Invite button
          UnifiedChallenge_MainButton          <- Status/Ready button
```

---

## Reflection Details

### UnifiedChallengeDisplay (no namespace)
- `_localPlayerDisplay` (ChallengePlayerDisplay) - local player info
- `_enemyPlayerDisplay` (ChallengePlayerDisplay) - opponent info

### Wizards.Mtga.PrivateGame.ChallengePlayerDisplay
- `_playerName` (TMP_Text) - player display name (with rich text color tags)
- `_playerStatus` (Localize) - status text component
- `_noPlayer` (GameObject) - shown when no opponent
- `_playerInvited` (GameObject) - shown when opponent invited but not joined
- `PlayerId` (string property) - player identifier

### DeckSelectBlade
- `Show(EventContext, DeckFormat, Action, Boolean)` - opens deck selection, stores onHide callback
- `Hide()` - closes blade, calls `SetDeckBoxSelected(false)`, invokes `_onHideCallback`
- `IsShowing` (property) - blade visibility state
- WARNING: Call `PlayBladeController.HideDeckSelector()` instead of `DeckSelectBlade.Hide()` directly - see Button Deactivation section

### PlayBladeController
- `ShowDeckSelector(EventContext, DeckFormat, Action, bool)` - opens blade + deactivates `_unifiedChallengeDisplay`
- `HideDeckSelector()` - closes blade + reactivates `_unifiedChallengeDisplay`
- `OnSelectDeckClicked(EventContext, DeckFormat, Action)` - toggles deck selector (show/hide)
- `DeckSelector` (public field) - reference to `DeckSelectBlade`
- `PlayBladeVisualState` - Hidden, Events, or Challenge
- `_unifiedChallengeDisplay` (private field) - the `UnifiedChallengeDisplay` component

### UnifiedChallengeBladeWidget (extends PlayBladeWidget)
- Manages spinners: `_challengeTypeSpinner`, `_deckTypeSpinner`, `_bestOfSpinner`, `_startingPlayerSpinner`
- `OnChallengeTypeChanged` / `OnDeckTypeChanged` -> `RefreshDeckSelector(true)` -> opens DeckSelectBlade
- `_settingsAnimator` controls UI layout (Expand, Tournament, Locked states)
- `UpdateButton()` changes main/secondary button text based on `DeckSelector.IsShowing` and challenge state

---

## Implementation Files

- **`ChallengeNavigationHelper.cs`** - Central helper: HandleEnter, HandleBackspace, OnChallengeOpened/Closed, HandleDeckSelected, player status, CloseDeckSelectBlade
- **`ElementGroupAssigner.cs`** - `IsChallengeContainer()` routes elements to ChallengeMain; NewDeck/EditDeck to PlayBladeFolders; InviteFriendPopup to Popup. Note: `MainButton_Leave` cannot be filtered here (returning Unknown from `DetermineOverlayGroup` means "not an overlay", not "hide") - filtered in `ShouldShowElement` instead
- **`GroupedNavigator.cs`** - `_isChallengeContext`, `RequestChallengeMainEntry()`, folder extra elements support
- **`OverlayDetector.cs`** - Returns ChallengeMain overlay when PlayBladeState >= 2; `IsInsideChallengeScreen()` checks
- **`GeneralMenuNavigator.cs`** - Challenge helper integration, spinner rescan, label enhancement, player status in announcements, `ShouldShowElement` filters `MainButton_Leave`
- **`Strings.cs`** + `lang/*.json` - ChallengeYou, ChallengeOpponent, ChallengeNotInvited, ChallengeInvited, GroupChallengeMain

---

# Challenge Screen - Work In Progress

Investigation of missing elements and settings on the challenge screen that the mod does not yet expose. Based on decompilation of `UnifiedChallengeBladeWidget`, `UnifiedChallengeDisplay`, `ChallengePlayerDisplay`, `PVPChallengeData`, spinner types, and related classes.

---

## Currently Navigable (Already Working)

- **Mode spinner** (`_challengeTypeSpinner`) - 7 options: Challenge Match, Tournament Match (Standard/Limited/Historic/Alchemy/Explorer/Timeless)
- **Deck Type spinner** (`_deckTypeSpinner`) - 4 options: 60-Card, Brawl, 40-Card (Limited), 60-Card Alchemy. Hidden in tournament mode.
- **Best Of spinner** (`_bestOfSpinner`) - Best of 1 / Best of 3. Only visible when NOT tournament AND deck type allows Bo3.
- **Starting Player spinner** (`_startingPlayerSpinner`) - Random / Challenger / Opponent. Always visible.
- **Select Deck / Deck display** - Opens deck selector on Enter
- **Invite button** (enemy player card, `_noPlayerInviteButton`) - Opens invite popup
- **Main status button** (`UnifiedChallenge_MainButton`) - Enhanced with player name prefix
- **Player status summary** - Announced once on challenge open via `GetPlayerStatusSummary()`

---

## Missing Elements - Not Yet Implemented

### Priority 1: Challenge Status Text (High Impact)

`_challengeStatusText` (Localize) sits below the main button and shows crucial contextual guidance. It is NOT a button, so the mod never reads it.

**Status text values by state:**
- `"MainNav/Challenges/MainButton/NoInvitesDescription"` - "Invite an opponent" (no invites sent, waiting for opponent)
- `"MainNav/Challenges/MainButtonDescription/Waiting"` - "Waiting for an opponent" (invite sent, pending)
- `"MainNav/Challenges/MainButtonDescription/SelectDeck"` - "Select a deck" / "Create or choose a valid deck"
- `"MainNav/Challenges/MainButtonDescription/Ready"` / `"MainNav/Challenges/MainButtonDescription/Unready"` - "Waiting for all players to select valid deck and ready"
- `"MainNav/Challenges/MainButtonDescription/WaitingForHost"` - "Waiting for Host to start game"
- `"MainNav/Challenges/MainButton/StartingMatch"` - "Starting" (countdown active)
- `"MainNav/Challenges/MainButtonDescription/Cancel"` - "Starting" (can still cancel)

**Approach:** Read `_challengeStatusText` and include it in the main button announcement, or announce it separately when challenge data changes.

### Priority 2: Player Status Change Notifications (High Impact)

The mod reads player status **once** on challenge open. It does NOT re-announce when:
- Opponent joins the challenge
- Opponent readies up / unreadies
- Opponent leaves or gets kicked
- Opponent's deck becomes valid/invalid

**Game mechanism:** `PVPChallengeController.RegisterForChallengeChanges(Action<PVPChallengeData>)` fires on every state change. `UnifiedChallengeBladeWidget` already subscribes to this via `OnChallengeDataChanged`.

**Approach:** Subscribe to challenge data changes (or poll) and announce meaningful transitions (opponent joined, ready state changed, etc.).

### Priority 3: Match Start Countdown Timer (High Impact)

When both players are ready and the owner clicks "Start Match", `ChallengeStatus.Starting` activates:
- `DraftTimer _timer` becomes visible, showing countdown seconds
- `UpdateCountdown()` is polled every 30ms via `System.Timers.Timer`
- `_timer.UpdateTime(totalSeconds, matchLaunchCountdown)` updates the visual
- Audio pulses play (`_timerPulseEvent`, `_timerCriticalPulseEvent`)
- Button changes to "Starting Match" (locked) or "Cancel" (can still cancel)
- `_challengeController.IsChallengeLocked(challengeId)` determines if cancellation is still possible

A blind user has **no indication** a countdown is happening or how much time remains before the match starts.

**Key data:** `challengeData.MatchLaunchDateTime` (target time), `challengeData.MatchLaunchCountdown` (total seconds), `DateTime.Now` for remaining calculation.

**Approach:** Detect `ChallengeStatus.Starting` transition. Announce "Match starting in X seconds". Announce at key intervals (10s, 5s, 3, 2, 1). Announce whether cancellation is still possible.

### Priority 4: Enemy Player Card Action Buttons (Medium Impact)

`ChallengePlayerDisplay` (enemy) has `AdvancedButton` (extends `Button`) controls:
- **`_kickButton`** - Kick opponent. Only visible when local player is challenge owner.
- **`_blockButton`** - Block opponent. Always visible when opponent present.
- **`_addFriendButton`** - Add as friend. Visible when not already friends/invited.
- **`_noPlayerInviteButton`** / **`_invitedPlayerInviteButton`** - Invite buttons on player card area.

These ARE picked up by `FindObjectsOfType<Button>()` since `AdvancedButton extends Button`. However:
- They are icon-only buttons with no readable text labels
- A blind user hears the GameObject name or nothing useful
- Context is unclear (which button does what, which player area)

**Approach:** Either provide proper labels via announcement enhancement (detect button names in enemy player area and map to localized labels), or use an attached-actions pattern like friend tiles (Left/Right to cycle Kick/Block/Add Friend actions on the opponent element).

### Priority 5: Settings Lock Announcement (Medium Impact)

When joining someone else's challenge (`ChallengeOwnerId != LocalPlayerId`), settings are locked:
- `IsChallengeSettingsLocked = true`
- `_settingsAnimator.SetBool(ANIMATOR_LOCKED_HASH, true)` - spinners enter "Locked" visual state
- Spinner arrow buttons may become non-interactable

The mod never announces this. A blind user may try to change settings and not understand why nothing happens.

**Approach:** On challenge open, check `ChallengeOwnerId vs LocalPlayerId`. Announce "Settings controlled by host" or similar when locked. Could also prefix spinner labels with a locked indicator.

### Priority 6: Tournament Mode Parameters (Medium Impact)

When a tournament match type is selected (any of the 6 tournament variants):
- Interactive spinners (_deckTypeSpinner, _bestOfSpinner, _startingPlayerSpinner) are hidden
- `TournamentParameters` container becomes ACTIVE with 4 static text labels:
  - **Format** - Card pool text (e.g., "Zeitlose Karten", "Pioneer-Karten", "Klassische Standardkarten")
  - **BestofX** - Always "Best-of-Three" in tournament modes
  - **Coin** - Always "Münzwurf" (Coin Flip) in tournament modes
  - **Timer** - Shows "Timer an" (Timer On) in all tournament modes

In Challenge Match mode, `TournamentParameters` is INACTIVE and the interactive spinners are shown instead (no Timer element).

**UI structure:** `ChallengeOptions > Content > TournamentParameters > {Format, BestofX, Coin, Timer}` - each child is a `LayoutElement` with a `Text [TextMeshProUGUI, Localize]` child.

**Border element:** `ChallengeOptions > Border` contains the full mode title text (e.g., "Timeless – Turnier-Match", "Herausforderungs-Match").

These are all non-interactive text labels. A blind user selecting a tournament mode has no feedback about what card pool, match format, or timer settings apply.

**Approach:** When TournamentParameters is active, read all 4 child text values and announce them as a summary when the mode spinner changes. Could also expose them as readable elements in the navigation.

### Priority 7: Timer Setting (Medium Impact)

**Confirmed via deep UI dump:** The "Timer" setting exists ONLY in tournament modes as a static text label inside `TournamentParameters`. It is NOT a toggle or interactive control - it always shows "Timer an" (Timer On).

In Challenge Match mode, there is no Timer element at all (TournamentParameters is INACTIVE).

This means:
- Tournament modes always have Timer On (not configurable by players)
- Challenge Match mode has no timer display (timer behavior may be different/absent)
- The Timer text is purely informational, not a user-changeable setting

**Approach:** Include Timer status in the tournament parameters announcement (Priority 6 covers this).

### Priority 8: Fake Option Indicators (Low Impact)

- `_fakeOptionB01` - Static "Best of 1" shown when deck type doesn't allow Bo3 (instead of the spinner)
- `_fakeOption60Card` - Static deck size indicator for tournament modes

These are visual-only GameObjects, not interactive. A blind user doesn't know the forced setting.

**Approach:** When these are active, include their meaning in the announcement (e.g., announce "Best of 1 (fixed)" when `_fakeOptionB01` is active).

### Priority 9: Player Title (Low Impact)

`_playerTitle` (Localize) displays each player's cosmetic title on their player card. Set via `_playerTitle.SetText(value.Cosmetics.titleSelection)`.

### Priority 10: Challenge Owner / Party Leader (Low Impact)

`_partyLeaderCrown` GameObject is activated for the challenge owner's player card. Not announced directly, but partially conveyed by the settings lock state (Priority 5).

---

## Relevant Type Details

### UnifiedChallengeBladeWidget (extends PlayBladeWidget)
- **Spinners:** `_challengeTypeSpinner`, `_bestOfSpinner`, `_startingPlayerSpinner`, `_deckTypeSpinner` (all `Spinner_OptionSelector`)
- **Fake options:** `_fakeOptionB01`, `_fakeOption60Card` (GameObjects)
- **Tournament text:** `TournamentSettingsText` (TMP_Text)
- **Timer:** `_timer` (DraftTimer), `_timerPulseEvent`, `_timerCriticalPulseEvent` (AudioEvents)
- **Status:** `_challengeStatusText` (Localize), `_mainButtonGlow` (GameObject)
- **Lock:** `IsChallengeSettingsLocked` (bool), `_settingsAnimator` with Expand/Tournament/Locked states
- **Key methods:** `OnChallengeDataChanged()`, `UpdateView()`, `UpdateButton()`, `UpdateCountdown()`, `SetSpinnersFromChallenge()`

### ChallengePlayerDisplay (Wizards.Mtga.PrivateGame)
- **Identity:** `_playerName` (TMP_Text), `_playerTitle` (Localize), `_playerStatus` (Localize), `PlayerId` (string property)
- **Visual:** `_avatarImage` (Image), `_companionAnchor`, `_sleeveAnchor`, `_deckboxAnchor`, `_readyUpGlow`
- **Enemy-only:** `_invitedAvatar`, `_playerInvited`, `_noPlayer` (GameObjects for invite states)
- **Action buttons (AdvancedButton):** `_kickButton`, `_blockButton`, `_addFriendButton`, `_noPlayerInviteButton`, `_invitedPlayerInviteButton`
- **Callbacks:** `KickButtonPressed`, `BlockButtonPressed`, `AddFriendButtonPressed`, `InviteButtonPressed` (Action delegates)

### PVPChallengeData (SharedClientCore)
- `ChallengeId` (Guid), `Status` (ChallengeStatus: None/Setup/Starting/Removed)
- `ChallengeOwnerId` (string), `LocalPlayerId` (string)
- `MatchType` (ChallengeMatchTypes), `IsBestOf3` (bool), `StartingPlayer` (WhoPlaysFirst)
- `MatchLaunchCountdown` (int, total seconds), `MatchLaunchDateTime` (DateTime, target time)
- `ChallengePlayers` (Dictionary), `Invites` (Dictionary)
- `LocalPlayer` / `OpponentFullName` / `OpponentPlayerId` (derived properties)

### ChallengePlayer (SharedClientCore)
- `PlayerId`, `FullDisplayName` (string)
- `PlayerStatus` (PlayerStatus: NotReady/Ready)
- `Cosmetics` (ClientVanitySelectionsV3: avatarSelection, petSelection, cardBackSelection, titleSelection)
- `DeckArtId`, `DeckTileId` (uint), `DeckId` (Guid)

### Spinner_OptionSelector
- `_valueLabel` (TextMeshProUGUI) - current value display
- `_buttonNextValue` / `_buttonPreviousValue` (CustomButton) - arrow navigation
- `ValueIndex` (int property) - current selection, wraps around
- `onValueChanged` (SpinnerValueChangeEvent) - fires `(int index, string value)`
- `OnNextValue()` / `OnPreviousValue()` - increment/decrement

### ChallengeSpinnerMatchTypes (Wizards.Mtga.PrivateGame.Challenges)
- `ChallengeMatch` - Label: `"MainNav/PrivateGame/ChallengeMatch"`, IsTournament: false
- `TournamentMatch` - Label: `"MainNav/PrivateGame/TournamentMatch"`, TournamentText: TraditionalStandardCards
- `LimitedTournamentMatch` - TournamentText: TraditionalLimitedCards
- `HistoricTournamentMatch` - TournamentText: HistoricCards
- `AlchemyTournamentMatch` - TournamentText: AlchemyCards
- `ExplorerTournamentMatch` - TournamentText: ExplorerCards
- `TimelessTournamentMatch` - TournamentText: TimelessCards

### ChallengeSpinnerDeckTypes (Wizards.Mtga.PrivateGame.Challenges)
- `Standard` - Label: `"MainNav/PrivateGame/DeckType_60_Card"`, AllowBo3: true
- `Brawl` - Label: `"MainNav/PrivateGame/DeckType_Brawl"`, AllowBo3: false
- `Limited` - Label: `"MainNav/PrivateGame/DeckType_40_Card"`, AllowBo3: true
- `Alchemy` - Label: `"MainNav/PrivateGame/DeckType_60_Card_Alchemy"`, AllowBo3: true
