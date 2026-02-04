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

## 6. Play Mode Tabs (FindMatch)

**Status:** IMPLEMENTED (2026-02-01)

**Location:**
- Path: `.../BladeView_CONTAINER/Blade_FindMatch/FindMatchTabs/Tabs/`
- Elements: `Blade_Tab_Deluxe (OpenPlay)`, `Blade_Tab_Deluxe (Brawl)`, `Blade_Tab_Ranked`
- Components: RectTransform, Animator, CustomButton, CustomTab

**Problem:**
- Displayed text is generic German translation: "Spiele" (Play), "Mit Rangliste" (With Ranking)
- The actual game mode names are in the element names, not the displayed text
- No TooltipTrigger on these elements (checked - they only have CustomTab, no tooltip data)

**Solution:**
- Extract mode name from element name instead of displayed text
- Pattern 1: `Blade_Tab_Deluxe (ModeName)` → extract from parentheses
- Pattern 2: `Blade_Tab_Ranked` → extract suffix after `Blade_Tab_`

**Implementation:**
- File: `src/Core/Services/UITextExtractor.cs`
- Method: `TryGetPlayModeTabText()` - extracts play mode from element name
- Called in `GetText()` after objective check

**Text Extraction Logic:**
1. Check if element name starts with `Blade_Tab_`
2. Verify context: parent is `Tabs`, grandparent contains `FindMatchTabs`
3. Extract mode from parentheses or suffix
4. Clean up camelCase to spaces (e.g., "OpenPlay" → "Open Play")
5. Apply proper casing

**Results:**
- `Blade_Tab_Deluxe (OpenPlay)` → "Open Play" (was "Spiele")
- `Blade_Tab_Deluxe (Brawl)` → "Brawl" (unchanged)
- `Blade_Tab_Ranked` → "Ranked" (was "Mit Rangliste")

**Key Insight - Element Name Contains Mode Info:**
- Similar pattern to objectives where parent name contains type info
- The element name itself contains the canonical English mode identifier
- The displayed text is a localized translation that may be generic
- When displayed text is ambiguous, prefer extracting from element name

**Note:** These tabs do NOT have TooltipTrigger, so tooltip-based extraction is not possible here.

---

## 7. DeckManager Icon Buttons

**Status:** IMPLEMENTED (2026-02-01)

**Location:**
- Path: `Canvas/ContentController - DeckManager_Desktop_16x9(Clone)/SafeArea/MainButtons/`
- Elements: `Clone_MainButton_Round`, `Delete_MainButton_Round`, `Export_MainButton_Round`, etc.
- Components: RectTransform, CanvasRenderer, Image, CustomButton, Animator, LayoutElement, TooltipTrigger

**Problem:**
- These are icon-only buttons (no text, just images): `HasActualText: False`, `HasImage: True`
- All 7 buttons were showing "Sammlung" (Collection) - picking up text from nearby `MainButton_Collection`
- The element names contain the actual function (Clone, Delete, Export, etc.)
- TooltipTrigger exists with `IsActive=True` but we extracted from element name for consistency

**Solution:**
- Extract function name from element name prefix
- Pattern: `Function_MainButton_Round` → extract "Function" before `_MainButton_Round`
- Pattern: `Function_MainButtonBlue` → extract "Function" before `_MainButtonBlue`

**Implementation:**
- File: `src/Core/Services/UITextExtractor.cs`
- Method: `TryGetDeckManagerButtonText()` - extracts button function from element name
- Called in `GetText()` after play mode tab check

**Text Extraction Logic:**
1. Check if element name ends with `_MainButton_Round` or `_MainButtonBlue`
2. Verify context: element is inside a DeckManager hierarchy
3. Extract function name (prefix before the suffix)
4. Clean up camelCase to spaces
5. Apply specific mappings for better labels

**Results:**
- `Clone_MainButton_Round` → "Clone Deck" (was "Sammlung")
- `DeckDetails_MainButton_Round` → "Deck Details" (was "Sammlung")
- `Delete_MainButton_Round` → "Delete Deck" (was "Sammlung")
- `Export_MainButton_Round` → "Export Deck" (was "Sammlung")
- `Import_MainButton_Round` → "Import Deck" (was "Sammlung")
- `Favorite_MainButton_Round` → "Favorite" (was "Sammlung")
- `EditDeck_MainButtonBlue` → "Edit Deck" (was "Deck verändern" - already had text)
- `MainButton_Collection` → "Sammlung" (unchanged - has actual text, correctly labeled)

**Key Insight - Icon Buttons Need Element Name Extraction:**
- Icon-only buttons have no text content (`HasActualText: False`)
- Text extractor may pick up text from nearby elements (sibling fallback)
- Element names often contain the function in a predictable pattern
- Context check (DeckManager hierarchy) prevents false matches elsewhere

**Note:** These buttons DO have TooltipTrigger with `IsActive=True`, which could be used as an alternative source.

---

## Implementation Priority

1. ~~**Popup Body Text** - High priority, users need to know what popups are asking~~ **DONE**
2. ~~**Objectives** - High priority, quest/daily/weekly/battle pass progress~~ **DONE**
3. ~~**Play Mode Tabs** - High priority, users need to know which game mode they're selecting~~ **DONE**
4. ~~**DeckManager Buttons** - High priority, icon buttons need proper labels~~ **DONE**
5. **Tooltip Descriptions** - Medium priority, adds context to UI elements
6. **Deck Card Count** - Medium priority, important for deck building
7. **SocialCornerIcon** - Low priority, nice to have for notifications

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

### PlayBlade FindMatch Tabs Structure
```
BladeView_CONTAINER
└── Blade_FindMatch
    └── FindMatchTabs
        └── Tabs
            ├── Blade_Tab_Deluxe (Brawl)      ← Text: "Brawl", extracted: "Brawl"
            │   Components: RectTransform, Animator, CustomButton, CustomTab
            ├── Blade_Tab_Deluxe (OpenPlay)   ← Text: "Spiele", extracted: "Open Play"
            │   Components: RectTransform, Animator, CustomButton, CustomTab, AkGameObj
            └── Blade_Tab_Ranked              ← Text: "Mit Rangliste", extracted: "Ranked"
                Components: RectTransform, Animator, CustomButton, CustomTab
```

**Key Insights:**
- Play mode tabs have `CustomTab` component but NO `TooltipTrigger`
- Element names contain the canonical mode identifier in parentheses or as suffix
- Displayed text is localized and may be generic (e.g., "Spiele" = "Play")
- Context check required: parent must be "Tabs", grandparent must contain "FindMatchTabs"
- This prevents false matches on other `Blade_Tab_` elements elsewhere in the UI

### DeckManager MainButtons Structure
```
DeckManager_Desktop_16x9(Clone)
└── SafeArea
    └── MainButtons
        ├── Clone_MainButton_Round        ← Icon only, extracted: "Clone Deck"
        │   Components: Image, CustomButton, TooltipTrigger
        ├── DeckDetails_MainButton_Round  ← Icon only, extracted: "Deck Details"
        │   Components: Image, CustomButton, TooltipTrigger
        ├── Delete_MainButton_Round       ← Icon only, extracted: "Delete Deck"
        │   Components: Image, CustomButton, TooltipTrigger
        ├── Export_MainButton_Round       ← Icon only, extracted: "Export Deck"
        │   Components: Image, CustomButton, TooltipTrigger
        ├── Import_MainButton_Round       ← Icon only, extracted: "Import Deck"
        │   Components: Image, CustomButton, TooltipTrigger
        ├── Favorite_MainButton_Round     ← Icon only, extracted: "Favorite"
        │   Components: Image, CustomButton, TooltipTrigger
        ├── EditDeck_MainButtonBlue       ← Has text "Deck verändern", extracted: "Edit Deck"
        │   Components: CustomButton, CanvasGroup
        └── MainButton_Collection         ← Has text "Sammlung" (correct)
            Components: CustomButton, CanvasGroup, Image
```

**Key Insights:**
- Icon buttons have `HasActualText: False`, `HasImage: True`, `HasTextChild: False`
- Without extraction, text extractor picks up "Sammlung" from `MainButton_Collection` sibling
- Element name prefix contains the function: `Clone_MainButton_Round` → "Clone"
- Two patterns: `*_MainButton_Round` (icon buttons) and `*_MainButtonBlue` (text buttons)
- All icon buttons have `TooltipTrigger` with `IsActive=True` as alternative source
- Context check (DeckManager hierarchy) prevents false matches on other MainButton elements

---

## 8. Wildcard Progress (Packs Screen)

**Status:** IMPLEMENTED (2026-02-04)

**Location:**
- Path: `.../SafeArea/WildcardRewards/Rare/Wildcard Progress Rare/ObjectiveGraphics`
- Path: `.../SafeArea/WildcardRewards/Uncommon/WildcardProgressUncommon/ObjectiveGraphics`
- Components: RectTransform, CanvasRenderer, CustomButton

**Problem:**
- Wildcard progress indicators on the Packs screen were showing "objective graphics, progress"
- The parent name doesn't follow the standard "Objective_Base(Clone) - Type" pattern
- Instead uses names like "WildcardProgressUncommon" and "Wildcard Progress Rare"

**Solution:**
- Added special handling in `TryGetObjectiveText()` for wildcard progress patterns
- Detect parent names containing "WildcardProgress" or "Wildcard Progress"
- Extract rarity from parent name (Uncommon, Rare, Mythic, Common)
- Look for progress value in child elements (Text_GoalProgress)
- Fallback to image fill amount for percentage display

**Implementation:**
- File: `src/Core/Services/UITextExtractor.cs`
- Method: `TryGetWildcardProgressText()` - extracts wildcard progress from ObjectiveGraphics elements
- Called from `TryGetObjectiveText()` when parent name contains wildcard patterns

**Text Extraction Logic:**
1. Check if parent name contains "WildcardProgress" or "Wildcard Progress"
2. Extract rarity from parent name (case insensitive)
3. Search children for `Text_GoalProgress` or elements with progress fraction
4. Fallback: check Image components for fill amount
5. Return formatted label: "{Rarity} Wildcard: {progress}"

**Results:**
- `WildcardProgressUncommon` → "Uncommon Wildcard: 3/6" (was "objective graphics, progress")
- `Wildcard Progress Rare` → "Rare Wildcard: 2/6" (was "objective graphics, progress")

**Key Insight - Pattern Variation:**
- The standard objective pattern uses " - " separator (e.g., "Objective_Base(Clone) - Daily")
- Wildcard progress uses different patterns without the separator
- Need to check for both standard and variant patterns in objective text extraction

---

## 9. Vault Progress (Pack Opening)

**Status:** IMPROVED (2026-02-04)

**Location:**
- Pack contents screen (BoosterOpenNavigator)
- Appears when opening 5th+ copy of common/uncommon cards
- Element: `Prefab - BoosterMetaCardView_v2(Clone)` with no Title but has progress quantity

**Previous Behavior:**
- Showed just "Vault Progress +99" with no context about overall vault state

**Improved Behavior:**
- Now searches for additional context: description text, percentage labels, fill bar amounts
- Reports: "Vault Progress, +99, total 45%" when fill bar is available
- Falls back to description text if percentage not available

**Implementation:**
- File: `src/Core/Services/BoosterOpenNavigator.cs`
- Method: `ExtractCardName()` - enhanced vault progress extraction

**Extraction Logic:**
1. Detect vault progress entry (has ProgressQuantity text but no Title)
2. Search for additional text elements: description, label, percentage
3. Check Image components for fill amount (Type.Filled with 0 < amount < 1)
4. Build informative label with all available context

**Structure of Vault Progress Entry:**
```
Prefab - BoosterMetaCardView_v2(Clone)
├── Title                      ← Empty for vault progress
├── ProgressQuantity           ← Contains "+99" etc.
├── Description/Label          ← May contain explanation text
├── Fill/ProgressBar           ← Image with fillAmount indicating total vault %
└── Percentage                 ← May contain "45%" text
```

**Key Insight - Vault Progress vs Regular Cards:**
- Regular cards have a Title text element with the card name
- Vault progress entries have no Title, only ProgressQuantity
- This distinction allows identifying duplicate protection entries
