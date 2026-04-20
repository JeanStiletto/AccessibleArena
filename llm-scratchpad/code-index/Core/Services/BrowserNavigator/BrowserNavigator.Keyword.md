# BrowserNavigator.Keyword.cs
Path: src/Core/Services/BrowserNavigator/BrowserNavigator.Keyword.cs
Lines: 564

## Top-level comments
- Feature partial for the KeywordSelection browser (creature type picker). Interacts with the game's ChoiceFilter (formerly KeywordFilter) MonoBehaviour via reflection to enumerate, toggle, and navigate keyword choices. Also handles buffered A-Z letter jumping during the show-all phase.

## public partial class BrowserNavigator (line 16)
### Fields
- private bool _isKeywordSelection (line 19)
- private MonoBehaviour _keywordFilterRef (line 20)
- private int _currentKeywordIndex (line 21)
- private readonly LetterSearchHandler _keywordLetterSearch (line 23) — menu-style buffered A-Z jump handler
- private static Type _keywordFilterType (line 26, static) — ChoiceFilter type cache
- private static FieldInfo _kf_filteredKeywords (line 27, static)
- private static FieldInfo _kf_selectedKeywords (line 28, static)
- private static FieldInfo _kf_filterInput (line 29, static)
- private static FieldInfo _kf_showAllField (line 30, static)
- private static FieldInfo _keyword_DisplayText (line 31, static)
- private static FieldInfo _keyword_SearchText (line 32, static)
- private static MethodInfo _kf_onFilterSubmitted (line 33, static)
- private static bool _keywordReflectionInit (line 34, static)

### Methods
- private static void InitKeywordReflection() (line 39) — one-time static init: finds ChoiceFilter type (was KeywordFilter), caches filtered/selected/filterInput/showAll fields, nested Choice.DisplayText/SearchText fields, OnFilterSubmitted method
- private void CacheKeywordFilterState() (line 81) — finds ChoiceFilter MonoBehaviour in scaffold (scene fallback); calls DeactivateKeywordInputField; deactivates CardInfoNavigator
- private void DeactivateKeywordInputField() (line 140) — reads FilterInput from ChoiceFilter, calls DeactivateInputField() on TMP_InputField
- private bool IsKeywordShowAllActive() (line 166) — reads _showAllChoices bool on ChoiceFilter
- private int GetKeywordCount() (line 183) — reads _filteredChoices list count
- private string GetKeywordDisplayText(int index) (line 193) — reads Choice.DisplayText at index from _filteredChoices
- private bool IsKeywordSelected(int index) (line 208) — checks if _filteredChoices[index] is in _selectedChoices
- private void ToggleCurrentKeyword() (line 228) — temporarily sets filter text to keyword SearchText, invokes OnFilterSubmitted to toggle, restores original filter, re-deactivates input, announces new state
- private void AnnounceCurrentKeyword() (line 289) — reads DisplayText, selection state, position
- private bool HandleKeywordSelectionInput() (line 310) — Tab/Left/Right navigation across keywords+buttons; Home/End; Up/Down consumed; Enter→ToggleCurrentKeyword or ActivateCurrentButton; Space→ClickConfirmButton; Backspace→ClickCancelButton; delegates A-Z to HandleKeywordLetterJump in show-all phase
- private bool HandleKeywordLetterJump() (line 528) — buffered A-Z first-letter jump over keyword list; consumes any A-Z key in show-all phase regardless of match (blocks zone hotkeys)
