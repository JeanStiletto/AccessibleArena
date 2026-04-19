# ElementGroup.cs - Code Index

## File-level Comment
Groups for categorizing UI elements in menu navigation.
Elements are assigned to groups based on their parent hierarchy.

## Enums

### ElementGroup (line 9)
```csharp
public enum ElementGroup
```
Defines all element groups for hierarchical navigation:
- Unknown (line 14) - Unclassified elements, hidden in grouped mode
- Primary (line 20) - Main actions (Submit, Continue, CTA buttons)
- Play (line 26) - Play-related (Play button, Direct Challenge, Rankings, Events)
- Progress (line 32) - Progress-related (Boosters, Mastery, Gems, Gold, Wildcards)
- Objectives (line 38) - Objectives/Quests (daily wins, weekly wins, battle pass)
- Social (line 44) - Social elements (Profile, Achievements, Mail)
- Filters (line 48) - Filter controls (Search, sort, filter toggles)
- Content (line 52) - Main content items (Deck entries, cards, list items)
- Settings (line 58) - Settings controls (Sliders, checkboxes, dropdowns)
- Secondary (line 63) - Secondary actions (Help, info buttons)
- Popup (line 72) - Modal dialog/popup elements
- FriendsPanel (line 77) - Friends panel overlay elements
- PlayBladeTabs (line 82) - Play blade tabs (Events, Find Match, Recent)
- PlayBladeContent (line 87) - Play blade content elements
- PlayBladeFolders (line 92) - Play blade folders container
- ChallengeMain (line 98) - Challenge screen main settings
- SettingsMenu (line 104) - Settings menu elements
- NPE (line 108) - New Player Experience overlay
- DeckBuilderCollection (line 113) - Deck Builder collection cards (PoolHolder)
- DeckBuilderDeckList (line 118) - Deck Builder deck list cards (MainDeck_MetaCardHolder)
- DeckBuilderSideboard (line 123) - Deck Builder sideboard cards
- DeckBuilderInfo (line 128) - Deck Builder info group (virtual elements)
- EventInfo (line 137) - Event page info blocks (virtual elements)
- MailboxList (line 144) - Mailbox mail list (left pane)
- MailboxContent (line 149) - Mailbox mail content (right pane)
- RewardsPopup (line 156) - Rewards popup overlay
- FriendsPanelChallenge (line 165) - Friends panel: Challenge action button
- FriendsPanelAddFriend (line 170) - Friends panel: Add Friend action button
- FriendSectionFriends (line 176) - Friends panel: Actual friends list
- FriendSectionIncoming (line 181) - Friends panel: Incoming friend requests
- FriendSectionOutgoing (line 186) - Friends panel: Outgoing friend requests
- FriendSectionBlocked (line 191) - Friends panel: Blocked users
- FriendsPanelProfile (line 196) - Friends panel: Local player profile

## Static Classes

### ElementGroupExtensions (line 203)
```csharp
public static class ElementGroupExtensions
```
Extension methods for ElementGroup.

#### Methods
- public static bool IsOverlay(this ElementGroup group) (line 208)
  - Returns true if this group is an overlay group that suppresses other groups
- public static bool IsFriendPanelGroup(this ElementGroup group) (line 238)
  - Returns true if this group is one of the friend panel sub-groups
- public static bool IsFriendSectionGroup(this ElementGroup group) (line 253)
  - Returns true if this group is a friend section (not action buttons)
- public static bool IsDeckBuilderCardGroup(this ElementGroup group) (line 265)
  - Returns true if this group is a deck builder card group
- public static bool IsChallengeGroup(this ElementGroup group) (line 275)
  - Returns true if this group is the challenge main group
- public static string GetDisplayName(this ElementGroup group) (line 283)
  - Returns a screen-reader friendly localized name for the group
