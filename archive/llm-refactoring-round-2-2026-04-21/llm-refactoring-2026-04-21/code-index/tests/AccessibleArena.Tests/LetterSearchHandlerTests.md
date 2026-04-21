# LetterSearchHandlerTests.cs
Path: tests/AccessibleArena.Tests/LetterSearchHandlerTests.cs
Lines: 139

## public class LetterSearchHandlerTests (line 9)
### Fields
- private LetterSearchHandler _handler (line 11)
- private static readonly IReadOnlyList<string> Fruits (line 20)
### Methods
- public void SetUp() (line 14)
- public void SingleLetter_FindsFirstMatch_CaseInsensitive() (line 26)
- public void SingleLetter_NoMatch_ReturnsMinus1() (line 33)
- public void SameLetter_Repeated_CyclesToNextMatch() (line 40)
- public void TwoLetters_WithinTimeout_BuildsPrefix() (line 52)
- public void BufferTimeout_ResetsToSingleChar() (line 64)
- public void BufferPreserved_WithinTimeout() (line 78)
- public void Clear_ResetsBuffer() (line 88)
- public void EmptyList_ReturnsMinus1_NoException() (line 100)
- public void WrapAround_FindsMatchPastEndOfList() (line 111)
- public void SameLetter_CycleWrapsAroundList() (line 120)
- public void NullItemInList_Skipped_ReturnsNextNonNullMatch() (line 131)
