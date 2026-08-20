# Unsupported Events — Decompilation Report

Status: **implemented in v1.5.** All six items in "Suggested order of work" below
are in. See `docs/EVENTS.md` for the resulting behaviour and `docs/CHANGELOG.md` for the
user-facing summary. This document is kept as the reference for *why* each change is shaped
the way it is — every claim here was read out of the decompiled game code, not inferred.

Game build 2026.62.

Scope: the four event families that were uncovered before v1.5 — Cube draft,
Pick Two draft, Midweek Magic (partially working), and the paid/prize events
(Arena Direct, Arena Open, Qualifiers).

The guiding constraint for everything below: these events are expensive and
effectively untestable for us — which is exactly why they were left alone until
the game's own code could be read directly. Any change must fail *loudly and
harmlessly* rather than silently mis-click. A missing announcement costs
nothing; a stray click can burn a token, a gem entry fee, or a real prize.

Confirmed in play since: the Midweek Magic event page loads and refreshes
properly again. Everything else here rests on the decompilation, not on a
playthrough.

---

## Headline result

**None of the four families introduces a new screen or a new controller.**

Every one of them runs through the same three controllers we already handle:

- `EventPage.EventPageContentController` — the event page (already detected,
  already info-block navigable)
- `Wotc.Mtga.Wrapper.Draft.DraftContentController` — draft picking (already has
  `DraftNavigator`)
- `Wotc.Mtga.Wrapper.Draft.TableDraftQueueContentController` — human-draft
  podmaking lobby (already has `TableDraftQueueState` + `LoadingScreenNavigator`)

What differs between events is **data**, not UI classes. That confirms the
user's assumption: these are mostly already-covered screens under different
names, plus a small number of state transitions we don't react to.

The corollary is that the risk profile is different from what it looks like.
The danger is not "unknown screen, user gets stuck". The danger is
"screen looks familiar, mod announces stale or wrong state, user presses Enter
on a button that spends a token".

---

## The event taxonomy (authoritative)

`Wizards.Arena.Enums.Event.EventTag` (Wizards.Arena.Enums.dll) is the game's own
classification. Decompiled to `llm-docs/decompiled/EventTag.cs`:

- `QuickDraft = 2` — bot draft
- `PlayerDraft = 6` — human/premier draft
- `Sealed = 3`
- `JumpIn = 5`
- `Traditional = 14` — Bo3 variant flag
- `MidweekMagic = 15`
- `PhysicalPrize = 17`
- `ArenaDirect = 2000`
- `ArenaOpenDay1 = 2001`, `ArenaOpenDay2 = 2002`
- `QualifierPlayIn = 2003`, `QualifierDay1 = 2004`, `QualifierDay2 = 2005`

Cube is *not* in this enum. Cube exists only as localization keys
(`Events_Event_Title_CubeDraft_Arena`, `..._Chromatic`, `..._Tinkerers`,
`..._Power`, `..._Planar_*`, `CubeSealed_Arena`, `MWM_Cube_BotDraft`, …).
Same for Pick Two — only loc keys (`Events_Event_Title_ECL_PickTwo_Draft`,
`..._TMT_PickTwo_Premier_Draft`, and a prefab-ish `_PickTwo_Draft`).

This tag list is worth exposing to the user, by the way: reading the event's
tags on the event page would let us announce "Arena Open Day 2, physical prize"
before the user commits to anything.

---

## The event page is one page with 21 optional components

`EventPage.EventPageComponentFactory.CreateComponents` builds the page from
`EventComponentData`, one nullable field per widget. Every event — Midweek
Magic, Arena Open, a cube draft — is the same scaffolding with a different
subset switched on:

- AverageQueueTimeComponent
- BoosterPacksComponent
- StickerComponent
- CardsComponent
- CashTournamentComponent (used via `PreviewEventComponentController`)
- ChestWidgetComponent
- TextComponent (description)
- EmblemComponent
- InspectPreconDecksComponent
- InspectSingleDeckComponent
- LossDetailsComponent
- TitleRankComponent
- ResignComponent
- SelectedDeckComponent
- PrizeWallComponent
- TimerComponent
- ViewCardPoolComponent
- MainButtonComponent
- EventButtonComponent (sold out)
- ObjectiveTrackComponent ×3 (by-course, cumulative, hidden-bubbles)

Practical consequence: we do not need per-event code. We need the existing
event-page navigator to (a) refresh at the right moments and (b) describe the
main button honestly.

---

## The main button is five buttons, and only some of them ask before spending

`EventPage.Components.MainButtonComponent` holds five separate
`CustomButtonWithTooltip` objects, one per view state:

- `PlayState` — Play / Start Draft / Rejoin Draft / Build Deck / Select Deck /
  Choose Packets / Claim Prize
- `StartState` — free entry
- `GemsState` — pay with gems
- `GoldState` — pay with gold
- `EventState` — pay with a draft/event/sealed token

`ResetButtons()` hides all five, then one is shown. Hidden ones stay in the
hierarchy. `MainButtonComponentController.Update(...)` decides which.

Two findings that matter for safety:

**1. Multiple pay buttons can be visible at once.** The controller loops over
`playerEvent.EventInfo.EntryFees` and shows a button *per fee*. An event
offering "gems or gold or a token" produces three live buttons side by side.
Any label-substring heuristic will pick the wrong one.

**2. Only the gem path confirms.** In
`EventComponentManager.MainButton_OnPayJoinButtonClicked`:

- `EventEntryCurrencyType.Gem` → `SystemMessageManager.ShowSystemMessage(...)`
  with a cancel option, and only the confirm callback calls `JoinAndPayEvent`.
- **every other currency** (Gold, DraftToken, EventToken, SealedToken, Free)
  → falls straight through to `JoinAndPayEvent(...)`. No dialog. The token is
  spent on the click.

So on a token-entry event, one Enter = one token gone, with no undo and no
confirmation popup for us to intercept. The button's own text is set by
`UpdateTextWithQuantity(entryFee.Quantity)` — often just the bare number
("750"), with the currency conveyed only by the icon. Reading the button label
alone tells a blind user nothing about what they are about to spend.

**Recommendation:** before we let Enter through on an event-page main button,
announce the resolved entry fee — currency type and quantity, read from
`EventEntryFeeInfo` via the controller, not from the label — and require a
second, deliberate keypress for any non-gem currency. Ugly is fine here.

---

## Midweek Magic — the refresh bug has a concrete cause

Two independent mechanisms, both real:

### 1. The event page is cached and reused

`EventPageContentController` keeps
`Dictionary<string, EventPage> _instantiatedEventPages`, keyed by
`InternalEventName`. `_factory.CreateComponents(...)` runs **only on the first
visit**. On every later visit the same GameObjects are re-activated and only
`UpdateComponents()` runs. So "the page rebuilt itself" is never true after the
first time, and anything that keys off construction will not fire again.

Open sequence, for reference:

```
OnBeginOpen
  CoroutineBeginOpen
    wait for EventManager.RefreshingEventContexts == false
    reuse or instantiate EventPageScaffolding
    ComponentManager.UpdateComponents()      <- stale course data
    EventScaffolding.SetActive(true)
    Coroutine_ShowTemplate
      _readyToshow = false
      await PlayerEvent.GetEventCourse()      <- server round trip
      _readyToshow = true
      ComponentManager.OnEventPageOpen(ctx)
        UpdateComponents(onOpen: true)        <- fresh data lands HERE
```

`IsReadyToShow` already covers this window (we do gate on it in
`ReflectionPanelDetector` and `MenuScreenDetector`), so the first announcement
is probably fine. The problem is what happens *after*.

### 2. In-place state transitions fire no panel event

`EventComponentManager.SetProgressBarState(EventPageStates)` broadcasts
`OnEventPageStateChanged` to every component controller and swaps the whole page
between `DisplayQuest`, `ClaimQuestRewards`, `DisplayEvent`,
`ClaimEventRewards`. `UpdateComponents(bool)` re-runs every component's
`Update`. Neither opens or closes a panel. We get no rescan trigger.

This is exactly the Midweek Magic loop: play a match → return to the event page
with `PostMatchContext != null` → `DisplayQuest` → quest bar → `DisplayEvent`
→ possibly `ClaimEventRewards`. Three full page reconfigurations, zero panel
events. The mod keeps announcing the pre-match page.

And `SelectedDeckComponent.UpdateDeckBoxUI` makes it worse: it calls
`ReleaseDeckView(_deckBox)` then `CreateDeckView(...)`, i.e. it **destroys and
recreates** the deck-box GameObject on every update. Any cached element
reference we hold across an update is a dead Unity object. That is a very good
match for "sometimes the opening screen does not refresh properly" — MWM precon
events are precisely the ones carrying a `SelectedDeckComponent`.

**Recommendation:** Harmony-postfix
`EventComponentManager.SetProgressBarState(EventPageStates)` and
`EventComponentManager.UpdateComponents(bool)`, and use them to trigger a
delayed rescan of the event page plus a re-announce of the current element.
Both are cheap, both are on our existing patch pattern (`PanelStatePatch`
already postfixes the two `TableDraftQueue*` notification handlers the same
way). This is low-risk: it only causes extra rescans, never a click.

Note this fix is not MWM-specific. It repairs the event page for every event,
including the paid ones.

---

## Cube draft — no gap in the draft screens themselves

Cube events are ordinary bot or human drafts with a different card pool. Path:

- bot cube (`MWM_Cube_BotDraft`, Arena Cube quick draft):
  `PlayerEventModule.Draft` → `LoadBotDraft()` → `GoToDraftScene` →
  `DraftContentController` → **covered by `DraftNavigator`**
- traditional/premier cube: `PlayerEventModule.HumanDraft` →
  `DraftState.Podmaking` → `GoToTableDraftQueueScene` → **covered by
  `TableDraftQueueState` + `LoadingScreenNavigator`** → then
  `DraftState.Picking` → `GoToDraftScene` → **covered**
- `CubeSealed_Arena`: sealed → `SealedBoosterOpenController` → **covered by
  `BoosterOpenNavigator`** → deck builder → covered

There is genuinely nothing cube-specific in the UI layer. `DraftModes` has
exactly two values (`BotDraft`, `HumanDraft`).

The one soft caveat: cube pools contain cards from every set and many cards
that are not in Standard-legal printings. That stresses `CardModelProvider` /
`CardTextProvider` lookups more than a normal draft does, but it is the same
code path the collection already exercises.

**Recommendation:** treat cube as already supported and say so, rather than
writing cube-specific code. If we want one improvement, it is announcing the
pack/pick position (see below), which helps all drafts.

---

## Pick Two draft — one real gap, and one pre-existing hazard it makes worse

Pick Two is `DraftContentController` with `NumCardsToPick == 2`. The plumbing:

- `IDraftPod.PickNumCardsToTake` (int)
- `DraftDeckManager.NumCardsToPick => DraftPod?.PickNumCardsToTake ?? 1`
- `DraftContentController.NumberOfCardsToTake`, `NumberOfCardsCurrentlySelected`,
  `AtMaxReservedCards`
- `Wizards.Unification.Models.Draft.PickInfo` carries `NumCardsToPick`,
  `SelfPack`, `SelfPick`, `TimeoutSec`, `PassDirection`, `SuggestedCards`
- `DynamicDraftStateVisualData` carries `PackNumber`, `PickNumber`,
  `NumberOfCardsToPick`, `PassDirectionIsLeft`

Selection is a two-phase "reserve then confirm":
`ToggleCardReservation` adds/removes from `DraftDeckManager._reservedCards`;
`HandleOnConfirmPickButtonClicked` only submits when `AtMaxReservedCards`.

### Gap 1 — Space does nothing in a Pick Two draft

`DraftDeckView.UpdateConfirmButton(numSelected, numToTake)`:

- when `numSelected == numToTake` (or `numToTake == 1`) → label
  `EPP/RewardWeb/ConfirmPick`, `Interactable = numSelected == numToTake`
- otherwise → label `Social/Presence/Button_PackPick_PickX`,
  `Interactable = false`

Verified against the shipped localization DB
(`MTGA_Data/Downloads/Raw/Raw_ClientLocalization_*.mtga`, SQLite):

- `EPP/RewardWeb/ConfirmPick` → EN "Confirm Pick", DE "Auswahl bestätigen"
- `Social/Presence/Button_PackPick_PickX` →
  EN "Select ({numberSelected}/{numberToSelect})"

Our `DraftNavigator.ClickConfirmButton()` finds the button by
`label.Contains("confirm") || label.Contains("bestätigen")`. That matches both
EN and DE in the *ready* state, but with one of two cards selected the label is
"Select (1/2)" — no match. So in a Pick Two draft Space is silently a no-op
until the second card is selected, and the user gets no feedback about why.
(The English/German-only substring match is fragile in every other language
too; Pick Two is just where it breaks the flow outright.)

The upside: once we resolve the button properly, that same string already
carries the progress we want to announce.

**Recommendation:** stop matching on label text. Resolve the confirm button
from `DraftDeckView._confirmPickButton` on the active deck view via reflection,
and announce selection progress ("1 of 2 selected") plus the button's
`Interactable` state instead of pretending the press worked.

### Gap 2 — Enter twice quickly can submit the pick with no confirmation

This one already exists in every draft; Pick Two just raises the stakes.

`DraftContentController.HandleOnCardClicked` implements its own double-click
detection: same card, `_clickStopwatch.Elapsed < 0.5s` →
`ReserveCardAndLockIn(...)` instead of `ToggleCardReservation(...)`. And
`ReserveCardAndLockIn` ends with:

```csharp
if (AtMaxReservedCards && _draftDeckManager.AllReservedCardsLocked())
{
    DraftCards(_draftDeckManager.GetReservedCards());   // submits immediately
    return;
}
```

So two Enters within half a second on the same card **submit the pick**,
bypassing the confirm button entirely. A user who presses Enter, hears nothing
useful, and presses Enter again to deselect has instead just locked in that
card. In Pick Two, doing that on the second card commits both.

**Recommendation, in order of preference:**

1. Call `DraftContentController.ToggleCardReservation(DraftPackCardView)` by
   reflection instead of simulating a click. It is private but stable, and it
   is the exact method the single-click path calls. This removes the race
   entirely rather than papering over it.
2. If we prefer not to reflect into a private method, enforce our own ≥0.6 s
   debounce per card on the Enter path, and announce "already selected" instead
   of re-firing.

Either way this is worth doing for quick draft today, independent of Pick Two.

### Gap 3 — no pack/pick announcement

We announce the card count but never "Pack 2, pick 5, take 2 of 9". All of it
is available (`PickInfo.SelfPack` / `SelfPick` / `NumCardsToPick`, or
`DynamicDraftStateVisualData.PackNumber` / `PickNumber`). Cheap, read-only,
zero click risk. Good candidate for the first patch.

---

## Paid and prize events (Arena Direct / Arena Open / Qualifiers)

No dedicated screens, no in-client eligibility or terms gate (searched: no
`Eligib*` / `TermsOf*` / `Ineligible*` UI strings exist). These run on the
standard event page with a heavier component set — `TimerComponent`,
`LossDetailsComponent`, `ObjectiveTrackComponent`, `PrizeWallComponent`,
`CashTournamentComponent`.

`CashTournamentComponent` is just a labelled `CustomButton`; its click goes to
`EventComponentManager.CashTournamentComponent_OnClicked(eventName)`, which
looks up another `EventContext` and calls
`SceneLoader.GoToEventScreen(ctx, reloadIfAlreadyLoaded: true)` — i.e. it hops
to a *different event's page*, same controller. Note `reloadIfAlreadyLoaded:
true`: this is another in-place refresh with no panel change, and it lands on a
page whose title has changed. Without the `SetProgressBarState` /
`UpdateComponents` hook above, we will keep announcing the old event's name on
the new event's page.

### The physical-prize claim popup

The only genuinely event-specific interaction in the whole report.
`EventComponentManager.ClaimPrize()`:

```csharp
if (EventContext.PlayerEvent.EventInfo.EventTags.Contains(EventTag.PhysicalPrize)
    && chestDescription.image1.Contains("ArenaDirect"))
{
    // body text = Events/Rewards/Physical_PopUp_Directions
    //           + "Email: <account email>"
    //           + "Country: <region display name> (<code>)"
    SystemMessageManager.Instance.ShowMessage(
        title:   "Events/Rewards/Physical_PopUp_Title",
        message: <that body>,
        button1: "Events/Rewards/Physical_PopUp_ContactCS",  -> Application.OpenURL(support form)
        button2: "Events/Rewards/Physical_PopUp_ConfirmInfo" -> no-op callback
    );
}
Promise<ICourseInfoWrapper> claimPrize = EventContext.PlayerEvent.ClaimPrize();
```

Things to know:

- It is a plain `SystemMessageView`, which `BaseNavigator.Popup` already
  handles generically. So it is navigable today — but the *body* carries the
  email and country the player needs in order to actually receive a physical
  prize, and it is the kind of text our info-block filters are prone to
  swallow. This one must be read out verbatim.
- The two buttons are not "OK / Cancel". One opens a browser to a Zendesk form,
  the other just acknowledges. Backspace-to-dismiss semantics from
  `BaseNavigator.Popup` ("clicks first button") would open the browser. That is
  the wrong default here.
- `ClaimPrize()` fires regardless of which button is pressed — the popup is
  informational, not a gate. So there is no risk of the *claim* being lost by a
  wrong keypress, only of the *instructions* being missed.

**Recommendation:** special-case this popup by title loc key, read the full
body including email and country, label both buttons explicitly, and make
Backspace a no-op rather than "activate the first button".

### Sold-out and error paths

- `JoinType.Closed` → no pay button rendered at all, and a separate
  `EventButtonComponent` shows `Events/Button_SoldOut`, whose click shows
  `Events/SoldOut_Title` / `Events/SoldOut_Notice`.
- `JoinAndPayEvent` failure surfaces `ServerErrors.Event_MaxJoinLimitReached`
  as the same sold-out message and flips `Joinability` to `Closed` — another
  silent in-place page change.
- `ServerErrors.Event_EntryFeeRequired` → generic network-error popup.

All three are `SystemMessageManager` popups and covered generically; they just
need the rescan hook to be announced at the right time.

---

## Suggested order of work — all implemented in v1.5

Ordered by (safety benefit) ÷ (risk of touching a live event):

1. **Draft pack/pick announcement** — read-only, helps cube and Pick Two and
   quick draft alike. No click path touched.
   → `DraftNavigator.GetPackAndPick()`, folded into `GetScreenName()`.
2. **Draft Enter debounce / `ToggleCardReservation`** — closes a real
   mis-pick hole that exists today in quick draft.
   → `DraftNavigator.ToggleCurrentCard()` / `TryToggleCardReservation()`, with a
   0.6 s per-card debounce on the click fallback.
3. **Confirm button resolved from `_confirmPickButton`, with "N of M selected"
   progress** — makes Pick Two usable and de-hardcodes the German/English label
   match.
   → `GetConfirmPickButton()`, `GetSelectionProgressIfMeaningful()`. Progress and
   quota are announced **only when the quota is above 1**, so an ordinary draft
   never hears "1 of 1". Selecting past a full quota now also names the card the
   game silently dropped.
4. **`SetProgressBarState` + `UpdateComponents` rescan hooks** — fixes Midweek
   Magic refresh and every other event page along with it.
   → `PanelStatePatch.PatchEventComponentManager()` →
   `PanelStatePatch.OnEventPageRefreshed` →
   `GeneralMenuNavigator.OnEventPageRefreshed()`.
5. **Entry-fee announcement + second-keypress guard on non-gem currencies** —
   the one thing standing between a blind user and an accidentally spent token.
   → `EventAccessor.GetEventEntryFee()`,
   `GeneralMenuNavigator.ConfirmEventEntryFee()`. Effective because `CustomButton`
   implements only pointer handlers and no `ISubmitHandler`, so withholding
   `UIActivator.Activate` withholds the whole activation.
6. **Physical-prize popup special case** — only matters for Arena Direct, but
   it is the one place where a missed announcement costs a real-world prize.
   → `BaseNavigator.IsPhysicalPrizePopup()`,
   `AnnotatePhysicalPrizeButtons()`, and the early return in `DismissPopup()`.

Items 1–4 are testable without entering a paid event: a free bot draft
exercises 1–3, and any Midweek Magic or free event exercises 4. Items 5 and 6
cannot be tested here and ship with explicit "untested" wording in the changelog.

---

## Types decompiled for this report

Added to `llm-docs/decompiled/`:

- `EventTag`, `PlayerEventModule`, `MDNEFormatType`, `EventPageStates`,
  `DraftState`, `DraftModes`
- `EventPageContentController`, `EventPageScaffolding`,
  `EventPageComponentFactory`, `EventComponentManager`,
  `EventPageRewardsController`
- `MainButtonComponent`, `MainButtonComponentController`,
  `CashTournamentComponent`, `PrizeWallComponent`,
  `PrizeWallComponentController`, `TimerComponent`, `TimerComponentController`,
  `ObjectiveTrackComponent`, `LossDetailsComponent`, `SelectedDeckComponent`
- `DraftPackHolder`, `DraftDeckView`, `BotDraftPod`, `PickInfo`, `TableInfo`,
  `DynamicDraftStateVisualData`, `StaticDraftStateVisualData`
- `TournamentController`
