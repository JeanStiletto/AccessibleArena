# MTGA Accessibility Mod: Up/Down Arrow Key Input Flow Analysis

## Question 1: CardInfoNavigator - Return Value for Up/Down When Inactive

**File:** src/Core/Services/CardInfoNavigator.cs (lines 144-204)

### Key Finding: CardInfoNavigator RETURNS FALSE (input NOT consumed)

When CardInfoNavigator.IsActive=False, the method returns **FALSE** at line 152:

\\\csharp
144. public bool HandleInput()
145. {
146.     // Debug: Log why HandleInput might return false early
147.     if (!_isActive)
148.     {
149.         // Only log occasionally to avoid spam - check if arrow pressed
150.         if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.UpArrow))
151.             MelonLogger.Msg(\[CardInfo] HandleInput: Not active, ignoring arrow key\);
152.         return false;  // <-- PASSES INPUT THROUGH
153.     }
...
173.     if (Input.GetKeyDown(KeyCode.DownArrow))
174.     {
175.         if (!_blocksLoaded) { if (!LoadBlocks()) return false; }
176.         NavigateNext();
177.         return true;  // <-- CONSUMES INPUT (only when IsActive=True)
182.     }
\\\

**When active and Up/Down are pressed, it RETURNS TRUE (lines 177-182, 193-194)**, consuming the input.

---

## Question 2: BaseNavigator - Update(), MoveNext(), MovePrevious()

**File:** src/Core/Services/BaseNavigator.cs

### Update() Method (lines 444-498)

\\\csharp
444. public virtual void Update()
445. {
446.     // If not active, try to detect and activate
447.     if (!_isActive)
448.     {
449.         TryActivate();
450.         return;
451.     }
452.     // ... delayed updates ...
453.     HandleInput();  // <-- CALLS MAIN INPUT HANDLER
492.     HandleInput();
493.     
494.     // Track input field text for NEXT frame's Backspace character announcement
495.     TrackInputFieldState();
498. }
\\\

### HandleInput() Method (lines 1144-1365)

Key sections:
- **Lines 1280-1283:** Up/Down arrow handling calls MoveNext/MovePrevious
\\\csharp
1280. if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
1281. {
1282.     MovePrevious();
1283.     return;
1284. }
1286. if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
1287. {
1288.     MoveNext();
1289.     return;
1290. }
\\\

### MoveNext() and MovePrevious() Methods (lines 1829-1830)

\\\csharp
1829. protected virtual void MoveNext() => Move(1);
1830. protected virtual void MovePrevious() => Move(-1);
\\\

### Move() Method (lines 1644-1680) - Core Navigation Logic

\\\csharp
1644. protected virtual void Move(int direction)
1645. {
1646.     if (_elements.Count == 0) return;
1647. 
1648.     // Single element: re-announce it instead of saying "end/beginning of list"
1649.     if (_elements.Count == 1)
1650.     {
1651.         AnnounceCurrentElement();
1652.         return;
1653.     }
1654. 
1655.     int newIndex = _currentIndex + direction;
1656. 
1657.     // Check boundaries - no wrapping
1658.     if (newIndex < 0)
1659.     {
1660.         _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
1661.         return;
1662.     }
1663. 
1664.     if (newIndex >= _elements.Count)
1665.     {
1666.         _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
1667.         return;
1668.     }
1669. 
1670.     _currentIndex = newIndex;
1671.     _currentActionIndex = 0; // Reset action index when moving to new element
1672. 
1673.     // Update EventSystem selection to match our navigation
1674.     UpdateEventSystemSelection();
1675. 
1676.     AnnounceCurrentElement();
1677.     UpdateCardNavigation();
1678. }
\\\

### Grouped Navigation Integration

BaseNavigator does NOT handle grouped navigation directly. **It's overridden by subclasses** (e.g., GeneralMenuNavigator).

---

## Question 3: GeneralMenuNavigator - Update() and Input Flow

**File:** src/Core/Services/GeneralMenuNavigator.cs

### Update() Method (lines 1076-1159)

\\\csharp
1076. public override void Update()
1077. {
1078.     // Handle rescan delay...
1079.     if (_rescanDelay > 0)
1080.     {
1081.         _rescanDelay -= Time.deltaTime;
1082.         // ... rescan logic ...
1086.         // Don't return early - still process input during rescan delay
1087.     }
1088. 
1089.     // Handle delayed page rescan (collection scroll animation)...
1090.     // Handle blade auto-expand...
1091.     // NPE scene periodic checks...
1092.     // Overlay state change detection...
1093. 
1158.     // Panel detection handled by PanelDetectorManager via events
1159.     base.Update();  // <-- CALLS BaseNavigator.Update()
1160. }
\\\

**Key:** GeneralMenuNavigator.Update() does custom timing/delay logic, THEN calls ase.Update() which invokes HandleInput() from BaseNavigator.

### MoveNext() Override (lines 4339-4407)

\\\csharp
4339. protected override void MoveNext()
4340. {
4341.     if (_groupedNavigationEnabled && _groupedNavigator.IsActive)
4342.     {
4343.         // DeckBuilderInfo 2D navigation: Down arrow switches to next row
4344.         // Skip when Tab is pressed - let Tab cycling handle group switching
4345.         if (IsDeckInfoSubNavActive() && !Input.GetKey(KeyCode.Tab))
4346.         {
4347.             bool moved = _groupedNavigator.MoveNext();
4348.             if (moved)
4349.             {
4350.                 _deckInfoEntryIndex = 0;
4351.                 AnnounceDeckInfoEntry(includeRowName: true);
4352.             }
4353.             return;
4354.         }
4355. 
4356.         // Friend section navigation
4357.         if (IsFriendSectionActive() && !Input.GetKey(KeyCode.Tab))
4358.         {
4359.             bool moved = _groupedNavigator.MoveNext();
4360.             if (moved) { /* ... */ }
4361.             return;
4362.         }
4363. 
4364.         // Deck builder Tab-specific cycling
4365.         bool isTabPressed = Input.GetKey(KeyCode.Tab);
4366.         if (_activeContentController == "WrapperDeckBuilder" && isTabPressed)
4367.         {
4367.             if (_groupedNavigator.CycleToNextGroup(DeckBuilderCycleGroups))
4368.             {
4369.                 // ... handle deck info entry ...
4370.                 return;
4371.             }
4372.         }
4373. 
4374.         // DEFAULT: navigate all groups/elements
4401.         _groupedNavigator.MoveNext();  // <-- DOWN ARROW DELEGATES HERE
4402.         UpdateEventSystemSelectionForGroupedElement();
4403.         UpdateCardNavigationForGroupedElement();
4404.         return;
4405.     }
4406.     base.MoveNext();  // <-- Only called if grouped nav NOT active
4407. }
\\\

### MovePrevious() Override (lines 4414-4480)

Identical structure to MoveNext, but calls _groupedNavigator.MovePrevious() (line 4475) and ase.MovePrevious() (line 4480).

**Critical Insight:** 
- When _groupedNavigationEnabled=true AND _groupedNavigator.IsActive=true:
  - Down/Up press → **GroupedNavigator handles movement** (lines 4401, 4475)
  - Returns from override WITHOUT calling ase.MoveNext()
- When grouped nav NOT active:
  - Calls ase.MoveNext()/ase.MovePrevious() for normal flat navigation

---

## Question 4: GroupedNavigator - MoveNext() / MovePrevious()

**File:** src/Core/Services/ElementGrouping/GroupedNavigator.cs

### MoveNext() (lines 1422-1428)

\\\csharp
1422. public bool MoveNext()
1423. {
1424.     if (_navigationLevel == NavigationLevel.GroupList)
1425.         return MoveNextGroup();      // <-- AT GROUP LEVEL
1426.     else
1427.         return MoveNextElement();    // <-- INSIDE A GROUP
1428. }
\\\

### MovePrevious() (lines 1434-1440)

\\\csharp
1434. public bool MovePrevious()
1435. {
1436.     if (_navigationLevel == NavigationLevel.GroupList)
1437.         return MovePreviousGroup();
1438.     else
1439.         return MovePreviousElement();
1440. }
\\\

### MoveNextElement() - Inside Group (lines 1676-1692)

\\\csharp
1676. private bool MoveNextElement()
1677. {
1678.     int count = GetCurrentElementCount();
1679.     if (count == 0) return false;
1680. 
1681.     if (_currentElementIndex >= count - 1)
1682.     {
1683.         // Single element: re-announce it before saying end of list
1684.         if (count == 1)
1685.             AnnounceCurrentElement();  // <-- ANNOUNCES CURRENT ITEM
1686.         _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
1687.         return false;
1688.     }
1689. 
1690.     _currentElementIndex++;
1691.     AnnounceCurrentElement();          // <-- ANNOUNCES MOVED ITEM
1692.     return true;
1693. }
\\\

**Key Behavior:**
- At InsideGroup level: Down announces the current/next item in the group
- Returns 	rue if moved, alse if at end

### MoveNextGroup() - Group Level (lines 1644-1658)

\\\csharp
1644. private bool MoveNextGroup()
1645. {
1646.     if (_currentGroupIndex >= _groups.Count - 1)
1647.     {
1648.         if (_groups.Count == 1)
1649.             AnnounceCurrentGroup();   // <-- ANNOUNCES GROUP NAME
1650.         _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
1651.         return false;
1652.     }
1653. 
1654.     _currentGroupIndex++;
1655.     AnnounceCurrentGroup();            // <-- ANNOUNCES NEW GROUP NAME
1656.     return true;
1657. }
\\\

**Key Behavior:**
- At GroupList level: Down announces the group name/count
- E.g., "Play Mode Selection, 8 groups" → "Constructed, 2 items"

---

## Question 5: Navigator Priority System & Input Routing

**File:** src/Core/Services/NavigatorManager.cs

### Priority System (lines 33-39, 51-102)

\\\csharp
33. public void Register(IScreenNavigator navigator)
34. {
35.     _navigators.Add(navigator);
36.     // Sort by priority descending
37.     _navigators.Sort((a, b) => b.Priority.CompareTo(a.Priority));
38.     MelonLogger.Msg(\[NavigatorManager] Registered: {navigator.NavigatorId} (priority {navigator.Priority})\);
39. }
\\\

### Update Polling Order (lines 51-102)

\\\csharp
51. public void Update()
52. {
53.     // Check if a higher-priority navigator should preempt the current one
54.     if (_activeNavigator != null)
55.     {
56.         // Poll higher-priority navigators FIRST
57.         foreach (var navigator in _navigators)
58.         {
59.             // Only check navigators with higher priority than the active one
60.             if (navigator.Priority <= _activeNavigator.Priority)
61.                 break; // Sorted descending - stop here
62. 
63.             // Let navigator poll (may activate)
64.             navigator.Update();
65. 
66.             if (navigator.IsActive)
67.             {
67.                 // Higher priority navigator preempted current one
68.                 _activeNavigator.Deactivate();
69.                 _activeNavigator = navigator;
70.                 return;
71.             }
72.         }
73. 
74.         // No preemption - let active navigator handle input
75.         _activeNavigator.Update();  // <-- ONLY ONE NAVIGATOR HANDLES INPUT PER FRAME
76. 
77.         if (!_activeNavigator.IsActive)
78.             _activeNavigator = null;
79.         return;
80.     }
\\\

### Key Findings:

1. **Navigators are sorted by priority (descending)**
2. **Only ONE navigator is active at a time**
3. **That navigator's Update() is called (line 75)**
4. **Higher-priority navigators can preempt lower ones (lines 56-72)**

Example priorities (from logs):
- RewardPopupNavigator: 86 (high priority, pops over everything)
- GeneralMenuNavigator: 15 (low priority base navigator)

---

## ROOT CAUSE OF THE BUG: Down Arrow Not Moving Between Tabs

Based on the code analysis:

### Symptom:
- Down press doesn't move to next tab in Play blade
- Log shows: \[CardInfo] Up/Down pressed but CardInfoNavigator.IsActive=False, CurrentCard=null\
- GroupedNavigator should move between "Play Mode Selection" tabs

### Why It Happens:

**The grouped navigator is stuck at GroupList level instead of progressing:**

1. **Initial State:** Play blade opens with 5 tabs (groups)
2. **Navigator at GroupList level:** Positioned at tab 0 (Play Mode Selection)
3. **Down press:** GeneralMenuNavigator.MoveNext() → GroupedNavigator.MoveNext() (line 4401)
4. **GroupedNavigator.MoveNext():** Calls MoveNextGroup() (line 1425) since navigationLevel=GroupList
5. **Bug Point:** MoveNextGroup() at line 1646 checks:
   \\\csharp
   if (_currentGroupIndex >= _groups.Count - 1)  // IF AT LAST GROUP
   {
       // Announce "End of list" - DON'T MOVE
       _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
       return false;
   }
   \\\

**Question:** Is the grouped navigator interpreting tabs as "groups" instead of allowing navigation WITHIN the tab group?

The logs suggest: "8 groups" announced, but pressing Down doesn't iterate the tabs. This implies:
- Either the tabs aren't being loaded as elements
- Or the grouped navigator's groups != the tabs themselves
- Or the grouped navigator initialization isn't recognizing PlayBladeTabs as the active group

---

## Summary Table

| Component | File | Key Method | Behavior |
|-----------|------|-----------|----------|
| **CardInfoNavigator** | CardInfoNavigator.cs | HandleInput() L144 | Returns FALSE when inactive (passes through) |
| **BaseNavigator** | BaseNavigator.cs | HandleInput() L1144 | Calls MoveNext/Previous (L1286-1289) |
| **BaseNavigator** | BaseNavigator.cs | Move() L1644 | Navigates flat list, no groups |
| **GeneralMenuNavigator** | GeneralMenuNavigator.cs | MoveNext() L4339 | OVERRIDES to use GroupedNavigator (L4401) |
| **GeneralMenuNavigator** | GeneralMenuNavigator.cs | Update() L1076 | Custom delays, then calls base.Update() (L1159) |
| **GroupedNavigator** | GroupedNavigator.cs | MoveNext() L1422 | Routes to MoveNextGroup/Element based on level |
| **GroupedNavigator** | GroupedNavigator.cs | MoveNextGroup() L1644 | Announces group, moves to next tab |
| **NavigatorManager** | NavigatorManager.cs | Update() L51 | Polls priority-sorted navigators, activates one |

