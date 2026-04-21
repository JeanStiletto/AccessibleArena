# ReflectionUtils.cs
Path: src/Core/Utils/ReflectionUtils.cs
Lines: 52

## Top-level comments
- Shared reflection constants and helpers. Intended for `using static` import.

## static class ReflectionUtils (line 9)
### Fields
- public const BindingFlags PrivateInstance (line 12)
- public const BindingFlags PublicInstance (line 16)
- public const BindingFlags AllInstanceFlags (line 20)
### Methods
- public static Type FindType(string fullName) (line 27) — Note: searches all loaded assemblies; falls back to name-only match if full-name lookup fails; swallows assembly load exceptions silently
