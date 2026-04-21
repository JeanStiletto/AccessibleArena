# DebugConfigTests.cs
Path: tests/AccessibleArena.Tests/DebugConfigTests.cs
Lines: 117

## public class DebugConfigTests (line 7)
### Methods
- public void SetUp() (line 10)
- public void Log_WhenEnabled_AppearsInRecentEntries() (line 13)
- public void Log_WhenDisabled_DoesNotAppear() (line 22)
- public void LogIf_CategoryFalse_DoesNotAppear() (line 30)
- public void LogIf_CategoryTrue_AppearsInEntries() (line 37)
- public void RingBuffer_Wraps_ReturnsLastN() (line 45)
- public void GetRecentEntries_EmptyBuffer_ReturnsEmptyArray() (line 58)
- public void GetRecentEntries_FewerThanRequested_ReturnsAll() (line 65)
- public void DisableAll_SubsequentLogIsNoOp() (line 73)
- public void EnableAll_SetsAllCategoryFlagsTrue() (line 81)
- public void EntryFormat_IsTagBracketedPlusMessage() (line 96)
- public void GetRecentEntries_ReturnsOldestFirst() (line 104)
