---
name: mtga-reflection
description: Patterns for safely accessing MTGA game types via reflection in MelonLoader mods. Covers ILSpy decompilation, field vs property lookup, rich text stripping, and navigator structure.
---

# MTGA Reflection Skill

Use these patterns when investigating or implementing features that read MTGA game state.

## Step 1: Decompile Before Coding

Always decompile the exact game type before writing reflection code.

```powershell
powershell -NoProfile -File tools\decompile.ps1 "TypeName"
```

Check `llm-docs/decompiled/` first — the type may already be there.
Check `llm-docs/type-index.md` for the full namespace and which DLL contains the type.

## Step 2: Field vs Property

MTGA mixes fields and properties. Confirm in the decompiled source:
- Use `GetField("_name", flags)` for fields (usually `private`, prefixed `_`)
- Use `GetProperty("Name", flags)` for properties (usually `public`)
- Never guess — check the decompiled source

## Step 3: Interface Properties

When a field holds an interface type (e.g. `IClientAchievement`), get the property from the **interface type**, not the concrete type:

```csharp
var iface = FindType("IClientAchievement");
var titleProp = iface?.GetProperty("Title", BindingFlags.Public | BindingFlags.Instance);
string title = titleProp?.GetValue(achievementData) as string;
```

## Step 4: Strip Rich Text Markup

MTGA strings frequently contain TextMeshPro rich text tags. Always strip before announcing:

```csharp
private static readonly System.Text.RegularExpressions.Regex RichTextPattern =
    new System.Text.RegularExpressions.Regex(@"<[^>]+>", System.Text.RegularExpressions.RegexOptions.Compiled);

public static string StripRichText(string s) =>
    s == null ? null : RichTextPattern.Replace(s, "").Trim();
```

Common tags in MTGA: `<sprite=...>`, `<color=...>`, `<b>`, `<i>`, `<size=...>`, `<alpha=...>`.

## Step 5: FindObjectsOfType is Expensive

Avoid `FindObjectsOfType<T>()` in hot paths (every frame). Use it only during `DiscoverElements()` (called once on activation), then cache results.

For typed MonoBehaviours found by reflection type (not compile-time type):

```csharp
foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
{
    if (mb.GetType() == _cachedType) { /* use it */ }
}
```

Cache `_cachedType` — never call `FindType()` per frame.

## Step 6: Navigator Structure

All navigators extend `BaseNavigator`. Required overrides:
- `NavigatorId` — unique string ID
- `ScreenName` — localised name (via `Strings.cs`)
- `Priority` — higher = checked first
- `DetectScreen()` — return true when this navigator should own the screen
- `DiscoverElements()` — call `AddElement(go, label)` for each navigable item

Optional but common:
- `HandleCustomInput()` — return true to consume a key press before base handles it
- `GetElementAnnouncement(int index)` — custom per-element text
- `GetActivationAnnouncement()` — text spoken when navigator activates
- `OnDeactivating()` — clean up cached state
- `Update()` — if you need per-frame checks, always call `base.Update()` last

## Step 7: Announce Only What Sighted Users See

Do not invent or infer state. Only announce:
- Text visible in the UI
- Data the game has already computed (via reflection from game objects)
- Status flags from game interfaces (IsCompleted, IsClaimed, etc.)

## Step 8: Rich Text in Group/Achievement Titles

MTGA group titles often contain mana symbol sprites like `<sprite="SpriteSheet_ManaIcons" name="xG">`.
Always run `StripRichText()` on any string from the game before announcing.

## Common Utilities

- `UIActivator.Activate(go)` — click/activate a GameObject
- `CardDetector.IsCard(go)` — check if a GameObject is a card
- `UITextExtractor.GetText(go)` — extract visible text from a UI element
- `ReflectionUtils.FindType(name)` — find a type by short name across all assemblies
- `ReflectionUtils.AllInstanceFlags` — `BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance`

## Navigator Priority Reference

Settings(90) > AdvancedFilters(87) > RewardPopup(86) > BoosterOpen(80) > Draft(78) > NPEReward(75) > Loading(65) > Mastery(60) > Achievements(57) > Store(55) > Codex(50) > GeneralMenu(15) > AssetPrep(low)
