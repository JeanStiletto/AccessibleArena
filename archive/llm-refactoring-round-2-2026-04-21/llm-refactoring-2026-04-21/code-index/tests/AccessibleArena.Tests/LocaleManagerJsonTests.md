# LocaleManagerJsonTests.cs
Path: tests/AccessibleArena.Tests/LocaleManagerJsonTests.cs
Lines: 93

## Top-level comments
- Tests for LocaleManager.ParseFlatJson, the hand-written JSON parser used for locale files.

## public class LocaleManagerJsonTests (line 11)
### Methods
- private static Dictionary<string, string> Parse(string json) (line 13)
- public void Parse_SimpleKeyValue_Succeeds() (line 21)
- public void Parse_MultipleKeys_AllPresent() (line 28)
- public void Parse_EscapedQuote_InValue() (line 37)
- public void Parse_EscapedNewline_InValue() (line 44)
- public void Parse_UnicodeEscape_InValue() (line 51)
- public void Parse_EmptyObject_YieldsEmptyDict() (line 58)
- public void Parse_EmptyStringValue_Succeeds() (line 65)
- public void Parse_MalformedJson_DoesNotThrow() (line 72)
- public void Parse_BackslashEscape_InValue() (line 79)
- public void Parse_TabEscape_InValue() (line 86)
