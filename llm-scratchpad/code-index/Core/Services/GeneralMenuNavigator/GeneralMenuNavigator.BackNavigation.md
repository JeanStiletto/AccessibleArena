# GeneralMenuNavigator.BackNavigation.cs
Path: src/Core/Services/GeneralMenuNavigator/GeneralMenuNavigator.BackNavigation.cs
Lines: 361

## Top-level comments
- Partial class hosting universal Backspace routing: overlay-aware dispatch, content panel / campaign graph / deck builder back handling, generic back button lookup, and PlayBlade/Challenge close.

## public partial class GeneralMenuNavigator (line 22)
### Methods
- private bool HandleBackNavigation() (line 28) — Note: dispatches on active overlay (Popup/Mailbox/PlayBlade/NPE/Settings/Friends) first, then falls back to ForegroundLayer (ContentPanel/Home/None)
- private bool HandleContentPanelBack() (line 77) — Note: CampaignGraph and WrapperDeckBuilder have dedicated handlers; otherwise searches for dismiss button inside controller GameObject, else navigates Home
- private bool HandleCampaignGraphBack() (line 124) — Note: two-level back; re-expands blade if collapsed, otherwise navigates Home
- private GameObject FindDismissButtonInPanel(GameObject panel) (line 149) — Note: matches only explicit Close/Dismiss/Back names; skips ModalFade (ambiguous)
- private bool TryGenericBackButton() (line 176)
- private bool HandleNPEBack() (line 195)
- private GameObject FindButtonByPredicate(GameObject panel, System.Func<string, bool> namePredicate, System.Func<GameObject, bool> customButtonFilter = null) (line 208)
- private GameObject FindCloseButtonInPanel(GameObject panel) (line 254)
- private GameObject FindGenericBackButton() (line 267) — Note: requires no visible text on CustomButtons and skips DismissButton
- private static bool IsBackButtonName(string name) (line 279)
- private bool HandlePlayBladeBackspace() (line 292)
- private bool ClosePlayBlade() (line 300) — Note: challenge screens click MainButton_Leave and request ChallengeMain re-entry (confirmation dialog handles cleanup); otherwise toggles Btn_BladeIsOpen or Blade_DismissButton
- private void ClearBladeStateAndRescan() (line 350)
