# Game Type Index

Maps commonly-referenced game types to their full namespace and DLL location.
Used by `tools/decompile.ps1` and `tools/decompile-all.ps1`.

**DLL abbreviations:**
- `Core` = `Core.dll`
- `Asm` = `Assembly-CSharp.dll`
- `Gre` = `Wizards.MDN.GreProtobuf.dll`
- `Shared` = `SharedClientCore.dll`

**DLL location:** `C:\Program Files\Wizards of the Coast\MTGA\MTGA_Data\Managed\`

## Store Types

| Short Name | Full Namespace | DLL |
|---|---|---|
| ContentController_StoreCarousel | ContentController_StoreCarousel | Core |
| StoreItemBase | StoreItemBase | Core |
| StoreConfirmationModal | StoreConfirmationModal | Core |
| StoreItemDisplay | StoreItemDisplay | Core |
| StoreDisplayPreconDeck | Core.Meta.MainNavigation.Store.StoreDisplayPreconDeck | Core |
| StoreDisplayCardViewBundle | StoreDisplayCardViewBundle | Core |
| StoreSetFilterToggles | Core.Meta.MainNavigation.Store.StoreSetFilterToggles | Core |
| StoreSetFilterModel | Core.Meta.MainNavigation.Store.Data.StoreSetFilterModel | Core |
| StoreSetFilterToggle | Core.Meta.MainNavigation.Store.StoreSetFilterToggle | Core |
| StoreSetFilterDropdown | StoreSetFilterDropdown | Core |
| StoreSetFilterDropdownItem | StoreSetFilterDropdownItem | Core |
| StoreSetUtils | Core.Meta.MainNavigation.Store.Utils.StoreSetUtils | Core |
| CardDataForTile | Wizards.MDN.Store.CardDataForTile | Gre |

## Card Data & Display Types

| Short Name | Full Namespace | DLL |
|---|---|---|
| CardData | GreClient.CardData.CardData | Gre |
| CardPrintingData | GreClient.CardData.CardPrintingData | Shared |
| AbilityPrintingData | GreClient.CardData.AbilityPrintingData | Shared |
| AbilityPrintingRecord | GreClient.CardData.AbilityPrintingRecord | Shared |
| DynamicAbilityPrintingData | GreClient.CardData.DynamicAbilityPrintingData | Shared |
| AbilityTextProvider | Wotc.Mtga.Cards.Database.AbilityTextProvider | Shared |
| CardNameTextProvider | Wotc.Mtga.Cards.Database.CardNameTextProvider | Shared |
| DynamicAbilityDataProvider | Wotc.Mtga.Cards.Database.DynamicAbilityDataProvider | Shared |
| CardDatabase | Wotc.Mtga.Cards.Database.CardDatabase | Shared |
| MtgCardInstance | (runtime only, not easily decompiled) | Core |
| DuelScene_CDC | DuelScene_CDC | Core |
| BASE_CDC | BASE_CDC | Core |
| CDCPart_Highlights | CDCPart_Highlights | Core |
| HighlightType (enum) | HighlightType | Core |
| MetaCardView | MetaCardView | Core |
| PagesMetaCardView | PagesMetaCardView | Core |
| BoosterMetaCardView | BoosterMetaCardView | Core |
| DraftPackCardView | DraftPackCardView | Asm |
| CardView | CardView | Asm |
| DuelCardView | DuelCardView | Core |
| RewardDisplayCard | RewardDisplayCard | Core |
| CardRolloverZoomHandler | CardRolloverZoomHandler | Core |
| StaticColumnMetaCardHolder | StaticColumnMetaCardHolder | Core |
| StaticColumnMetaCardView | StaticColumnMetaCardView | Core |
| CDCViewMetadata | Wotc.Mtga.CardParts.CDCViewMetadata | Core |
| CardHolderType | Wotc.Mtga.CardParts.CardHolderType | Core |

## Card Holders & Browsers

| Short Name | Full Namespace | DLL |
|---|---|---|
| CardPoolHolder | CardPoolHolder | Core |
| ScrollCardPoolHolder | ScrollCardPoolHolder | Core |
| CardBrowserCardHolder | CardBrowserCardHolder | Core |
| ListMetaCardHolder | ListMetaCardHolder | Core |
| UniversalBattlefieldStack | UniversalBattlefieldStack | Core |
| CardBrowserBase | Wotc.Mtga.DuelScene.Browsers.CardBrowserBase | Core |
| SelectCardsBrowser | Wotc.Mtga.DuelScene.Browsers.SelectCardsBrowser | Core |
| SelectCardsWorkflow\<T\> | Wotc.Mtga.DuelScene.Interactions.SelectN.SelectCardsWorkflow\<T\> | Core |

## UI Components

| Short Name | Full Namespace | DLL |
|---|---|---|
| CustomButton | CustomButton | Core |
| CustomButtonWithTooltip | CustomButtonWithTooltip | Core |
| SystemMessageButtonView | SystemMessageButtonView | Asm |
| cTMP_Dropdown | cTMP_Dropdown | Asm |
| TooltipTrigger | TooltipTrigger | Asm |
| MainButton | MainButton | Asm |
| Spinner_OptionSelector | Spinner_OptionSelector | Core |

## Navigation & Controllers

| Short Name | Full Namespace | DLL |
|---|---|---|
| NavContentController | NavContentController | Core |
| NavBarController | NavBarController | Core |
| HomePageContentController | HomePageContentController | Core |
| EventPageContentController | EventPageContentController | Core |
| PacketSelectContentController | PacketSelectContentController | Core |
| CampaignGraphContentController | CampaignGraphContentController | Core |
| LearnToPlayControllerV2 | LearnToPlayControllerV2 | Core |
| DeckManagerController | DeckManagerController | Core |
| PlayBladeController | PlayBladeController | Core |
| DeckSelectBlade | DeckSelectBlade | Core |
| SettingsMenu | Wotc.Mtga.Wrapper.SettingsMenu | Core |
| GameManager | GameManager | Core |
| MatchManager | MatchManager (nested PlayerInfo) | Core |
| MatchTimer | MatchTimer | Core |
| LowTimeWarning | LowTimeWarning | Core |
| PlayerTimeoutDisplay | PlayerTimeoutDisplay | Core |
| TimeoutNotificationDisplay | TimeoutNotificationDisplay | Core |
| MtgTimer | GreClient.Rules.MtgTimer | Shared |
| TimeoutNotification | GreClient.Rules.TimeoutNotification | Shared |
| TimerUpdate | GreClient.Rules.TimerUpdate | Shared |
| ClientUpdateBase | GreClient.Rules.ClientUpdateBase | Shared |
| TimerType (enum) | Wotc.Mtgo.Gre.External.Messaging.TimerType | Gre |
| TimerBehavior (enum) | Wotc.Mtgo.Gre.External.Messaging.TimerBehavior | Gre |
| MatchWinCondition (enum) | Wotc.Mtgo.Gre.External.Messaging.MatchWinCondition | Gre |
| WrapperController | WrapperController | Core |

## Play Blade & Events

| Short Name | Full Namespace | DLL |
|---|---|---|
| BladeContentView | Wizards.Mtga.PlayBlade.BladeContentView | Core |
| EventBladeContentView | Wizards.Mtga.PlayBlade.EventBladeContentView | Core |
| LastPlayedBladeContentView | LastPlayedBladeContentView | Core |

## Deck Builder

| Short Name | Full Namespace | DLL |
|---|---|---|
| DeckBuilderModelProvider | Core.Code.Decks.DeckBuilderModelProvider | Core |
| DeckBuilderActionsHandler | Core.Code.Decks.DeckBuilderActionsHandler | Core |
| DeckMainTitlePanel | DeckMainTitlePanel | Core |
| DeckCostsDetails | DeckCostsDetails | Core |
| DeckTypesDetails | DeckTypesDetails | Core |

## Social / Friends

| Short Name | Full Namespace | DLL |
|---|---|---|
| SocialUI | SocialUI | Core |
| FriendsWidget | FriendsWidget | Core |
| SocialEntityListHeader | SocialEntityListHeader | Core |
| FriendTile | FriendTile | Asm |
| InviteOutgoingTile | InviteOutgoingTile | Asm |
| InviteIncomingTile | InviteIncomingTile | Asm |
| BlockTile | BlockTile | Asm |
| ChatWindow | ChatWindow | Core |
| ChatManager | MTGA.Social.ChatManager | Core |
| SocialMessagesView | SocialMessagesView | Core |
| MessageTile | MessageTile | Core |
| Conversation | MTGA.Social.Conversation | Core |
| SocialMessage | MTGA.Social.SocialMessage | Core |
| SocialEntity | MTGA.Social.SocialEntity | Core |
| ISocialManager | MTGA.Social.ISocialManager | Core |
| Direction | MTGA.Social.Direction | Core |
| MessageType | MTGA.Social.MessageType | Core |
| MessageSeen | MTGA.Social.MessageSeen | Core |
| PresenceStatus | MTGA.Social.PresenceStatus | Core |

## Challenge / Private Game

| Short Name | Full Namespace | DLL |
|---|---|---|
| UnifiedChallengeDisplay | UnifiedChallengeDisplay | Core |
| ChallengePlayerDisplay | Wizards.Mtga.PrivateGame.ChallengePlayerDisplay | Core |
| UnifiedChallengeBladeWidget | UnifiedChallengeBladeWidget | Core |

## Duel Scene

| Short Name | Full Namespace | DLL |
|---|---|---|
| UXEventQueue | Wotc.Mtga.DuelScene.UXEvents.UXEventQueue | Core |
| UXEvent | Wotc.Mtga.DuelScene.UXEvents.UXEvent | Core |
| ButtonPhaseLadder | ButtonPhaseLadder | Core |
| ManaColorSelector | ManaColorSelector | Core |
| View_ChooseXInterface | View_ChooseXInterface | Core |
| DuelSceneBrowserType | DuelSceneBrowserType (enum) | Core |
| NumericInputVisualState | Wotc.Mtga.DuelScene.Interactions.NumericInputVisualState (enum) | Core |
| NumericInputType | Wotc.Mtgo.Gre.External.Messaging.NumericInputType (enum) | Gre |
| NumericInputReq | Wotc.Mtgo.Gre.External.Messaging.NumericInputReq | Gre |
| PresetManaWheel | Wotc.Mtga.DuelScene.UI.PresetManaWheel | Core |
| Spinner_OptionSelector | Spinner_OptionSelector | Core |

## NPE Tutorial (Duel)

| Short Name | Full Namespace | DLL |
|---|---|---|
| NPEController | NPEController | Core |
| NPE_Game (abstract) | NPE_Game | Core |
| F1_NPEGame1_Combined | F1_NPEGame1_Combined | Core |
| NPEDirector | NPEDirector | Core |
| NPEReminder | NPEReminder | Core |
| NPEDismissableDeluxeTooltipUXEvent | Wotc.Mtga.DuelScene.UXEvents.NPEDismissableDeluxeTooltipUXEvent | Core |
| NPEDialogUXEvent | Wotc.Mtga.DuelScene.UXEvents.NPEDialogUXEvent | Core |
| NPEReminderUXEvent | Wotc.Mtga.DuelScene.UXEvents.NPEReminderUXEvent | Core |
| NPEPauseUXEvent | Wotc.Mtga.DuelScene.UXEvents.NPEPauseUXEvent | Core |
| NPEShowBattlefieldHangerUXEvent | Wotc.Mtga.DuelScene.UXEvents.NPEShowBattlefieldHangerUXEvent | Core |
| NPEWarningUXEvent | Wotc.Mtga.DuelScene.UXEvents.NPEWarningUXEvent | Core |
| NPEUXEventWithDuration | Wotc.Mtga.DuelScene.UXEvents.NPEUXEventWithDuration | Core |
| DeluxeTooltipType (enum) | DeluxeTooltipType | Core |
| DeluxeTooltip | DeluxeTooltip | Core |
| DismissableDeluxeTooltip | DismissableDeluxeTooltip | Core |
| ManaDeluxeTooltipController | ManaDeluxeTooltipController | Core |
| CombatDeluxeTooltipController | CombatDeluxeTooltipController | Core |
| ElfDeluxeTooltipController | ElfDeluxeTooltipController | Core |
| NPEContentControllerRewards | NPEContentControllerRewards | Core |
| NPEObjective | NPEObjective | Core |

## Home Page / Objectives

| Short Name | Full Namespace | DLL |
|---|---|---|
| HomePageBillboard | HomePageBillboard | Core |
| ObjectiveBubble | ObjectiveBubble | Core |
| ContentControllerObjectives | ContentControllerObjectives | Core |
| NotificationPopup | NotificationPopup | Core |
| NotificationPopup.PopupData | NotificationPopup (nested class PopupData) | Core |
| MTGALocalizedString | MTGALocalizedString | Core |
| EBillboardType (enum) | EBillboardType | Core |
| EventTimerState (enum) | EventTimerState | Core |
| BillboardData | BillboardData | Core |

## Mastery / Rewards

| Short Name | Full Namespace | DLL |
|---|---|---|
| ProgressionTrackLevel | Core.MainNavigation.RewardTrack.ProgressionTrackLevel | Core |
| ClientTrackLevelInfo | Core.MainNavigation.RewardTrack.ClientTrackLevelInfo | Core |
| RewardDisplayData | RewardDisplayData | Core |
| ProgressionTracksContentController | ProgressionTracksContentController | Core |
| ContentController_PrizeWall | ContentController_PrizeWall | Core |

## Color Challenge (Campaign Graph)

| Short Name | Full Namespace | DLL |
|---|---|---|
| CampaignGraphContentController | CampaignGraphContentController | Core |
| CampaignGraphTrackModule | CampaignGraphTrackModule | Core |
| CampaignGraphObjectiveBubble | CampaignGraphObjectiveBubble | Core |
| IColorChallengeStrategy | (interface, from _strategy field on controller) | Core |
| Client_ColorChallengeMatchNode | (from strategy.CurrentTrack.Nodes) | Core |

## Login / Registration

| Short Name | Full Namespace | DLL |
|---|---|---|
| LoginScene | Wotc.Mtga.Login.LoginScene | Core |
| RegistrationPanel | Wotc.Mtga.Login.RegistrationPanel | Core |
| LoginPanel | Wotc.Mtga.Login.LoginPanel | Core |
| RegisterOrLoginPanel | Wotc.Mtga.Login.RegisterOrLoginPanel | Core |
| Panel (login base) | Wotc.Mtga.Login.Panel | Core |
| UIWidget_InputField_Registration | UIWidget_InputField_Registration | Core |
| LoginFlowAnalytics | (in Core.dll, has SendEvent_Registration) | Core |
| AccountError | (in Core.dll, has ErrorTypes enum: Email, Password, DisplayName, Token, Age) | Core |
| CountryCodes | (in Core.dll, has DataShareCountries dictionary) | Core |

## Codex / Learn to Play

| Short Name | Full Namespace | DLL |
|---|---|---|
| TableOfContentsSection | Core.MainNavigation.LearnToPlay.TableOfContentsSection | Core |
| LearnMoreSection | Core.MainNavigation.LearnToPlay.LearnMoreSection | Core |

## Mailbox

| Short Name | Full Namespace | DLL |
|---|---|---|
| ContentControllerPlayerInbox | Wotc.Mtga.Wrapper.Mailbox.ContentControllerPlayerInbox | Core |

## System / Popups

| Short Name | Full Namespace | DLL |
|---|---|---|
| PopupManager | Core.Meta.MainNavigation.PopUps.PopupManager | Core |
| SystemMessageManager | SystemMessageManager | Core |
| SystemMessageView | SystemMessageView | Core |

## Localization

| Short Name | Full Namespace | DLL |
|---|---|---|
| LocalizedString | Wotc.Mtga.Loc.LocalizedString | Core |
| Languages | Wotc.Mtga.Loc.Languages | Core |
| MTGALocalizedString | MTGALocalizedString | Core |

## GRE Protocol Enums

| Short Name | Full Namespace | DLL |
|---|---|---|
| EventContext | Wizards.MDN.EventContext | Gre |
| StopType | (check Gre) | Gre |
| SettingStatus | (check Gre) | Gre |

## Set Metadata

| Short Name | Full Namespace | DLL |
|---|---|---|
| ISetMetadataProvider | SharedClientCore.SharedClientCore.Code.Providers.ISetMetadataProvider | Shared |
| SetMetadataProvider | Core.Shared.Code.CardFilters.SetMetadataProvider | Core |
| SetMetadataCollection | (referenced by ISetMetadataProvider.LoadData, not yet decompiled) | Shared |
| ClientSetMetadata | Core.Code.Collations.ClientSetMetadata | Shared |
| ClientSetCollation | Core.Code.Collations.ClientSetCollation | Shared |
| CollationMapping | Wotc.Mtga.Wrapper.CollationMapping | Shared |
| CollationMappingExtensions | Wotc.Mtga.Wrapper.CollationMappingExtensions | Shared |

## Provider / Utility Types

| Short Name | Full Namespace | DLL |
|---|---|---|
| Pantry | Wizards.Mtga.Pantry | Core |
| ICardRolloverZoom | Wotc.Mtga.ICardRolloverZoom | Core |
| AbilityHangerBaseConfigProvider | Wotc.Mtga.Hangers.AbilityHangers.AbilityHangerBaseConfigProvider | Core |
| AbilityHangerBase | AbilityHangerBase | Core |

## Critical Field/Property Notes

Some types have members that are fields (not properties) - reflection with `GetProperty()` will fail:

- **MtgCardInstance**: `AttachedToId` (uint field), `IsTapped` (bool field), `HasSummoningSickness` (bool field)
- **EventContext**: `PlayerEvent` (IPlayerEvent field)
- **TooltipTrigger**: `TooltipData` (public field), but `TooltipData.Text` IS a property
- **StoreSetFilterModel**: `SetSymbol` (string field), `Availability` (field), `Sets` (List\<CollationMapping\>)
- **StoreSetFilterToggle**: UI is icon-only (Image `_symbol`), no text — set names must come from elsewhere
- **CollationMappingExtensions.GetName()**: just returns `.ToString()` (the 3-letter code), NOT a localized name
- **ClientSetCollation**: has `FlavorId` (string) — EMPTY at runtime, not populated by game data
- **StoreSetFilterDropdownItem**: only `Image _symbol` + `RawImage _logo` (no text) — set names are image textures
- **ClientSetMetadata**: has `SetCode` (string), no display name field
- **Set name localization**: Use `Languages.ActiveLocProvider.GetLocalizedText("General/Sets/" + setCode)` — see `docs/SET_NAME_LOCALIZATION.md`
- **cTMP_Dropdown**: extends `Selectable`, NOT `TMP_Dropdown` - use type name reflection
- **CampaignGraphContentController**: `_strategy` (private field) → IColorChallengeStrategy. Strategy has `CurrentTrack` property → track with `Name`, `Completed`, `UnlockedMatchNodeCount`, `Nodes` (list of Client_ColorChallengeMatchNode)
- **Client_ColorChallengeMatchNode**: `Id` (string field), `IsPvpMatch` (bool field), `DeckUpgradeData` (field, null if none), `Reward` (field → RewardDisplayData with `MainText`/`RewardText` fields)
- **CampaignGraphObjectiveBubble**: `ID` (public property), `_circleText` (private TMP field, roman numeral), `_animator` (private, use GetBool for "Locked"/"Completed"/"Selected"), `_notificationPopup` (private, has `_titleLabel`/`_descriptionLabel` Localize fields)
- **RegistrationPanel**: 5 serialized `UIWidget_InputField_Registration` fields (`displayName_inputField`, `email_inputField`, `email2_inputField`, `password_inputField`, `password2_inputField`), 5 serialized `Toggle` fields (`receiveOffers_Toggle`, `dataShare_Toggle`, `termsAndConditions_Toggle`, `codeOfConduct_Toggle`, `privacyPolicy_Toggle`), `submitButton` (GameObject), `generalError` (TMP), `_validDisplayName` (private bool, default false)
- **ObjectiveBubble**: `_popupData` (protected field, type `NotificationPopup.PopupData`) holds localization keys for popup text. Timer type sets `HeaderString1="MainNav/Quest/Quest_Wait_Text"`, `FooterString="MainNav/Popups/QuestRewardPopupDetailsForWaiting"`. Reward types set `HeaderString1="MainNav/EventRewards/Reward"`, `HeaderString2=reward.MainText` (localized reward description). Read via `_popupData` → field access on PopupData fields
- **NotificationPopup.PopupData**: Public fields: `HeaderString1`, `HeaderString2`, `DescriptionString`, `FooterString`, `ProgressString`, `RefreshButtonString` (all MTGALocalizedString). `ApplyData(NotificationPopup)` pushes data to the visual popup
- **MTGALocalizedString**: `Key` is a PUBLIC FIELD (not property!). `ToString()` resolves localization via `Languages.ActiveLocProvider.GetLocalizedText(Key, Parameters)`. Implicit string operators exist
- **HomePageBillboard**: `Title`, `Description`, `TimerText` (all TMP public fields), `locTitle`, `locDescription` (Localize). `SetEvent()` only sets `locTitle` — `Description`/`locDescription` are NEVER populated. Timer text set dynamically in `Update()` with loc keys `MainNav/HomePage/Billboards/EventStartTimer`, `SignUpEndTimer`, `EventEndTimer`
- **RegistrationPanel._checkFields()**: runs in `Update()` every frame, controls button enabled state. Required: `_validDisplayName==true`, displayname 3-23 chars, email non-empty, email1==email2, password>=8, password1==password2, password rules, `termsAndConditions_Toggle.isOn && codeOfConduct_Toggle.isOn && privacyPolicy_Toggle.isOn`. Offers and Data toggles are NOT required.
- **RegistrationPanel._validDisplayName**: only set to `true` by `Coroutine_ValidateUsername` (server call), triggered by `_displayName_endEdit`. Reset to `false` by `_displayName_select`. If displayname validation fails (taken, invalid), button stays permanently disabled.
- **RegistrationPanel.Show()**: Data toggle visibility depends on `CountryCodes.DataShareCountries.ContainsKey(selectedCountry)` — shown for EU/GDPR countries, hidden (auto-checked) otherwise
- **RegistrationPanel.OnButton_SubmitRegistration()**: does NOT check validation — directly calls `DoRegistration()`. Button disabling is done by `_checkFields()` + `EnableButton(false)`
- **BASE_CDC**: base class for all card display controllers. `CurrentHighlight()` is a METHOD (not property), returns `HighlightType` enum. `UpdateHighlight(HighlightType)` sets it. Uses `CDCPart_Highlights` internally via `_cachedHighlightPart`, falls back to `_mostRecentHighlight` field.
- **HighlightType (enum)**: None=0, Cold=1, Tepid=2, Hot=3, AutoPay=4, **Selected=5**, OpponentHover=8, Invalid=9, ColdMana=10, Hover=11. Gaps at 6,7. No localized text — just visual glow states.
- **SelectCardsBrowser** (extends CardBrowserBase): selection tracked via `currentSelections` list (List\<DuelScene_CDC\>) on workflow, NOT on CDC. But `GetBrowserHighlights()` sets `HighlightType.Selected` on selected CDCs, so `CurrentHighlight()` reliably reflects toggle state.
- **SelectCardsWorkflow\<T\>**: generic type, cannot be decompiled with ilspycmd 8.x. Maintains `selectable`, `currentSelections`, `nonSelectable` lists. Toggle logic in `CardBrowser_OnCardViewSelected`. `GetBrowserHighlights()` maps: selectable→Hot, nonSelectable→None, currentSelections→Selected.
- **MatchManager**: accessed via `GameManager.MatchManager` (public property). Has `LocalPlayerInfo` and `OpponentInfo` (public properties → nested `PlayerInfo` class). `PlayerInfo.CommanderGrpIds` (`IReadOnlyList<uint>`, property) — commander GrpIds, available from match start even for opponents. MTGA never fires zone transfer events for opponent commanders, so this is the only reliable source.
