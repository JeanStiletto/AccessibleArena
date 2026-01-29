# Informational Text Investigation

This document tracks informational text elements in MTGA that could be announced for accessibility but are not currently tracked.

## Investigation Date: 2026-01-28

---

## 1. SocialCornerIcon (Chat Bubble on Home Page)

**Status:** Not tracked

**Location:**
- Path: `SocialUI_V2_Desktop_16x9(Clone)/MobileSafeArea/CornerIconRoot_Wrapper/SocialCornerIcon`
- Components: CustomButton, Animator, RolloverAudioPlayer

**Content:**
- Shows notification count (e.g., "0" for no messages)
- Text child element contains the number

**Use Case:**
- Announce friend requests or message notifications
- Could say "Social, 3 notifications" when focused

---

## 2. Tooltip Descriptions (LocalizedString)

**Status:** Not tracked

**Location:**
- Many UI elements have `TooltipTrigger` component
- Contains `TooltipData` with `LocString (LocalizedString)` field

**Examples Found in Log:**
- "Deck-Layout ändern" (Change deck layout)
- "Direkte Herausforderung" (Direct Challenge)
- "Kodex des Multiversums" (Multiverse Codex)
- "Optionen anpassen" (Customize options)
- "Verdiene Gold, indem du spielst und Quests abschließt" (Earn gold by playing and completing quests)
- "Edelsteine können im Laden gekauft werden" (Gems can be bought in store)
- "Post" (Mail)

**Use Case:**
- When focusing on an element with TooltipTrigger, announce the LocString as a description
- Could be triggered by a "read description" hotkey or automatically after element name

---

## 3. Popup Body Text (SystemMessageView)

**Status:** IMPLEMENTED (2026-01-28)

**Location:**
- Path: `SystemMessageView_Desktop_16x9(Clone)/SystemMessageView_OK_Cancel/MessageArea/Scroll View`
- The `MessageArea` contains the descriptive text explaining the popup

**Implementation:**
- Added `UITextExtractor.GetPopupBodyText()` method to extract body text from popups
- `GeneralMenuNavigator.OnPanelStateManagerAnyOpened()` detects popup opening
- Calls `AnnouncePopupBodyText()` to announce the body text before rescan

**Files Modified:**
- `src/Core/Services/UITextExtractor.cs` - Added `GetPopupBodyText()` method
- `src/Core/Services/GeneralMenuNavigator.cs` - Added popup detection and announcement

**Behavior:**
- When a popup/dialog opens, the body text is announced first (High priority)
- Then the normal rescan happens to announce available buttons
- Example flow: "Do you want to discard changes?" -> "2 items. 1 of 2: Discard deck, button"

**Search Paths Used:**
1. `SystemMessageView_OK_Cancel/MessageArea/Scroll View/Viewport/Content`
2. `SystemMessageView_OK/MessageArea/Scroll View/Viewport/Content`
3. `MessageArea/Scroll View/Viewport/Content`
4. Recursive search for `MessageArea` child
5. Fallback: any TMP_Text not inside button containers with >10 chars

---

## 4. Deck Builder Card Count

**Status:** Not tracked

**Location:**
- `TitlePanel_MainDeck` - Shows deck name
- `TitlePanel_Container` - Contains title panel elements
- `MainDeck_MetaCardHolder` - Contains the deck card list
- Individual cards show counts like "4x", "23x"

**Missing Element:**
- Deck size indicator (e.g., "60/60" or "23/60")
- Likely a child text element of `TitlePanel_Container` or a sibling of `TitlePanel_MainDeck`
- Not captured in current UI dump - may need deeper hierarchy exploration

**Use Case:**
- Announce deck size when entering deck builder or on demand
- "Deck: New Deck Name, 23 of 60 cards"

---

## 5. Quest/Objective Progress

**Status:** COMPLETED (2026-01-29)

**Location:**
- `ContentController - Objectives_Desktop_16x9(Clone)/SafeArea/ObjectivesLayout_CONTAINER/Objective_Base(Clone)`
- Clickable element: `ObjectiveGraphics` (has CustomButton component)

**Objective Types and Labels (Final Implementation):**
- `Objective_Base(Clone) - Daily` → "Daily: 0/15 wins, 250 gold"
- `Objective_Base(Clone) - Weekly` → "Weekly: 5/15"
- `Objective_Base(Clone) - QuestNormal` → "Cast 20 spells, 14/20, 500 gold" (description + progress + reward)
- `Objective_BattlePass_Base(Clone) - BattlePass - Level` → "Battle Pass Level: 7, 400/1000 EP"
- `Objective_NPEQuest(Clone) - SparkRankTier1` → "Spark Rank: 0/1"

**Implementation Completed:**

1. **ElementGroup.Objectives** - Subgroup within Progress
   - File: `src/Core/Services/ElementGrouping/ElementGroup.cs`
   - Objectives are stored in `_subgroupElements` and accessed via "Objectives, X items" entry in Progress

2. **ElementGroupAssigner** - Objective detection and NPE exclusion
   - File: `src/Core/Services/ElementGrouping/ElementGroupAssigner.cs`
   - Method: `IsObjectiveElement()` detects elements by path patterns (Objective_Base, Objective_BattlePass, Objective_NPE)
   - Fix: `DetermineOverlayGroup()` excludes `Objective_NPE` from NPE overlay classification

3. **UITextExtractor** - Full text extraction for objectives
   - File: `src/Core/Services/UITextExtractor.cs`
   - Method: `TryGetObjectiveText()` extracts complete info from objectives
   - For quests: description + progress + reward
   - For progress bars: type prefix + progress value

4. **UIElementClassifier** - Progress indicator classification
   - File: `src/Core/Services/UIElementClassifier.cs`
   - Method: `TryClassifyAsProgressIndicator()` classifies objectives as ProgressBar role
   - Method: `IsProgressIndicator()` matches "objective" in name

**Key Discovery - ObjectiveGraphics Internal Structure:**

```
ObjectiveGraphics (CustomButton)
├── TextLine                    ← Contains description text (for quests)
│   ├── DescriptionContainer
│   └── Text_Description        ← May have localized text (empty for SparkRank)
├── Circle                      ← Contains reward/level number
│   ├── Text_RewardQuantity
│   └── ... (visual elements)
└── Text_GoalProgress           ← Contains progress fraction (e.g., "14/20", "400/1000 EP")
```

**Text Extraction Logic (TryGetObjectiveText):**

1. Check if element is `ObjectiveGraphics`
2. Extract objective type from parent name (e.g., "Daily", "Weekly", "QuestNormal", "BattlePass - Level")
3. For QuestNormal:
   - Find `TextLine` child → get description via `GetComponentInChildren<TMP_Text>()`
   - Find `Text_GoalProgress` child → get progress fraction
   - Find `Circle` child → get reward amount
   - Return: "{description}, {progress}, {reward} gold"
4. For other types (Daily, Weekly, BattlePass):
   - Find `Circle` child → get main value
   - Find `Text_GoalProgress` child → get detailed progress
   - Return type-specific format (e.g., "Daily: {progress} wins, {value} gold")

**Important Lessons Learned:**

1. **GetComponent vs GetComponentInChildren:** Text is often in child elements, not directly on the named element. Always use `GetComponentInChildren<TMP_Text>()` when searching for text.

2. **Parent name contains type info:** The parent element name (e.g., "Objective_Base(Clone) - Daily") contains the objective type after " - ".

3. **Multiple text sources:** A single UI element may have multiple text children with different purposes (description, progress, reward). Need to find specific named children.

4. **NPE overlay conflict:** Elements with "NPE" in path were being classified as NPE overlay. Fixed by excluding "Objective_NPE" from NPE check.

5. **Debug helper pattern:** `MenuDebugHelper.DumpGameObjectDetails()` is useful for investigating unknown UI structures.

**Files Modified:**
- `src/Core/Services/UITextExtractor.cs` - Added `TryGetObjectiveText()` method
- `src/Core/Services/ElementGrouping/ElementGroupAssigner.cs` - NPE exclusion for Objective_NPE
- `src/Core/Services/ElementGrouping/GroupedNavigator.cs` - Objectives subgroup handling
- `src/Core/Services/MenuDebugHelper.cs` - Added `DumpGameObjectDetails()` debug utility

---

## Implementation Priority

1. ~~**Popup Body Text** - High priority, users need to know what popups are asking~~ **DONE**
2. ~~**Objectives** - High priority, quest/daily/weekly/battle pass progress~~ **DONE**
3. **Tooltip Descriptions** - Medium priority, adds context to UI elements
4. **Deck Card Count** - Medium priority, important for deck building
5. **SocialCornerIcon** - Low priority, nice to have for notifications

---

## Technical Notes

### TooltipTrigger Structure
```
TooltipTrigger
├── IsActive (Boolean)
├── TooltipData (TooltipData)
│   └── LocString (LocalizedString) - The descriptive text
├── tooltipContext (TooltipContext)
└── TooltipProperties (TooltipProperties)
```

### SystemMessageView Structure
```
SystemMessageView_Desktop_16x9(Clone)
└── SystemMessageView_OK_Cancel
    ├── MessageArea
    │   └── Scroll View
    │       └── Viewport
    │           └── Content (contains body text)
    └── ButtonLayout
        └── SystemMessageButton_Desktop_16x9(Clone) (multiple)
```

### Deck Builder Structure
```
DeckListView_Desktop_16x9(Clone)
└── Blade_Deck
    ├── TitlePanel_Container
    │   ├── TitlePanel_MainDeck (deck name)
    │   ├── SideboardToggle
    │   └── [Card count element - to be found]
    └── MetaCardHolders_Container
        └── MainDeck_MetaCardHolder
            └── CardsList_ScrollRect
```

### Objectives Panel Structure
```
ContentController - Objectives_Desktop_16x9(Clone)
└── SafeArea
    └── ObjectivesLayout_CONTAINER
        ├── Objective_Base(Clone) - Daily
        │   └── ObjectiveGraphics (CustomButton)
        │       ├── TextLine → "250"
        │       ├── Circle → "250"
        │       └── Text_GoalProgress → "0/15" (wins progress!)
        ├── Objective_Base(Clone) - Weekly
        │   └── ObjectiveGraphics (CustomButton)
        │       ├── TextLine → "5/15"
        │       ├── Circle → "5/15"
        │       └── Text_GoalProgress → "5/15"
        ├── Objective_Base(Clone) - QuestNormal (multiple)
        │   └── ObjectiveGraphics (CustomButton)
        │       ├── TextLine → quest description (e.g., "Cast 20 spells")
        │       ├── Circle → reward amount (e.g., "500")
        │       └── Text_GoalProgress → progress (e.g., "14/20")
        ├── Objective_BattlePass_Base(Clone) - BattlePass - Level
        │   └── ObjectiveGraphics (CustomButton)
        │       ├── Circle → level number (e.g., "7")
        │       └── Text_GoalProgress → "400/1000 EP" (XP progress!)
        └── Objective_NPEQuest(Clone) - SparkRankTier1
            └── ObjectiveGraphics (CustomButton)
                ├── TextLine
                │   ├── DescriptionContainer
                │   └── Text_Description → (empty)
                ├── Circle → "0/1"
                └── Text_GoalProgress → "0/1"
```

**Key Insights:**
- ObjectiveGraphics has multiple text children with different purposes
- `TextLine` contains the main display text (description for quests, progress for others)
- `Circle` contains reward amount (quests) or main value (progress bars)
- `Text_GoalProgress` contains the detailed progress fraction - this is where hidden info like "0/15 wins" for Daily lives!
- Parent name after " - " contains the objective type (Daily, Weekly, QuestNormal, BattlePass - Level, SparkRankTier1)
- Use `GetComponentInChildren<TMP_Text>()` to find text in children, not `GetComponent<TMP_Text>()`

**Note:** ObjectiveGraphics elements have:
- Components: RectTransform, CanvasRenderer, CustomButton
- Size: 0x0 (unusual - doesn't affect classification since we check name "ObjectiveGraphics")
- HasActualText: True (text comes from child elements)
