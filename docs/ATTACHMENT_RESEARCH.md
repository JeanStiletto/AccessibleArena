# Attachment/Enchantment Research Findings

Investigation into how MTGA handles card attachments (auras, equipment) for accessibility announcements.

**Date:** 2026-01-26
**Status:** Research complete, implementation pending

## Current Problem

Per KNOWN_ISSUES.md, the existing code in `CardModelProvider.cs` uses `Model.Parent` and `Model.Children` properties but they always return null/empty. Cards with enchantments or equipment don't announce their attached state.

**Affected Files:**
- `src/Core/Services/CardModelProvider.cs` (lines 1480-1896)
- `src/Core/Services/BattlefieldNavigator.cs` (line 530)
- `src/Core/Services/ZoneNavigator.cs` (line 714)
- `src/Core/Services/DuelAnnouncer.cs` (lines 1464-1501)

## Key Discovery: UniversalBattlefieldStack

The game does NOT track attachments via `Model.Parent`/`Children`. Instead, it uses a **stack-based system**.

### Class: `Wotc.Mtga.DuelScene.Universal.UniversalBattlefieldStack`

Found in analysis_core_menus.txt:22885-22904

**Properties:**
| Property | Type | Purpose |
|----------|------|---------|
| `AllCards` | Collection | All cards in the stack (includes attachments) |
| `StackedCards` | Collection | Cards stacked together |
| `StackParent` | Object | The card this stack is attached TO |
| `StackParentModel` | Object | Model data of the parent card |
| `HasAttachmentOrExile` | Boolean | Flag indicating attachments exist |
| `AttachmentCount` | Int32 | Number of attachments |
| `ExileCount` | Int32 | Number of exiled cards |
| `OldestCard` | Object | Oldest card in stack |
| `YoungestCard` | Object | Youngest card in stack |
| `IsAttackStack` | Boolean | Combat state |
| `IsBlockStack` | Boolean | Combat state |

**Methods:**
- `RefreshAbilitiesBasedOnStackPosition()` - Updates abilities based on stack order

### Interface: `IBattlefieldStack`

Found in analysis_core.txt:76-87

```
Properties:
  Boolean HasAttachmentOrExile
  UInt32 Age
  Boolean IsAttackStack
  Boolean IsBlockStack
  Int32 AttachmentCount
  Int32 ExileCount
Methods:
  Void RefreshAbilitiesBasedOnStackPosition()
```

## Supporting Evidence

### CardData Evaluators

Found in analysis_input.txt:1583-1593:
- `CardData_IsAttached` - Boolean evaluator for "is this card attached to something"
- `CardData_IsAttachedToPlayer` - Boolean for cards attached to players (curses)

### RelativeSpace Enum

Found in analysis_input.txt:11743:
- `Target_AttachedTo` - Relative space value for "the card this is attached to"
- `Source_Parent` - Parent reference in effect targeting

These confirm the game tracks attachment relationships internally.

## Implementation Plan

### Step 1: Discovery Logging

Add logging to discover how to access the battlefield stack from a card:

```csharp
// In CardModelProvider.cs, add to GetDuelSceneCDC or a new method:
public static void LogCDCProperties(Component cdc)
{
    if (cdc == null) return;

    var cdcType = cdc.GetType();
    MelonLogger.Msg($"=== CDC Type: {cdcType.FullName} ===");

    foreach (var prop in cdcType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
        try
        {
            var value = prop.GetValue(cdc);
            string typeName = value?.GetType().Name ?? "null";

            // Look for stack-related properties
            if (prop.Name.Contains("Stack") ||
                prop.Name.Contains("Battlefield") ||
                prop.Name.Contains("Attach") ||
                typeName.Contains("Stack"))
            {
                MelonLogger.Msg($"[STACK?] {prop.Name}: {typeName}");
            }
        }
        catch { }
    }
}
```

### Step 2: Access the Stack

Once the property name is found, access it:

```csharp
public static object GetBattlefieldStack(GameObject card)
{
    var cdc = GetDuelSceneCDC(card);
    if (cdc == null) return null;

    var cdcType = cdc.GetType();

    // Try likely property names (update after discovery)
    string[] possibleNames = {
        "BattlefieldStack", "Stack", "CardStack",
        "StackInfo", "BFStack", "UniversalStack"
    };

    foreach (var name in possibleNames)
    {
        var prop = cdcType.GetProperty(name);
        if (prop != null)
        {
            return prop.GetValue(cdc);
        }
    }

    return null;
}
```

### Step 3: Extract Attachment Info

```csharp
public static List<string> GetAttachmentNames(object battlefieldStack)
{
    var names = new List<string>();
    if (battlefieldStack == null) return names;

    var stackType = battlefieldStack.GetType();

    // Check if there are attachments
    var countProp = stackType.GetProperty("AttachmentCount");
    int count = (int)(countProp?.GetValue(battlefieldStack) ?? 0);
    if (count == 0) return names;

    // Get the stacked cards
    var stackedProp = stackType.GetProperty("StackedCards")
                   ?? stackType.GetProperty("AllCards");
    var stackedCards = stackedProp?.GetValue(battlefieldStack) as IEnumerable;

    if (stackedCards != null)
    {
        foreach (var card in stackedCards)
        {
            // Extract GrpId and look up name
            uint grpId = GetNestedPropertyValue<uint>(card, "GrpId");
            if (grpId > 0)
            {
                string name = GetNameFromGrpId(grpId);
                if (!string.IsNullOrEmpty(name))
                    names.Add(name);
            }
        }
    }

    return names;
}
```

### Step 4: Get Parent Card (What This Is Attached To)

```csharp
public static string GetAttachedToFromStack(object battlefieldStack)
{
    if (battlefieldStack == null) return null;

    var stackType = battlefieldStack.GetType();

    // Get StackParent or StackParentModel
    var parentProp = stackType.GetProperty("StackParent");
    var parent = parentProp?.GetValue(battlefieldStack);

    if (parent != null)
    {
        uint grpId = GetNestedPropertyValue<uint>(parent, "GrpId");
        if (grpId > 0)
        {
            return GetNameFromGrpId(grpId);
        }
    }

    return null;
}
```

## Alternative Approaches (If Stack Access Fails)

### Approach 2: CardHolder Level

Query the battlefield card holder for relationships between cards.

### Approach 3: Game State Manager

Use `IGameStateManager` or `IGameStateProvider` to query card relationships.

### Approach 4: UXEvent Monitoring

Monitor `ZoneTransferGroup` events for attachment information when cards move.

## Files to Modify

1. **CardModelProvider.cs** - Update `GetAttachments()` and `GetAttachedTo()` methods
2. **BattlefieldNavigator.cs** - Already calls `GetAttachmentText()`, will work once fixed
3. **ZoneNavigator.cs** - Already calls `GetAttachmentText()`, will work once fixed
4. **DuelAnnouncer.cs** - Update `GetAttachedToName()` to use stack system

## Testing Plan

1. Start a duel with a deck containing auras (e.g., Pacifism, All That Glitters)
2. Cast an aura on a creature
3. Navigate to the enchanted creature with B key
4. Verify announcement includes "enchanted by [aura name]"
5. Navigate to the aura itself
6. Verify announcement includes "attached to [creature name]"

## References

- `analysis_core.txt:76-87` - IBattlefieldStack interface
- `analysis_core_menus.txt:22885-22904` - UniversalBattlefieldStack class
- `analysis_input.txt:1583-1593` - CardData_IsAttached evaluators
- `analysis_input.txt:11743` - Target_AttachedTo RelativeSpace
