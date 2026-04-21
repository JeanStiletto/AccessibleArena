# GeneralMenuNavigator.Mail.cs
Path: src/Core/Services/GeneralMenuNavigator/GeneralMenuNavigator.Mail.cs
Lines: 299

## Top-level comments
- Partial class hosting mailbox navigation: detail-view tracking, mail content fields injected as navigable elements, close-mailbox / close-letter via reflection, and mail-content button filtering.

## public partial class GeneralMenuNavigator (line 22)
### Fields
- private bool _isInMailDetailView (line 25)
- private Guid _currentMailLetterId (line 26)
- private UITextExtractor.MailContentParts _mailContentParts (line 29)

### Methods
- private void OnMailLetterSelected(Guid letterId, string title, string body, bool hasAttachments, bool isClaimed) (line 34) — Note: handler for PanelStatePatch.OnMailLetterSelected; marks detail view and triggers rescan so content fields are added
- private void AddMailContentFieldsAsElements() (line 53) — Note: inserts Title/Date/Body TMP GameObjects at the front of _elements
- private void ResetMailFieldNavigation() (line 104)
- private bool CloseMailbox() (line 114) — Note: if in detail view delegates to CloseMailDetailView; otherwise invokes NavBarController.HideInboxIfActive() via reflection, falls back to close button
- private bool CloseMailDetailView() (line 175) — Note: invokes ContentControllerPlayerInbox.CloseCurrentLetter() via reflection
- private static bool IsInsideMailboxPanel(GameObject obj) (line 223)
- private bool IsMailContentButtonWithNoText(GameObject obj) (line 242) — Note: filters out mail-content buttons like SecondaryButton_v2 that expose only their GameObject name as text
