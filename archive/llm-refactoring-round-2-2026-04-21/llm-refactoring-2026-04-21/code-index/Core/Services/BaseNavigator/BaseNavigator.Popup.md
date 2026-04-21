# BaseNavigator.Popup.cs
Path: src/Core/Services/BaseNavigator/BaseNavigator.Popup.cs
Lines: 1438

## Top-level comments
- Popup mode handling: saves/restores underlying elements, detects popups via PanelStateManager, discovers popup-specific UI, handles popup input and dismissal.
- Implements 4-level dismiss chain (cancel button → dismiss overlay → SystemMessageView.OnBack → SetActive(false)), element refresh on chained popups, stepper detection via reflection.

## public partial class BaseNavigator (line 20)
### Fields
- private bool _isInPopupMode (line 24)
- private GameObject _popupGameObject (line 25)
- private List<NavigableElement> _savedElements (line 26)
- private int _savedIndex (line 27)
- private InputFieldEditHelper _popupInputHelper (line 28)
- private DropdownEditHelper _popupDropdownHelper (line 29)

### Properties
- protected bool IsInPopupMode (line 36)
- protected GameObject PopupGameObject (line 39)

### Methods
- protected void EnablePopupDetection() (line 45) — subscribe to PanelStateManager
- protected void DisablePopupDetection() (line 64)
- private void OnPopupPanelChanged(PanelInfo oldPanel, PanelInfo newPanel) (line 73)
- public static bool IsPopupPanel(PanelInfo panel) (line 97)
- protected void EnterPopupMode(GameObject popup) (line 112) — discover elements, auto-focus actionable
- protected void ExitPopupMode() (line 160)
- private void ClearPopupModeState() (line 171) — restore elements, refresh labels
- private bool ValidatePopup() (line 216) — detect destroyed GOs, re-discover on chained popups
- protected void DismissPopup() (line 254) — 4-level: cancel → dismiss → OnBack → SetActive
- private void HandlePopupInput() (line 325) — routes to dropdown/input helpers
- private void NavigatePopupItem(int direction) (line 403)
- private void NavigatePopupToIndex(int index) (line 426)
- private void ActivatePopupItem() (line 437) — handles TextBlock, input field, dropdown, button
- private void AnnouncePopupOpen() (line 474)
- private void AnnouncePopupCurrentItem() (line 502) — refresh dynamic labels
- protected virtual void DiscoverPopupElements(GameObject popup) (line 550) — master discovery (6 phases)
- private void DiscoverPopupTextBlocks(GameObject popup, bool hasDeckCosts, List<Transform> skipTransforms) (line 598)
- private void DiscoverPopupTitleTexts(GameObject popup) (line 649)
- private void DiscoverPopupInputFields(GameObject popup, HashSet<GameObject> addedObjects) (line 692)
- private void DiscoverPopupDropdowns(GameObject popup, HashSet<GameObject> addedObjects) (line 719)
- private void DiscoverPopupButtons(GameObject popup, HashSet<GameObject> addedObjects) (line 772)
- private void DeduplicateTextBlocksAgainstButtons() (line 855)
- private void DiscoverPopupSteppers(GameObject popup) (line 875) — detect craft stepper via reflection
- private string ExtractPopupTitle(GameObject popup) (line 1020)
- private static bool IsInsideTitleContainer(Transform child, Transform stopAt) (line 1036)
- private static bool IsInsideButton(Transform child, Transform stopAt) (line 1052)
- private static bool IsInsideDropdown(Transform child, Transform stopAt) (line 1074)
- private static bool IsInsideInputField(Transform child, Transform stopAt) (line 1086)
- private static bool IsInsideComponentByName(Transform child, Transform stopAt, string typeName) (line 1098)
- private static bool HasComponentInChildren(GameObject go, string typeName) (line 1113)
- private static bool IsDismissOverlay(GameObject obj) (line 1124)
- private static GameObject FindDismissOverlay(GameObject popup) (line 1137)
- private static bool IsChildOfAny(Transform child, List<Transform> parents) (line 1163)
- private static void CollectWidgetContentTransforms(GameObject popup, string componentTypeName, string fieldName, List<Transform> skipTransforms) (line 1173)
- private GameObject FindPopupCancelButton(GameObject popup) (line 1201) — 4-pass discovery
- private static bool TryCloseFriendInvitePanel(GameObject popup) (line 1255)
- protected bool TryInvokePopupButtonByFieldName(string fieldName) (line 1276)
- private bool TryInvokeCustomButtonOnClick(GameObject buttonObj) (line 1305) — invoke via reflection
- private static bool MatchesCancelPattern(GameObject obj, string[] patterns) (line 1349)
- private static bool ContainsCancelWord(string text, string word) (line 1362)
- private MonoBehaviour FindSystemMessageViewInPopup(GameObject popup) (line 1376)
- private bool TryInvokeOnBack(MonoBehaviour component) (line 1409)
