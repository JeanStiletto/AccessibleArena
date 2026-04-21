# UITextExtractor.Social.cs
Path: src/Core/Services/UITextExtractor/UITextExtractor.Social.cs
Lines: 343

## Top-level comments
- Feature partial for social/mailbox text extraction: FriendsWidget button labels, mailbox list-item titles (skipping "Neu"/"New" badges), and structured/flat content extraction from opened mail messages.

## public struct MailContentParts (line 11)

### Fields
- public string Title (line 13)
- public string Date (line 14)
- public string Body (line 15)
- public GameObject TitleObject (line 16)
- public GameObject DateObject (line 17)
- public GameObject BodyObject (line 18)

### Properties
- public bool HasContent (line 19) — true if any of Title/Date/Body is non-empty

## public static partial class UITextExtractor (line 6)

### Fields
(no fields declared in this partial)

### Methods
- private static string TryGetFriendsWidgetLabel(GameObject gameObject) (line 27) — walks up to 10 levels to confirm FriendsWidget context; maps AddFriend/Challenge to loc keys; strips "Button_"/"_Button" from parent names
- private static string TryGetMailboxItemTitle(GameObject gameObject) (line 98) — walks up to Mailbox_Blade_ListItem container, picks longest active TMP_Text while skipping "Neu"/"New"/"gelesen" badges; prepends unread badge
- public static MailContentParts GetMailContentParts() (line 165) — finds ContentController - Mailbox_Base(Clone), locates Mailbox_Letter_Base / CONTENT_Mailbox_Letter, classifies TMP_Text by object/parent name (title/date/body) with size-based fallback
- public static string GetMailContentText() (line 266) — flat-text variant joining non-duplicate non-button content with ". " separator
