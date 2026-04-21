# llm-docs Validation Report

Generated: 2026-04-19
Validator: Claude (Sonnet 4.6)

---

## 1. architecture-overview.md — DRIFTED

Most structural claims are still valid but several details have gone stale.

### OK
- Entry point class `AccessibleArenaMod : MelonMod` — correct.
- Core services list (AnnouncementService, ShortcutRegistry, InputManager, UIFocusTracker, CardInfoNavigator, ModSettings, LocaleManager) — all exist.
- PanelStateManager owns the three detectors (Harmony, Reflection, Alpha) — correct.
- Card data split into 5 focused providers (CardModelProvider, CardTextProvider, CardStateProvider, DeckCardProvider, ExtendedCardInfoProvider) — correct.
- Harmony patch table lists UXEventQueuePatch, PanelStatePatch, KeyboardManagerPatch, EventSystemPatch — all still exist in src/Patches/.

### Drifted
- **Update loop priority is incomplete.** The doc lists 4 priority levels (Help → Settings → Extended info → Card detail → Active navigator). The actual `OnUpdate()` now has a 4th modal navigator: `GameLogNavigator` runs between Extended info and Card detail. The doc does not mention it.
- **TimerPatch not in patch table.** There are now 5 patch files in `src/Patches/` — the doc omits `TimerPatch` (added for timeout notification announcements). The CLAUDE.md root file still says "4 patch classes in src/Patches/" which is also now wrong.
- **Navigator line counts are stale:**
  - `BaseNavigator.cs`: doc says 2,928 lines → actual 4,085 lines (+39%)
  - `GeneralMenuNavigator.cs`: doc says 4,766 lines → actual 6,148 lines (+29%)
  - `BrowserNavigator.cs`: doc says 2,177 lines → actual 4,528 lines (+108%)
  - `DuelNavigator.cs`: doc says 823 lines → actual 909 lines
  - `GroupedNavigator.cs`: doc says 1,702 lines → actual 2,167 lines
- **Many new navigators not mentioned** (see source-inventory section for full list): DuelChatNavigator, ChatNavigator, ProfileNavigator, AchievementsNavigator, SideboardNavigator, SpinnerNavigator, GameLogNavigator, PhaseSkipGuard.
- **AccessibleArenaMod.cs line count**: doc (via source-inventory) says 309 → actual 442.

---

## 2. framework-reference.md — MOSTLY OK, one wrong version

### OK
- .NET Framework 4.7.2 (net472) — confirmed in csproj.
- MelonLoader.dll and 0Harmony.dll paths — match csproj.
- Unity module references (CoreModule, UI, UIModule, InputLegacyModule, TextMeshPro, InputSystem) — all present in csproj.
- MTGA game assemblies (Core.dll, Assembly-CSharp.dll, Wizards.Arena.Models.dll, Wizards.Arena.Enums.dll, Wizards.Mtga.Metadata.dll, Wizards.Mtga.Interfaces.dll, ZFBrowser.dll) — all in csproj.
- Patch details for UXEventQueuePatch, PanelStatePatch, KeyboardManagerPatch, EventSystemPatch — structurally correct.

### Drifted
- **ilspycmd version wrong.** `framework-reference.md` says "ilspycmd v8.2.0.7535". MEMORY.md records the actual installed version as v9.1.0.7988. The `-l` flag note is correct for both versions.
- **TimerPatch not listed** in the Harmony Patch Details section. It is a real patch in src/Patches/TimerPatch.cs (98 lines) that intercepts `GameManager.Update_TimerNotification` for timeout announcements.
- **SharedClientCore.dll not listed** in the MTGA Game Assemblies table, yet type-index.md lists many types from it (CardPrintingData, AbilityPrintingData, CardDatabase, etc.) and the decompile.ps1 tool references it. It should be added as a known DLL.
- **Wizards.MDN.GreProtobuf.dll not listed** in the csproj (the project does not reference it directly — it is accessed only via reflection at runtime), but this is fine; the doc is correct that it is located in MTGA_Data/Managed/ for decompilation purposes.

---

## 3. source-inventory.md — SIGNIFICANTLY DRIFTED

The inventory was generated 2026-03-04 and has drifted substantially in the 45 days since.

### Summary
- Claimed: 91 files (excluding obj/), ~55,635 total lines.
- Actual: 115 files (excluding obj/), ~94,716 total lines.
- Difference: +24 files, +~39,000 lines (roughly +70% growth in LOC).

### Files listed in inventory that NO LONGER EXIST
These 3 files were deleted/archived as part of the panel detection refactor:
- `Core/Services/PanelDetection/old/detector-plugin-system/PanelRegistry.cs`
- `Core/Services/PanelDetection/old/detector-plugin-system/PanelDetectorManager.cs`
- `Core/Services/PanelDetection/old/detector-plugin-system/IPanelDetector.cs`
  (The directory now contains only a README.md explaining why they were archived.)

These 3 utility/diagnostic files were removed:
- `Core/Services/UnifiedPanelDetector.cs` (MISSING)
- `Core/Services/PanelAnimationDiagnostic.cs` (MISSING)
- `Core/Services/MenuPanelTracker.cs` (MISSING)

### New files NOT in inventory (significant additions)
All of the following exist in src/ but are absent from the inventory:

| File | Lines | Note |
|------|------:|------|
| Core/Services/DuelChatNavigator.cs | 949 | Duel chat sub-navigator (F4 key) |
| Core/Services/ChatNavigator.cs | 856 | Standalone chat window navigator |
| Core/Services/ChatMessageWatcher.cs | 317 | Global incoming message watcher |
| Core/Services/ProfileNavigator.cs | 1,889 | Player profile screen |
| Core/Services/AchievementsNavigator.cs | 1,338 | Achievements screen |
| Core/Services/SideboardNavigator.cs | 1,039 | Bo3 sideboard navigator |
| Core/Services/GameLogNavigator.cs | 178 | Game log modal (F-key) |
| Core/Services/SpinnerNavigator.cs | 580 | Spinner/option selector |
| Core/Services/PhaseSkipGuard.cs | 204 | Space-key guard to prevent accidental pass |
| Core/Services/NPETutorialTextProvider.cs | 203 | NPE tutorial text extraction |
| Core/Services/LetterSearchHandler.cs | 67 | A-Z letter jump in menus |
| Core/Services/SteamOverlayBlocker.cs | 157 | Steam overlay detection/warning |
| Core/Services/UpdateChecker.cs | 354 | Background update check |
| Core/Services/ScreenReaderAdapter.cs | 13 | Thin adapter layer |
| Core/Constants/GameTypeNames.cs | 123 | Game type name constants |
| Core/Interfaces/IScreenReaderOutput.cs | 12 | Interface for screen reader output |
| Core/Utils/KeyHoldRepeater.cs | 90 | Key-hold repeat utility |
| Patches/TimerPatch.cs | 98 | Timer/timeout Harmony patch |
| AssemblyInfo.cs | (small) | Assembly metadata |

### Key line count drift for files that DO exist
Files that grew significantly since the inventory snapshot:

| File | Inventory | Actual | Delta |
|------|----------:|-------:|------:|
| BrowserNavigator.cs | 2,177 | 4,528 | +108% |
| BaseNavigator.cs | 2,928 | 4,085 | +39% |
| GeneralMenuNavigator.cs | 4,766 | 6,148 | +29% |
| DuelAnnouncer.cs | 2,294 | 3,245 | +41% |
| UIActivator.cs | 2,196 | 2,745 | +25% |
| UITextExtractor.cs | 1,976 | 2,760 | +40% |
| UIElementClassifier.cs | 1,677 | 1,857 | +11% |
| CardModelProvider.cs | 2,185 | 2,374 | +9% |
| MasteryNavigator.cs | 1,489 | 2,174 | +46% |
| BrowserZoneNavigator.cs | 1,040 | 1,385 | +33% |
| HotHighlightNavigator.cs | 1,053 | 1,484 | +41% |
| StoreNavigator.cs | 2,042 | 2,773 | +36% |
| BattlefieldNavigator.cs | 700 | 847 | +21% |
| ZoneNavigator.cs | 1,006 | 1,214 | +21% |
| WebBrowserAccessibility.cs | 1,236 | 2,236 | +81% |
| PlayerPortraitNavigator.cs | 1,197 | 2,151 | +80% |
| PanelStatePatch.cs | 1,275 | 1,445 | +13% |
| KeyboardManagerPatch.cs | 175 | 224 | +28% |
| UXEventQueuePatch.cs | 167 | 254 | +52% |
| BoosterOpenNavigator.cs | 999 | 1,494 | +49% |
| GroupedNavigator.cs | 1,805 | 2,167 | +20% |

---

## 4. type-index.md — DRIFTED (index vs files inconsistent)

The type-index lists types correctly but the decompiled/ directory consistency is poor.

### Missing decompiled files (type in index, no .cs in decompiled/)
There are 66 types listed in type-index.md that have no corresponding file in `llm-docs/decompiled/`. Notable gaps:

- Navigation/Controllers: NavBarController, HomePageContentController, EventPageContentController, PacketSelectContentController, CampaignGraphContentController, LearnToPlayControllerV2, DeckSelectBlade, BladeContentView, EventBladeContentView, LastPlayedBladeContentView
- Card views: PagesMetaCardView, CardView, DuelCardView, CardRolloverZoomHandler, StaticColumnMetaCardHolder, StaticColumnMetaCardView, CDCViewMetadata, ScrollCardPoolHolder, ListMetaCardHolder
- Battlefield: UniversalBattlefieldStack, UniversalBattlefieldCardHolder, UniversalBattlefieldLayout, UniversalBattlefieldRegion
- UI: cTMP_Dropdown, NavBarController, ButtonPhaseLadder
- Social: FriendTile, InviteOutgoingTile, InviteIncomingTile, BlockTile, UnifiedChallengeDisplay, ChallengePlayerDisplay
- Home/Campaign: HomePageBillboard, ContentControllerObjectives, CampaignGraphTrackModule, CampaignGraphObjectiveBubble, EBillboardType, EventTimerState, BillboardData
- Mastery: ProgressionTrackLevel, ClientTrackLevelInfo, ProgressionTracksContentController, ContentController_PrizeWall
- Login: RegisterOrLoginPanel, AccountError, CountryCodes
- Learn to Play: TableOfContentsSection, LearnMoreSection
- Mail: ContentControllerPlayerInbox
- Localization: LocalizedString
- GRE: EventContext, StopType, SettingStatus, TimerUpdate, ClientUpdateBase
- Card data: MtgCardInstance (noted as "runtime only"), AbilityPrintingRecord, DynamicAbilityPrintingData, AbilityTextProvider, CardNameTextProvider, DynamicAbilityDataProvider, CardDatabase
- Misc: Pantry, ICardRolloverZoom, StoreDisplayPreconDeck, CardDataForTile, DeckCostsDetails, DeckTypesDetails

Note: Some of these (MtgCardInstance, EventContext, StopType, SettingStatus) are noted in the index as hard to decompile or GRE-only — so their absence may be intentional. But most are simply not yet decompiled.

### Orphan decompiled files (file exists, type NOT in type-index)
There are approximately 160 decompiled .cs files that have no corresponding entry in type-index.md. These were evidently decompiled for investigation but never added to the index. Large categories include:

- Browser types: ScryBrowser, SurveilBrowser, OrderCardsBrowser, AssignDamageBrowser, RepeatSelectionBrowser, SelectGroupBrowser, SplitCardsBrowser, ScrollListBrowser, ButtonScrollListBrowser, SelectNKeywordWithContextBrowser, GroupedBrowserWorkflow, OrderBrowserWorkflow
- Booster/Pack: BoosterChamberController, BoosterCardHolder, BoosterCardSpawner, BoosterOpenCardDataHelper, BoosterOpenToScrollListController, AnimationForwarder_BoosterChamber, SealedBoosterView
- Actions/Workflow: ActionSystem, ActionsAvailableWorkflow, AutoTapActionsWorkflow, AutoTapSolution, TriggeredAbilityWorkflow, GroupWorkflow, OrderWorkflow, SelectCountersWorkflow, SelectNPileGroupWorkflow, CastingTimeOption_ModalRepeatWorkflow
- Rank/Profile: RankDisplay, RankUtilities, SeasonEndRankDisplay, ProfileContentController, ProfileDetailsPanel, ProfileUtilities, QualificationData
- Challenge: ChallengeInviteWindowEntityInvited, ChallengeInviteWindowEntityPlayer, ChallengeInviteWindowPopup, IncomingChallengeRequestTile, CurrentChallengeTile
- Deck: DeckBuilderPile, DeckBuilderWidget, DeckBuilderCardFilterProvider, DeckColumnView, DeckListView, DeckDetailsPopup, DraftDeckManager
- Commander: CommanderSlotCardHolder, CommanderStackCardHolder, CommanderSlotUtils, ListCommanderHolder, ListCommanderView
- Input system: KeyboardManager, NewInputHandler, OldInputHandler, CustomInputModule, CustomUIInputModule
- Settings panels: SettingsMenuPanel, SettingsPanelAccount, SettingsPanelPrivacyPolicy, SettingsPanelReportIssue
- Store: StoreManager, GeneralStoreManager, InactiveStoreManager, StoreDisplayData, StoreDisplayDataPayload, BundlePayload, StorePayload, PackReward
- Reward/Progression: ContentControllerRewards, CurrentProgressionSummary, ProgressionTrack, RewardDisplay, RewardScrollList
- Misc important: PopupBase, Panel, ZoneTransferUXEvent, ZoneType, CounterType, CounterDistribution, SelectionType

There are also 5 scratch/temp files in decompiled/ that should be cleaned up: `_core_types_order.txt`, `core_dump`, `core_search`, `temp-core-search`, `temp_core`.

---

## 5. llm-docs/CLAUDE.md — OK (but minimal)

The file exists and correctly lists the 4 documentation files plus the decompile workflow tools. It does not mention the `decompile-all.ps1` batch refresh tool by name (only by description), which is a minor omission. Content is accurate for what it covers.

---

## 6. Overall Gaps

### Architecture complexity not documented in llm-docs/
The following systems have grown substantially since the March 2026 refactor and warrant their own sections or dedicated files:

1. **DuelChatNavigator (sub-navigator pattern)** — DuelChatNavigator is a 949-line sub-navigator managed exclusively by DuelNavigator, not NavigatorManager. The F4 key handling, SocialUI restoration, ChatVisible polling loop, and input isolation are complex and undocumented in llm-docs. MEMORY.md has a summary but llm-docs/ has nothing.

2. **PhaseSkipGuard** — A 204-line safety guard that prevents accidental Space-key passes during combat/main-phase. Not mentioned anywhere in llm-docs/. The poll-on-every-frame pattern for key-release detection is non-obvious.

3. **Update loop priority (4 modals, not 3)** — architecture-overview.md shows 3 modal layers; there are now 4 (Help, Settings, ExtendedInfo, GameLogNavigator). Any new developer looking at this doc will miss the GameLogNavigator layer.

4. **Chat system (ChatNavigator + ChatMessageWatcher)** — Two new files totalling ~1,173 lines for the full chat UI and global message watching. No llm-docs coverage.

5. **ProfileNavigator + AchievementsNavigator** — 3,227 lines combined of complex screen navigators added after the refactor. No llm-docs coverage.

6. **SteamOverlayBlocker + UpdateChecker** — Infrastructure classes for Steam deployment and auto-update. Not documented.

---

## Recommendations

1. **Regenerate source-inventory.md** — The +24 files and +70% LOC growth makes the current inventory misleading. Run `decompile-all.ps1` equivalent for source, or at minimum update the file/line count header and add the new files listed above.

2. **Fix architecture-overview.md**:
   - Add TimerPatch to the Harmony patch table (5 patches, not 4).
   - Add GameLogNavigator to the OnUpdate priority list (step 4).
   - Update navigator line count claims or remove them (they age quickly).
   - Add a brief mention of DuelChatNavigator as a sub-navigator outside NavigatorManager.
   - Add PhaseSkipGuard to the Update loop section.

3. **Fix framework-reference.md**:
   - Update ilspycmd version from v8.2.0.7535 to v9.1.0.7988.
   - Add TimerPatch to the Harmony Patch Details section.
   - Add SharedClientCore.dll to the MTGA Game Assemblies table (it is referenced by type-index for ~15 types).

4. **Fix CLAUDE.md root** — Change "4 patch classes in src/Patches/" to 5.

5. **Clean up decompiled/**:
   - Remove scratch files: `_core_types_order.txt`, `core_dump`, `core_search`, `temp-core-search`, `temp_core`.
   - The ~160 orphan decompiled files are not harmful but the index should either reference them or acknowledge the index is a subset.
   - Priority types to add to type-index: ScryBrowser, SurveilBrowser, PopupBase, KeyboardManager, ZoneTransferUXEvent, ZoneType, ProgressionTrack, ProfileContentController.

6. **Consider adding** `llm-docs/chat-and-new-navigators.md` or expanding architecture-overview to cover the DuelChatNavigator sub-navigator pattern, PhaseSkipGuard, and the 4-layer modal priority — these are the highest-complexity areas that currently have zero llm-docs coverage.
