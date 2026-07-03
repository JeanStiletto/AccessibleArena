using UnityEngine;
using UnityEngine.EventSystems;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services.ElementGrouping;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for rewards popup (from mail claims, store purchases, etc.).
    /// Extracted from GeneralMenuNavigator - uses exact same detection and discovery logic.
    /// </summary>
    public class RewardPopupNavigator : BaseNavigator
    {
        public override string NavigatorId => "RewardPopup";
        public override string ScreenName => Strings.ScreenRewards;
        public override int Priority => 86; // Higher than Overlay (85), below SettingsMenu (90)

        private GameObject _activePopup;
        private int _rewardCount;
        private int _cardIndex;          // Card-only counter for numbering cards separately from other rewards
        private int _seasonEndState;     // 0=None, 1=OldRank, 2=Rewards, 3=NewRank
        private string _seasonDisplayText; // Extracted rank text for season end announcements
        private int _lastRescanElementCount; // Suppress duplicate announcements in ForceRescan

        // Season-end phase, derived from which display objects are active rather than
        // the game's _endOfSeasonDisplayState enum (observed stuck at OldRankDisplay
        // across all three phases in user logs). Drives reward enumeration, the
        // activation announcement, and the input mode.
        private enum SeasonPhase { NotSeason, RankDisplay, RewardsReveal }
        private SeasonPhase _seasonPhase = SeasonPhase.NotSeason;

        // Last spoken season announcement — suppresses the identical-text repeats
        // seen when every rescan re-announces via AnnounceInterrupt (Immediate is
        // exempt from the announcer's consecutive-duplicate filter).
        private string _lastSeasonAnnouncement;

        // True while we are waiting for the season reward-reveal animation to finish
        // spawning its RewardPrefab_ objects. The reveal is animated: when the user
        // advances into the rewards phase we rescan ~1 frame later, long before any
        // prefab exists, so the first pass finds only the title. PollSeasonReveal
        // keeps checking each frame and re-scans once the reveal settles.
        private bool _awaitingSeasonReveal;

        // Time.time when reward prefabs were first seen while awaiting the reveal.
        // Used for a safety re-scan if the reveal coroutine never reports "done"
        // (e.g. a rare multi-page season payout waiting on a "More" click). -1 = unset.
        private float _seasonRevealPrefabSince = -1f;

        // Pre-extracted pack set names from ContentControllerRewards._packReward data
        // Uses List+index instead of Queue because rescans re-discover elements but
        // the game's ToAdd queue gets consumed after first display
        private List<string> _packSetNames = new List<string>();
        private int _packSetNameIndex;

        // Time.time when popup was first detected with subpages=false but no content yet.
        // Used for 2s timeout fallback. -1 means not tracking.
        private float _popupDetectedTime = -1f;

        // True after the timeout fallback fired once. If the next detection cycle still
        // finds no content, return false (give up) instead of looping forever.
        private bool _timeoutFallbackFired = false;

        // True if we ever saw _stillDisplayingSubpagesForRewardItem == true for the
        // current popup. Only allow the timeout fallback when this is set — it means
        // the game actually started a reveal cycle. Without this, the preloaded empty
        // Rewards controller (always active at startup / during Prize Wall) triggers
        // a false positive.
        private bool _revealingWasSeen = false;

        // Cache to avoid logging spam
        private bool _lastRewardsPopupState = false;

        public RewardPopupNavigator(IAnnouncementService announcer) : base(announcer) { }

        /// <summary>
        /// True while the game's reward reveal coroutine is in progress for the
        /// current popup (i.e. _stillDisplayingSubpagesForRewardItem was observed
        /// as true). Other navigators should defer to this one while claiming is
        /// underway — DetectScreen keeps returning false until the reveal finishes
        /// and content is ready, so lower-priority navigators would otherwise grab
        /// the overlay-blocker state in the meantime.
        /// </summary>
        public bool IsClaimInProgress => _revealingWasSeen;

        public override void Update()
        {
            // While the season reward-reveal animation is running, keep watching for
            // its prefabs so we can announce what was earned instead of just the title.
            if (_isActive && _awaitingSeasonReveal)
                PollSeasonReveal();

            base.Update();
        }

        /// <summary>
        /// Poll the season reward-reveal animation and re-scan once it has settled.
        /// The reveal spawns its RewardPrefab_ objects over time (card flips, page
        /// sequencing), and _seasonRewardCoroutine stays non-null until the whole
        /// sequence finishes — so IsSeasonRevealBusy() going false is the "all prefabs
        /// present" signal. A prefabs-visible timeout covers the rare multi-page payout
        /// that parks on a "More" click without ever clearing the busy flag.
        /// </summary>
        private void PollSeasonReveal()
        {
            // Left the rewards phase (advanced to new-rank, or popup gone) — stop polling.
            if (_seasonPhase != SeasonPhase.RewardsReveal)
            {
                _awaitingSeasonReveal = false;
                _seasonRevealPrefabSince = -1f;
                return;
            }

            if (!HasRewardPrefabs())
            {
                _seasonRevealPrefabSince = -1f;
                return;
            }

            if (_seasonRevealPrefabSince < 0f)
                _seasonRevealPrefabSince = Time.time;

            bool settled = !IsSeasonRevealBusy();
            bool prefabsShownLongEnough = Time.time - _seasonRevealPrefabSince >= 6.0f;

            if (settled || prefabsShownLongEnough)
            {
                _awaitingSeasonReveal = false;
                _seasonRevealPrefabSince = -1f;
                Log.Msg("{NavigatorId}", $"Season reward reveal settled (settled={settled}) — rescanning for rewards");
                ForceRescan();
            }
        }

        /// <summary>True if any RewardPrefab_ object is active inside the popup.</summary>
        private bool HasRewardPrefabs()
        {
            if (_activePopup == null) return false;

            foreach (Transform t in _activePopup.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.gameObject.activeInHierarchy && t.name.StartsWith("RewardPrefab_"))
                    return true;
            }
            return false;
        }

        #region Detection - Copied from OverlayDetector

        protected override bool DetectScreen()
        {
            bool result = CheckRewardsPopupOpenInternal();

            // Only log when state changes to reduce spam
            if (result != _lastRewardsPopupState)
            {
                _lastRewardsPopupState = result;
                Log.Msg("{NavigatorId}", $"IsRewardsPopupOpen changed to: {result}");

                // The "ContentController - Rewards" GO lives on Canvas - Screenspace
                // Popups and stays active across purchases — its mere presence won't
                // tell us a new reward cycle has started. Use the public True→False
                // transition as the lifecycle boundary: when one popup ends, drop the
                // pack-name cache, the revealing flag, and the timeout-fired flag so
                // the next purchase re-runs the full ExtractPackSetNames flow.
                if (!result)
                {
                    _packSetNames.Clear();
                    _packSetNameIndex = 0;
                    _revealingWasSeen = false;
                    _timeoutFallbackFired = false;
                    _popupDetectedTime = -1f;
                }
            }

            return result;
        }

        /// <summary>
        /// Check if the rewards popup is currently open AND has displayable content.
        /// Returns false during loading/transition phases so the navigator stays inactive
        /// until content is ready (NPE-style gating).
        /// </summary>
        private bool CheckRewardsPopupOpenInternal()
        {
            // Look for the rewards controller in Screenspace Popups canvas
            var screenspacePopups = GameObject.Find("Canvas - Screenspace Popups");
            if (screenspacePopups == null)
            {
                ClearPopupState();
                return false;
            }

            // Find the rewards controller - it should have ContentController and Rewards in its name
            bool foundMatchingChild = false;
            foreach (Transform child in screenspacePopups.transform)
            {
                if (!child.name.Contains("ContentController") || !child.name.Contains("Rewards") ||
                    !child.gameObject.activeInHierarchy)
                    continue;

                foundMatchingChild = true;
                _activePopup = child.gameObject;

                // Read season end state from controller
                _seasonEndState = GetSeasonEndState();

                // Phase-specific content gating
                if (_seasonEndState == 1 || _seasonEndState == 3) // OldRank or NewRank
                {
                    // Rank display phases: content is available immediately (SetRank populates TMP_Text before visible)
                    // Check that the end-of-season GameObject is active
                    return HasActiveSeasonDisplay();
                }

                // State 2 (Rewards) or 0 (None): activate as soon as reward prefabs exist.
                // Do NOT gate purely on _stillDisplayingSubpagesForRewardItem — that flag
                // stays true across multi-page reveals (turtle packs, SOS Strixhaven)
                // while prefabs are already on screen and the user clicks "Mehr" between
                // pages. Gating on it would keep us inactive for the entire sequence.
                bool? stillRevealing = ReadStillRevealingFlag();

                if (stillRevealing == true)
                {
                    // Coroutine is active — extract pack names while ToAdd queue is
                    // still populated (queue is cleared 1 frame after RevealRewards starts).
                    _revealingWasSeen = true;
                    ExtractPackSetNames();
                }

                // Check for actual reward content
                bool hasContent = false;
                foreach (Transform t in child.GetComponentsInChildren<Transform>(true))
                {
                    if (t == null || !t.gameObject.activeInHierarchy) continue;
                    if (t.name.StartsWith("RewardPrefab_") || t.name == "RewardsCONTAINER")
                    {
                        hasContent = true;
                        break;
                    }
                }

                if (hasContent)
                {
                    // Prefabs visible — activate regardless of coroutine state.
                    _popupDetectedTime = -1f;
                    _timeoutFallbackFired = false;
                    return true;
                }

                // No content yet. If the coroutine is still setting up, just wait —
                // don't start the timeout clock until the flag drops or fails to appear.
                if (stillRevealing == true)
                {
                    _popupDetectedTime = -1f;
                    continue;
                }

                // Flag is false/unavailable and no content. Only use the timeout fallback
                // if we previously saw the revealing flag, meaning the game actually
                // started a reward reveal cycle. Without this gate, the preloaded empty
                // Rewards controller (always active at startup / during Prize Wall)
                // would cause false positives.
                if (!_revealingWasSeen)
                {
                    // Preloaded shell — don't start a timeout, don't activate.
                    continue;
                }

                // The timeout fallback already fired once and found no elements —
                // stop retrying to prevent an infinite loop.
                if (_timeoutFallbackFired)
                {
                    return false;
                }

                // Start/check timeout for fallback (revealing was seen, prefabs may be slow).
                if (_popupDetectedTime < 0f)
                    _popupDetectedTime = Time.time;

                if (Time.time - _popupDetectedTime >= 2.0f)
                {
                    // Timeout: try once (DiscoverElements will use controller data fallback).
                    _timeoutFallbackFired = true;
                    Log.Msg("{NavigatorId}", $"Content timeout — activating with controller data fallback");
                    return true;
                }

                // Still waiting for content or timeout
                continue;
            }

            if (foundMatchingChild)
            {
                // Popup found but not ready yet — preserve timeout and pack name state
                return false;
            }

            // No matching popup found — clear everything (incl. _packSetNames so a
            // subsequent purchase doesn't re-use the previous pack's set name).
            ClearPopupState();
            return false;
        }

        /// <summary>
        /// Reset all popup-specific state when the popup disappears.
        /// Called from detection when no popup is found — NOT from OnSceneChanged,
        /// because the popup (in Screenspace Popups canvas) survives scene changes.
        /// </summary>
        private void ClearPopupState()
        {
            _activePopup = null;
            _seasonEndState = 0;
            _popupDetectedTime = -1f;
            _timeoutFallbackFired = false;
            _revealingWasSeen = false;
            _packSetNames.Clear();
            _packSetNameIndex = 0;
            _seasonDisplayText = null;
            _lastRescanElementCount = 0;
            _seasonPhase = SeasonPhase.NotSeason;
            _lastSeasonAnnouncement = null;
            _awaitingSeasonReveal = false;
            _seasonRevealPrefabSince = -1f;
        }

        /// <summary>
        /// Find the ContentControllerRewards component inside the active popup.
        /// Central lookup used by all season/reward reflection helpers.
        /// </summary>
        private MonoBehaviour FindRewardsController()
        {
            if (_activePopup == null) return null;

            foreach (var mb in _activePopup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb != null && mb.GetType().Name == "ContentControllerRewards")
                    return mb;
            }
            return null;
        }

        /// <summary>Read a private instance field off the rewards controller (null-safe).</summary>
        private object ReadControllerField(MonoBehaviour controller, string fieldName)
        {
            var fi = controller?.GetType().GetField(fieldName, PrivateInstance);
            return fi?.GetValue(controller);
        }

        /// <summary>
        /// Read _endOfSeasonDisplayState from ContentControllerRewards via reflection.
        /// Returns integer cast: 0=None, 1=OldRankDisplay, 2=RewardsDisplay, 3=NewRankDisplay.
        /// </summary>
        private int GetSeasonEndState()
        {
            try
            {
                var controller = FindRewardsController();
                var val = ReadControllerField(controller, "_endOfSeasonDisplayState");
                if (val != null) return (int)val;
            }
            catch (System.Exception ex)
            {
                Log.Msg("{NavigatorId}", $"GetSeasonEndState failed: {ex.Message}");
            }

            return 0;
        }

        /// <summary>
        /// Determine the current season-end phase from active display objects.
        /// Independent of _endOfSeasonDisplayState (unreliable in the field) and of
        /// localized title text: the game keeps the SeasonEndRankDisplay objects
        /// active only during the old/new rank phases and explicitly deactivates
        /// them during the rewards reveal.
        /// </summary>
        private SeasonPhase DetermineSeasonPhase()
        {
            var controller = FindRewardsController();
            if (controller == null) return SeasonPhase.NotSeason;

            // Are we in an end-of-season context at all?
            bool isSeason = false;
            var state = ReadControllerField(controller, "_endOfSeasonDisplayState");
            if (state != null && (int)state != 0) isSeason = true;
            if (!isSeason)
            {
                var eosGo = ReadControllerField(controller, "_endOfSeasonGameObject") as GameObject;
                if (eosGo != null && eosGo.activeInHierarchy) isSeason = true;
            }
            if (!isSeason) return SeasonPhase.NotSeason;

            return IsAnyRankDisplayActive(controller)
                ? SeasonPhase.RankDisplay
                : SeasonPhase.RewardsReveal;
        }

        /// <summary>True if either SeasonEndRankDisplay (constructed/limited) is active.</summary>
        private bool IsAnyRankDisplayActive(MonoBehaviour controller)
        {
            foreach (string fieldName in new[] { "_endSeasonRankDisplayConstructed", "_endSeasonRankDisplayLimited" })
            {
                var disp = ReadControllerField(controller, fieldName) as MonoBehaviour;
                if (disp != null && disp.gameObject.activeInHierarchy) return true;
            }
            return false;
        }

        /// <summary>Read the localized rewards title (e.g. "Season Rewards") for context.</summary>
        private string ReadRewardsTitle()
        {
            var controller = FindRewardsController();
            var tmp = ReadControllerField(controller, "_rewardsTitleText") as TMPro.TMP_Text;
            if (tmp == null) return null;
            return UITextExtractor.StripRichText(tmp.text)?.Trim();
        }

        /// <summary>
        /// True while the game is still revealing season rewards (card flips, page
        /// reveals, running coroutines). Mirrors the guard in OnClaimClicked_Unity
        /// that absorbs clicks — so we can tell the user to wait instead of advancing.
        /// </summary>
        private bool IsSeasonRevealBusy()
        {
            var controller = FindRewardsController();
            if (controller == null) return false;

            var still = ReadControllerField(controller, "_stillDisplayingSubpagesForRewardItem");
            if (still is bool b && b) return true;
            if (ReadControllerField(controller, "_seasonRewardCoroutine") != null) return true;
            if (ReadControllerField(controller, "_claimRewardsCoroutine") != null) return true;
            return false;
        }

        /// <summary>
        /// Check if the end-of-season display GameObjects are active (rank display visible).
        /// </summary>
        private bool HasActiveSeasonDisplay()
        {
            if (_activePopup == null) return false;

            try
            {
                var controller = FindRewardsController();
                if (controller == null) return false;

                // Check _endOfSeasonGameObject active
                var go = ReadControllerField(controller, "_endOfSeasonGameObject") as GameObject;
                if (go != null && go.activeInHierarchy)
                    return true;

                // Check SeasonEndRankDisplay component active
                var display = ReadControllerField(controller, "_endSeasonRankDisplayConstructed") as MonoBehaviour;
                if (display != null && display.gameObject.activeInHierarchy)
                    return true;
            }
            catch (System.Exception ex)
            {
                Log.Msg("{NavigatorId}", $"HasActiveSeasonDisplay failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Read _stillDisplayingSubpagesForRewardItem from ContentControllerRewards.
        /// Returns true while rewards are being revealed via coroutines, false when done.
        /// Returns null if the controller or field cannot be found.
        /// </summary>
        private bool? ReadStillRevealingFlag()
        {
            if (_activePopup == null) return null;

            try
            {
                var controller = FindRewardsController();
                var val = ReadControllerField(controller, "_stillDisplayingSubpagesForRewardItem");
                if (val is bool b) return b;
            }
            catch (Exception ex)
            {
                Log.Msg("{NavigatorId}", $"ReadStillRevealingFlag failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get the rewards container transform for element discovery.
        /// Copied exactly from OverlayDetector.GetRewardsContainer().
        /// </summary>
        private Transform GetRewardsContainer()
        {
            var screenspacePopups = GameObject.Find("Canvas - Screenspace Popups");
            if (screenspacePopups == null)
                return null;

            foreach (Transform child in screenspacePopups.transform)
            {
                if (child.name.Contains("ContentController") && child.name.Contains("Rewards") &&
                    child.gameObject.activeInHierarchy)
                {
                    var container = child.Find("Container");
                    if (container != null)
                    {
                        var rewardsContainer = container.Find("RewardsCONTAINER");
                        if (rewardsContainer != null && rewardsContainer.gameObject.activeInHierarchy)
                            return rewardsContainer;
                    }
                }
            }

            return null;
        }

        #endregion

        #region Discovery - Copied from GeneralMenuNavigator

        protected override void DiscoverElements()
        {
            _rewardCount = 0;
            _cardIndex = 0;
            var addedObjects = new HashSet<GameObject>();

            _seasonDisplayText = null;

            // Derive the phase from active display objects, not the unreliable
            // _endOfSeasonDisplayState enum (see DetermineSeasonPhase).
            _seasonPhase = DetermineSeasonPhase();

            if (_seasonPhase == SeasonPhase.RankDisplay)  // OldRank or NewRank
            {
                DiscoverSeasonRankElements(addedObjects);
            }
            else if (_seasonPhase == SeasonPhase.RewardsReveal)
            {
                // Season reward reveal: enumerate what was actually earned instead of
                // treating it as a rank screen (the old bug left this phase silent).
                DiscoverSeasonRewardElements(addedObjects);
            }
            else
            {
                // Pre-extract pack set names from controller data before discovering UI elements
                ExtractPackSetNames();

                // Discover reward elements
                DiscoverRewardElements(addedObjects);

                // Fallback: if no prefabs found (timeout case), read reward data from controller
                if (_rewardCount == 0)
                    DiscoverRewardsFromControllerData(addedObjects);
            }

            // Only expose buttons on the normal (non-season) rewards popup. On the
            // season screens the game's buttons are unlabeled coin/card hitboxes
            // ("Button", "Button", ...) — advancing is handled by Enter/Space here.
            if (_seasonPhase == SeasonPhase.NotSeason)
                DiscoverButtons(addedObjects);

            Log.Msg("{NavigatorId}", $"Discovered {_elements.Count} elements ({_rewardCount} rewards, phase={_seasonPhase}, seasonState={_seasonEndState})");
        }

        /// <summary>
        /// Discover elements for season end rank display phases (OldRank/NewRank).
        /// Extracts rank text and adds a single read-only element.
        /// </summary>
        private void DiscoverSeasonRankElements(HashSet<GameObject> addedObjects)
        {
            if (_activePopup == null) return;

            _seasonDisplayText = ExtractSeasonRankText();

            if (!string.IsNullOrEmpty(_seasonDisplayText))
            {
                Log.Msg("{NavigatorId}", $"Season rank text: {_seasonDisplayText}");
                AddElement(_activePopup, _seasonDisplayText);
                addedObjects.Add(_activePopup);
            }
        }

        /// <summary>
        /// Discover the rewards earned during the season reward-reveal phase.
        /// Reuses the standard reward-prefab discovery (card names, pack set names)
        /// with the controller-data summary as a fallback, then builds a concise
        /// spoken summary. Guarantees at least one navigable element so the screen
        /// always announces and Enter can advance even before prefabs have spawned.
        /// </summary>
        private void DiscoverSeasonRewardElements(HashSet<GameObject> addedObjects)
        {
            string title = ReadRewardsTitle();

            ExtractPackSetNames();
            DiscoverRewardElements(addedObjects);
            if (_rewardCount == 0)
                DiscoverRewardsFromControllerData(addedObjects);

            // The game only enters this phase when there ARE season rewards (with no
            // deltas it skips straight from old-rank to new-rank), so finding nothing
            // means the reveal animation hasn't spawned its prefabs yet. Arm the poll
            // (PollSeasonReveal) to re-scan once the reveal settles, and speak a
            // "revealing, please wait" placeholder in the meantime.
            _awaitingSeasonReveal = _rewardCount == 0;

            if (_awaitingSeasonReveal)
            {
                _seasonDisplayText = string.IsNullOrEmpty(title)
                    ? Strings.SeasonRevealing
                    : $"{title}. {Strings.SeasonRevealing}";

                // Keep a navigable placeholder so the navigator stays active (and Enter
                // can still advance) while we wait for the rewards to reveal.
                if (_elements.Count == 0)
                {
                    AddElement(_activePopup, title ?? Strings.ScreenRewards);
                    addedObjects.Add(_activePopup);
                }
                return;
            }

            var parts = new List<string>();
            if (!string.IsNullOrEmpty(title)) parts.Add(title);
            parts.AddRange(_elements.Select(e => e.Label));
            _seasonDisplayText = parts.Count > 0 ? string.Join(". ", parts) : title;

            if (_elements.Count == 0)
            {
                string fallback = title ?? Strings.ScreenRewards;
                AddElement(_activePopup, fallback);
                addedObjects.Add(_activePopup);
                if (string.IsNullOrEmpty(_seasonDisplayText)) _seasonDisplayText = fallback;
            }
        }

        /// <summary>
        /// Extract readable text from season end rank display.
        /// Reads title, subtitle from ContentControllerRewards, and rank details from SeasonEndRankDisplay components.
        /// </summary>
        private string ExtractSeasonRankText()
        {
            if (_activePopup == null) return null;

            var parts = new List<string>();

            try
            {
                MonoBehaviour rewardsController = null;
                foreach (var mb in _activePopup.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb != null && mb.GetType().Name == "ContentControllerRewards")
                    {
                        rewardsController = mb;
                        break;
                    }
                }
                if (rewardsController == null) return null;

                // Read title text (protected field on ContentControllerRewards)
                var titleField = rewardsController.GetType().GetField("_rewardsTitleText", PrivateInstance);
                if (titleField != null)
                {
                    var titleTmp = titleField.GetValue(rewardsController) as TMPro.TMP_Text;
                    if (titleTmp != null)
                    {
                        string title = UITextExtractor.StripRichText(titleTmp.text)?.Trim();
                        if (!string.IsNullOrEmpty(title))
                            parts.Add(title);
                    }
                }

                // Read subtitle text
                var subtitleField = rewardsController.GetType().GetField("_rewardsSubtitleText", PrivateInstance);
                if (subtitleField != null)
                {
                    var subtitleTmp = subtitleField.GetValue(rewardsController) as TMPro.TMP_Text;
                    if (subtitleTmp != null)
                    {
                        string subtitle = UITextExtractor.StripRichText(subtitleTmp.text)?.Trim();
                        if (!string.IsNullOrEmpty(subtitle))
                            parts.Add(subtitle);
                    }
                }

                // Read rank details from SeasonEndRankDisplay components
                string[] rankFieldNames = { "_endSeasonRankDisplayConstructed", "_endSeasonRankDisplayLimited" };
                foreach (string fieldName in rankFieldNames)
                {
                    var displayField = rewardsController.GetType().GetField(fieldName, PrivateInstance);
                    if (displayField == null) continue;

                    var display = displayField.GetValue(rewardsController) as MonoBehaviour;
                    if (display == null || !display.gameObject.activeInHierarchy) continue;

                    // SeasonEndRankDisplay fields: RankDisplayPreface (private TMP_Text), Details (private TMP_Text)
                    var prefaceField = display.GetType().GetField("RankDisplayPreface", PrivateInstance);
                    var detailsField = display.GetType().GetField("Details", PrivateInstance);

                    string preface = null;
                    string details = null;

                    if (prefaceField != null)
                    {
                        var prefaceTmp = prefaceField.GetValue(display) as TMPro.TMP_Text;
                        if (prefaceTmp != null)
                            preface = UITextExtractor.StripRichText(prefaceTmp.text)?.Trim();
                    }

                    if (detailsField != null)
                    {
                        var detailsTmp = detailsField.GetValue(display) as TMPro.TMP_Text;
                        if (detailsTmp != null)
                            details = UITextExtractor.StripRichText(detailsTmp.text)?.Trim();
                    }

                    if (!string.IsNullOrEmpty(preface) || !string.IsNullOrEmpty(details))
                    {
                        string rankInfo = "";
                        if (!string.IsNullOrEmpty(preface))
                            rankInfo = preface;
                        if (!string.IsNullOrEmpty(details))
                            rankInfo = string.IsNullOrEmpty(rankInfo) ? details : $"{rankInfo}: {details}";
                        parts.Add(rankInfo);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Msg("{NavigatorId}", $"ExtractSeasonRankText failed: {ex.Message}");
            }

            return parts.Count > 0 ? string.Join(". ", parts) : null;
        }

        /// <summary>
        /// Discover reward elements in the rewards popup.
        /// Searches the entire popup for reward prefabs, not just RewardsCONTAINER.
        /// </summary>
        private void DiscoverRewardElements(HashSet<GameObject> addedObjects)
        {
            if (_activePopup == null)
            {
                Log.Msg("{NavigatorId}", $"DiscoverRewardElements: No active popup");
                return;
            }

            Log.Msg("{NavigatorId}", $"DiscoverRewardElements: Searching entire popup '{_activePopup.name}' for reward prefabs");

            var rewardPrefabs = new List<(Transform prefab, string type, float sortOrder)>();

            // Find all reward prefabs in the entire popup (not just RewardsCONTAINER)
            foreach (Transform child in _activePopup.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || !child.gameObject.activeInHierarchy) continue;

                string name = child.name;
                if (!name.StartsWith("RewardPrefab_")) continue;

                // Determine reward type from prefab name pattern: RewardPrefab_<Type>(Clone)
                string rewardType = null;
                if (name.Contains("Pack"))
                    rewardType = "Pack";
                else if (name.Contains("IndividualCard"))
                    rewardType = "Card";
                else if (name.Contains("CardSleeve"))
                    rewardType = "CardSleeve";
                else if (name.Contains("Gold") || name.Contains("Gems") || name.Contains("Coins"))
                    rewardType = "Currency";
                else if (name.Contains("XP"))
                    rewardType = "XP";
                else if (name.Contains("Avatar"))
                    rewardType = "Avatar";
                else if (name.Contains("Emote"))
                    rewardType = "Emote";
                else if (name.Contains("Token"))
                    rewardType = "Token";
                else
                    rewardType = DetectRewardTypeByComponent(child.gameObject);

                // Sort by horizontal position (left to right)
                float sortOrder = child.position.x;
                rewardPrefabs.Add((child, rewardType, sortOrder));
            }

            // Sort rewards by position
            rewardPrefabs = rewardPrefabs.OrderBy(r => r.sortOrder).ToList();

            foreach (var (prefab, rewardType, sortOrder) in rewardPrefabs)
            {
                // Find click target early to skip duplicates (e.g. nested token prefabs)
                GameObject clickTarget = FindRewardClickTarget(prefab.gameObject, rewardType);
                if (clickTarget == null)
                    clickTarget = prefab.gameObject;

                if (addedObjects.Contains(clickTarget)) continue;

                _rewardCount++;

                // Debug: dump reward prefab structure
                DumpRewardPrefabStructure(prefab.gameObject, rewardType, _rewardCount);

                string label = ExtractRewardLabel(prefab.gameObject, rewardType, _rewardCount);

                // Debug: log card detection info for card rewards
                bool isCard = CardDetector.IsCard(clickTarget);
                Log.Msg("{NavigatorId}", $"Adding reward: {label} (target: {clickTarget.name}, IsCard: {isCard})");

                AddElement(clickTarget, label);
                addedObjects.Add(clickTarget);

                // Add ALL elements inside this reward prefab to addedObjects to prevent duplicates
                foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
                {
                    if (child != null && child.gameObject.activeInHierarchy)
                        addedObjects.Add(child.gameObject);
                }
            }

            Log.Msg("{NavigatorId}", $"DiscoverRewardElements: Added {_rewardCount} rewards");
        }

        /// <summary>
        /// Fallback: read reward data directly from the controller's _allRewards array
        /// when no RewardPrefab_ GameObjects were found (timeout case).
        /// Creates a summary info element listing what rewards are pending.
        /// </summary>
        private void DiscoverRewardsFromControllerData(HashSet<GameObject> addedObjects)
        {
            if (_activePopup == null) return;

            try
            {
                MonoBehaviour rewardsController = null;
                foreach (var mb in _activePopup.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb != null && mb.GetType().Name == "ContentControllerRewards")
                    {
                        rewardsController = mb;
                        break;
                    }
                }
                if (rewardsController == null) return;

                // Read _allRewards array
                var allRewardsField = rewardsController.GetType().GetField("_allRewards", PrivateInstance);
                if (allRewardsField == null) return;
                var allRewards = allRewardsField.GetValue(rewardsController) as Array;
                if (allRewards == null) return;

                var rewardParts = new List<string>();

                foreach (var reward in allRewards)
                {
                    if (reward == null) continue;

                    // Read ToAddCount property (from IRewardBase)
                    var toAddCountProp = reward.GetType().GetProperty("ToAddCount", PublicInstance);
                    if (toAddCountProp == null) continue;
                    int count = (int)toAddCountProp.GetValue(reward);
                    if (count <= 0) continue;

                    string typeName = reward.GetType().Name;

                    // Use pre-extracted set names for packs
                    if (typeName == "PackReward" && _packSetNames.Count > 0)
                    {
                        foreach (string setName in _packSetNames)
                            rewardParts.Add($"{setName} Pack");
                        continue;
                    }

                    string label = MapRewardTypeToLabel(typeName);
                    if (count > 1)
                        rewardParts.Add($"{count} {label}");
                    else
                        rewardParts.Add(label);
                }

                if (rewardParts.Count > 0)
                {
                    _rewardCount = rewardParts.Count;
                    string summary = string.Join(", ", rewardParts);
                    Log.Msg("{NavigatorId}", $"Controller data fallback: {summary}");
                    AddElement(_activePopup, summary);
                    addedObjects.Add(_activePopup);
                }
            }
            catch (Exception ex)
            {
                Log.Msg("{NavigatorId}", $"DiscoverRewardsFromControllerData failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Map reward class type name to a human-readable label.
        /// </summary>
        private static string MapRewardTypeToLabel(string typeName)
        {
            switch (typeName)
            {
                case "GoldReward": return "Gold";
                case "GemReward": return "Gems";
                case "XPReward": return "XP";
                case "PackReward": return "Booster Packs";
                case "CardReward": return "Cards";
                case "CardRewardWithBonus": return "Cards";
                case "StyleReward": return "Styles";
                case "SleeveReward": return "Sleeves";
                case "AvatarReward": return "Avatars";
                case "EmoteReward": return "Emotes";
                case "TitleReward": return "Titles";
                case "DeckBoxReward": return "Deck Box";
                case "VoucherReward": return "Voucher";
                case "EventTokenReward": return "Event Token";
                case "EventTicketReward": return "Event Ticket";
                case "MythicQualifierReward": return "Mythic Qualifier";
                case "OrbReward": return "Orb";
                case "BpOrbReward": return "BP Orb";
                case "PrizeWallTokenReward": return "Prize Wall Token";
                case "CompleteSetReward": return "Complete Set";
                case "PetReward": return "Pet";
                default: return typeName.Replace("Reward", "");
            }
        }

        /// <summary>
        /// Pre-extract pack set names from ContentControllerRewards._packReward.ToAdd data.
        /// Pack rewards display in CollationId order, so we queue names in the same order.
        /// </summary>
        private void ExtractPackSetNames()
        {
            // Reset consumption index for each discovery pass
            _packSetNameIndex = 0;

            // Only extract once - the game's ToAdd queue gets consumed after display,
            // so re-extraction on rescan would find an empty queue
            if (_packSetNames.Count > 0) return;

            if (_activePopup == null) return;

            try
            {
                // Find ContentControllerRewards component on the popup
                MonoBehaviour rewardsController = null;
                foreach (var mb in _activePopup.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb != null && mb.GetType().Name == "ContentControllerRewards")
                    {
                        rewardsController = mb;
                        break;
                    }
                }
                if (rewardsController == null) return;

                // Get _packReward field
                var packRewardField = rewardsController.GetType().GetField("_packReward", PrivateInstance);
                if (packRewardField == null) return;
                var packReward = packRewardField.GetValue(rewardsController);
                if (packReward == null) return;

                // Get ToAdd field (public Queue<InventoryBooster> on ItemReward<T, P>)
                var toAddField = packReward.GetType().GetField("ToAdd", PublicInstance);
                if (toAddField == null) return;
                var toAdd = toAddField.GetValue(packReward) as IEnumerable;
                if (toAdd == null) return;

                // Extract CollationId from each InventoryBooster, sorted by CollationId (matching display order)
                var collationIds = new List<int>();
                FieldInfo collationIdField = null;

                foreach (var booster in toAdd)
                {
                    if (booster == null) continue;
                    if (collationIdField == null)
                        collationIdField = booster.GetType().GetField("CollationId", PublicInstance);
                    if (collationIdField == null) break;

                    int collationId = (int)collationIdField.GetValue(booster);
                    collationIds.Add(collationId);
                }

                // Sort by CollationId (same order as PackReward.DisplayRewards)
                collationIds.Sort();

                // Convert CollationId to set code via CollationMapping enum ToString()
                var collationMappingType = FindType("Wotc.Mtga.Wrapper.CollationMapping");

                foreach (int collationId in collationIds)
                {
                    string setCode = null;
                    if (collationMappingType != null && Enum.IsDefined(collationMappingType, collationId))
                        setCode = Enum.ToObject(collationMappingType, collationId).ToString();

                    string setName = !string.IsNullOrEmpty(setCode)
                        ? UITextExtractor.MapSetCodeToName(setCode)
                        : null;

                    _packSetNames.Add(setName ?? $"Pack #{collationId}");
                    Log.Msg("{NavigatorId}", $"Pack set name: CollationId={collationId}, SetCode={setCode}, Name={setName}");
                }
            }
            catch (System.Exception ex)
            {
                Log.Msg("{NavigatorId}", $"ExtractPackSetNames failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Discover buttons in the rewards popup (ClaimButton, etc.).
        /// </summary>
        private void DiscoverButtons(HashSet<GameObject> addedObjects)
        {
            if (_activePopup == null) return;

            // Find CustomButtons
            foreach (var mb in _activePopup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (addedObjects.Contains(mb.gameObject)) continue;

                string typeName = mb.GetType().Name;
                if (typeName != T.CustomButton && typeName != T.CustomButtonWithTooltip) continue;

                string label = GetButtonLabel(mb.gameObject);
                Log.Msg("{NavigatorId}", $"Adding button: {label} ({mb.gameObject.name})");
                AddElement(mb.gameObject, label);
                addedObjects.Add(mb.gameObject);
            }

            // Background_ClickBlocker is NOT added as a navigable element —
            // it's an invisible dismiss overlay. User has Backspace to dismiss instead.
        }

        private string GetButtonLabel(GameObject button)
        {
            string text = UITextExtractor.GetButtonText(button, null);
            if (!string.IsNullOrEmpty(text) && text.Length < 30)
                return $"{text}, button";

            string name = button.name;
            if (name.Contains("ClaimButton") || name.Contains("Mehr"))
                return "More, button";
            if (name.Contains("Close") || name.Contains("Dismiss"))
                return "Close, button";
            if (name.Contains("Continue") || name.Contains("Done"))
                return "Continue, button";

            return "Button";
        }

        /// <summary>
        /// Debug: Dump the structure of a reward prefab to understand what data is available.
        /// </summary>
        private void DumpRewardPrefabStructure(GameObject rewardPrefab, string rewardType, int index)
        {
            Log.Msg("{NavigatorId}", $"=== REWARD PREFAB DEBUG #{index} ===");
            Log.Msg("{NavigatorId}", $"Prefab name: {rewardPrefab.name}");
            Log.Msg("{NavigatorId}", $"Detected type: {rewardType}");

            // List all TMP_Text components
            var texts = rewardPrefab.GetComponentsInChildren<TMPro.TMP_Text>(true);
            Log.Msg("{NavigatorId}", $"TMP_Text components: {texts.Length}");
            foreach (var text in texts)
            {
                if (text != null)
                {
                    string content = text.text?.Trim() ?? "(null)";
                    Log.Msg("{NavigatorId}", $"  Text in '{text.gameObject.name}': '{content}' (active: {text.gameObject.activeInHierarchy})");
                }
            }

            // List key child objects and their components
            Log.Msg("{NavigatorId}", $"Child structure (depth 3):");
            DumpChildStructure(rewardPrefab.transform, 0, 3);

            // List all MonoBehaviour types
            var monoBehaviours = rewardPrefab.GetComponentsInChildren<MonoBehaviour>(true);
            var typeNames = new HashSet<string>();
            foreach (var mb in monoBehaviours)
            {
                if (mb != null)
                    typeNames.Add(mb.GetType().Name);
            }
            Log.Msg("{NavigatorId}", $"Component types: {string.Join(", ", typeNames)}");

            Log.Msg("{NavigatorId}", $"=== END REWARD PREFAB DEBUG #{index} ===");
        }

        private void DumpChildStructure(Transform parent, int depth, int maxDepth)
        {
            if (depth >= maxDepth) return;

            string indent = new string(' ', depth * 2);
            foreach (Transform child in parent)
            {
                if (child == null) continue;
                string activeStatus = child.gameObject.activeInHierarchy ? "" : " [INACTIVE]";
                Log.Msg("{NavigatorId}", $"  {indent}- {child.name}{activeStatus}");
                DumpChildStructure(child, depth + 1, maxDepth);
            }
        }

        /// <summary>
        /// Extract a readable label for a reward prefab.
        /// Copied exactly from GeneralMenuNavigator.ExtractRewardLabel().
        /// </summary>
        private string ExtractRewardLabel(GameObject rewardPrefab, string rewardType, int index)
        {
            switch (rewardType)
            {
                case "Card":
                    _cardIndex++;
                    var cardObj = FindCardObjectInReward(rewardPrefab);
                    if (cardObj != null)
                    {
                        var cardInfo = CardDetector.ExtractCardInfo(cardObj);
                        if (cardInfo.IsValid && !string.IsNullOrEmpty(cardInfo.Name))
                        {
                            string cardLabel = $"Card {_cardIndex}: {cardInfo.Name}";
                            if (!string.IsNullOrEmpty(cardInfo.TypeLine))
                                cardLabel += $", {cardInfo.TypeLine}";
                            return cardLabel;
                        }
                    }
                    var cardTexts = rewardPrefab.GetComponentsInChildren<TMPro.TMP_Text>(true);
                    foreach (var text in cardTexts)
                    {
                        if (text != null && text.gameObject.activeInHierarchy)
                        {
                            string content = text.text?.Trim();
                            if (!string.IsNullOrEmpty(content) && content != "+99" && !content.StartsWith("+"))
                                return $"Card {_cardIndex}: {content}";
                        }
                    }
                    return $"Card {_cardIndex}";

                case "Pack":
                    // Use pre-extracted set name from controller data
                    if (_packSetNameIndex < _packSetNames.Count)
                    {
                        string setName = _packSetNames[_packSetNameIndex++];
                        // Check for count text (e.g., "x3")
                        string countText = null;
                        var packTexts = rewardPrefab.GetComponentsInChildren<TMPro.TMP_Text>(true);
                        foreach (var text in packTexts)
                        {
                            if (text != null && text.gameObject.activeInHierarchy)
                            {
                                string content = text.text?.Trim();
                                if (!string.IsNullOrEmpty(content) && content.StartsWith("x"))
                                {
                                    countText = content;
                                    break;
                                }
                            }
                        }
                        return countText != null ? $"{setName} Pack {countText}" : $"{setName} Pack";
                    }
                    // Fallback: no pre-extracted set name, check for count text
                    var fallbackTexts = rewardPrefab.GetComponentsInChildren<TMPro.TMP_Text>(true);
                    foreach (var text in fallbackTexts)
                    {
                        if (text != null && text.gameObject.activeInHierarchy)
                        {
                            string content = text.text?.Trim();
                            if (!string.IsNullOrEmpty(content) && content.StartsWith("x"))
                                return $"Booster Pack {content}";
                        }
                    }
                    return "Booster Pack";

                case "CardSleeve":
                    var sleeveTexts = rewardPrefab.GetComponentsInChildren<TMPro.TMP_Text>(true);
                    foreach (var text in sleeveTexts)
                    {
                        if (text != null && text.gameObject.activeInHierarchy)
                        {
                            string content = text.text?.Trim();
                            if (!string.IsNullOrEmpty(content))
                                return $"Card Sleeve: {content}";
                        }
                    }
                    return $"Card Sleeve {index}";

                case "Currency":
                    // Determine currency type from prefab name
                    string currencyType = "Gold";
                    if (rewardPrefab.name.Contains("Gems"))
                        currencyType = "Gems";
                    else if (rewardPrefab.name.Contains("Coins") || rewardPrefab.name.Contains("Gold"))
                        currencyType = "Gold";

                    // Find quantity in Text_Quantity (may be inactive — text is set before GO activates)
                    var currencyTexts = rewardPrefab.GetComponentsInChildren<TMPro.TMP_Text>(true);
                    foreach (var text in currencyTexts)
                    {
                        if (text != null && text.gameObject.name == "Text_Quantity")
                        {
                            string quantity = text.text?.Trim();
                            if (!string.IsNullOrEmpty(quantity) && quantity != "x0")
                                return $"{quantity} {currencyType}";
                        }
                    }
                    return currencyType;

                case "XP":
                    // Find quantity in Text_Quantity (may be inactive — text is set before GO activates)
                    var xpTexts = rewardPrefab.GetComponentsInChildren<TMPro.TMP_Text>(true);
                    foreach (var text in xpTexts)
                    {
                        if (text != null && text.gameObject.name == "Text_Quantity")
                        {
                            string quantity = text.text?.Trim();
                            if (!string.IsNullOrEmpty(quantity) && quantity != "x0")
                                return $"{quantity} XP";
                        }
                    }
                    return "XP";

                case "Emote":
                    var emoteTexts = rewardPrefab.GetComponentsInChildren<TMPro.TMP_Text>(true);
                    foreach (var text in emoteTexts)
                    {
                        if (text != null && text.gameObject.activeInHierarchy &&
                            text.gameObject.name == "Text - Speech")
                        {
                            string emoteName = text.text?.Trim();
                            if (!string.IsNullOrEmpty(emoteName))
                                return $"Emote: {emoteName}";
                        }
                    }
                    return $"Emote {index}";

                case "Avatar":
                    var avatarTexts = rewardPrefab.GetComponentsInChildren<TMPro.TMP_Text>(true);
                    foreach (var text in avatarTexts)
                    {
                        if (text != null && text.gameObject.activeInHierarchy)
                        {
                            string content = text.text?.Trim();
                            if (!string.IsNullOrEmpty(content) && content.Length > 1)
                                return $"Avatar: {content}";
                        }
                    }
                    return $"Avatar {index}";

                case "Pet":
                    var petTexts = rewardPrefab.GetComponentsInChildren<TMPro.TMP_Text>(true);
                    foreach (var text in petTexts)
                    {
                        if (text != null && text.gameObject.name == "Text_Quantity")
                        {
                            string petName = text.text?.Trim();
                            if (!string.IsNullOrEmpty(petName))
                                return $"Pet: {petName}";
                        }
                    }
                    return $"Pet {index}";

                case "Token":
                    var tokenTexts = rewardPrefab.GetComponentsInChildren<TMPro.TMP_Text>(true);
                    foreach (var text in tokenTexts)
                    {
                        if (text != null && text.gameObject.activeInHierarchy &&
                            text.gameObject.name == "Text_Title")
                        {
                            string tokenTitle = text.text?.Trim();
                            if (!string.IsNullOrEmpty(tokenTitle))
                                return tokenTitle;
                        }
                    }
                    return $"Token {index}";

                default:
                    // Try to extract name from Text_Note (may be inactive, e.g. Mastery Orb)
                    string rewardName = null;
                    string rewardQuantity = null;
                    var defaultTexts = rewardPrefab.GetComponentsInChildren<TMPro.TMP_Text>(true);
                    foreach (var text in defaultTexts)
                    {
                        if (text == null) continue;
                        string content = text.text?.Trim();
                        if (string.IsNullOrEmpty(content)) continue;

                        if (text.gameObject.name == "Text_Note")
                            rewardName = content;
                        else if (text.gameObject.name == "Text_Quantity" && text.gameObject.activeInHierarchy)
                            rewardQuantity = content;
                    }

                    // Fallback: extract type from prefab name (RewardPrefab_<Type>_...)
                    if (string.IsNullOrEmpty(rewardName))
                    {
                        string prefabName = rewardPrefab.name;
                        if (prefabName.StartsWith("RewardPrefab_"))
                        {
                            string extracted = prefabName.Substring("RewardPrefab_".Length);
                            int sep = extracted.IndexOf('_');
                            if (sep > 0) extracted = extracted.Substring(0, sep);
                            int paren = extracted.IndexOf('(');
                            if (paren > 0) extracted = extracted.Substring(0, paren);
                            if (extracted.Length > 1)
                                rewardName = extracted;
                        }
                    }

                    if (!string.IsNullOrEmpty(rewardName))
                        return !string.IsNullOrEmpty(rewardQuantity) ? $"{rewardQuantity} {rewardName}" : rewardName;

                    return $"Reward {index}";
            }
        }

        /// <summary>
        /// Detect reward type by checking component types when prefab name doesn't match known patterns.
        /// Used for rewards like pets whose prefab names vary (e.g. RewardPrefab_M20Tentpole_Base).
        /// </summary>
        private static string DetectRewardTypeByComponent(GameObject prefab)
        {
            foreach (var mb in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                switch (mb.GetType().Name)
                {
                    case "PetRewardDisplay": return "Pet";
                }
            }
            return "Reward";
        }

        // TryGetPackNameFromReward removed - NotificationPopupReward has no reward data fields.
        // Pack names are now pre-extracted from ContentControllerRewards._packReward.ToAdd
        // via ExtractPackSetNames() during discovery.

        /// <summary>
        /// Find the actual card object inside a reward prefab.
        /// Copied exactly from GeneralMenuNavigator.FindCardObjectInReward().
        /// </summary>
        private GameObject FindCardObjectInReward(GameObject rewardPrefab)
        {
            foreach (var mb in rewardPrefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;

                string typeName = mb.GetType().Name;
                if (typeName == T.BoosterMetaCardView ||
                    typeName == T.RewardDisplayCard ||
                    typeName == T.PagesMetaCardView ||
                    typeName == T.MetaCardView ||
                    typeName == T.MetaCDC ||
                    typeName == T.CardView ||
                    typeName == T.DuelCardView)
                {
                    return mb.gameObject;
                }
            }

            var cardAnchor = rewardPrefab.transform.Find("CardAnchor");
            if (cardAnchor != null)
            {
                foreach (Transform child in cardAnchor.GetComponentsInChildren<Transform>(true))
                {
                    if (child.gameObject.activeInHierarchy && CardDetector.IsCard(child.gameObject))
                        return child.gameObject;
                }
            }

            return null;
        }

        /// <summary>
        /// Find a clickable target within a reward prefab.
        /// Copied exactly from GeneralMenuNavigator.FindRewardClickTarget().
        /// </summary>
        private GameObject FindRewardClickTarget(GameObject rewardPrefab, string rewardType = null)
        {
            if (rewardType == "Card")
            {
                var cardObj = FindCardObjectInReward(rewardPrefab);
                if (cardObj != null)
                    return cardObj;
            }

            foreach (Transform child in rewardPrefab.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || !child.gameObject.activeInHierarchy) continue;

                string name = child.name;
                if (name == "Hitbox_Middle" || name.Contains("Hitbox"))
                {
                    foreach (var mb in child.GetComponents<MonoBehaviour>())
                    {
                        string typeName = mb?.GetType().Name;
                        if (typeName == T.CustomButton || typeName == T.CustomButtonWithTooltip)
                            return child.gameObject;
                    }
                }
            }

            foreach (var mb in rewardPrefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                string typeName = mb?.GetType().Name;
                if (mb != null && mb.gameObject.activeInHierarchy &&
                    (typeName == T.CustomButton || typeName == T.CustomButtonWithTooltip))
                    return mb.gameObject;
            }

            var cardAnchor = rewardPrefab.transform.Find("CardAnchor");
            if (cardAnchor != null && cardAnchor.gameObject.activeInHierarchy)
            {
                foreach (var mb in cardAnchor.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    string typeName = mb?.GetType().Name;
                    if (mb != null && mb.gameObject.activeInHierarchy &&
                        (typeName == T.CustomButton || typeName == T.CustomButtonWithTooltip))
                        return mb.gameObject;
                }
            }

            return null;
        }

        #endregion

        #region Input Handling

        public override string GetTutorialHint() => LocaleManager.Instance.Get("RewardPopupHint");

        protected override string GetActivationAnnouncement()
        {
            // Season phases (rank display or reward reveal): announce the extracted
            // text with the season-specific "Press Enter to continue" hint.
            if (_seasonPhase != SeasonPhase.NotSeason)
            {
                string text = _seasonDisplayText ?? Strings.ScreenRewards;
                // While the reveal is still animating, _seasonDisplayText already ends
                // with "Revealing, please wait" — don't append "Press Enter to continue".
                if (_awaitingSeasonReveal)
                    return text;
                return Strings.WithHint(text, "SeasonEndHint");
            }

            // Standard rewards
            string rewardInfo = _rewardCount > 0 ? $" {_rewardCount} rewards." : "";
            string core = $"Rewards.{rewardInfo}".TrimEnd();
            return Strings.WithHint(core, "RewardPopupHint");
        }

        protected override void HandleInput()
        {
            if (HandleCustomInput()) return;

            // Season-end screens use a dedicated, single-action flow.
            if (_seasonPhase != SeasonPhase.NotSeason)
            {
                HandleSeasonInput();
                return;
            }

            // Left/Right navigation (hold-to-repeat)
            if (_holdRepeater.Check(KeyCode.LeftArrow, () => MovePrevious())) return;
            if (_holdRepeater.Check(KeyCode.RightArrow, () => MoveNext())) return;

            // Home/End
            if (Input.GetKeyDown(KeyCode.Home))
            {
                MoveFirst();
                return;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                MoveLast();
                return;
            }

            // Tab navigation
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                bool shiftTab = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (shiftTab)
                    MovePrevious();
                else
                    MoveNext();
                return;
            }

            // Enter activates current element
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (IsValidIndex)
                {
                    var elem = _elements[_currentIndex];
                    Log.Msg("{NavigatorId}", $"Activating: {elem.Label}");
                    UIActivator.Activate(elem.GameObject);

                    // Always rescan after activation - claiming a reward destroys the
                    // GameObject, which leaves stale references in _elements
                    Log.Msg("{NavigatorId}", $"Scheduling rescan after activation");
                    ForceRescan();
                }
                return;
            }

            // Backspace dismisses the popup
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                DismissRewardsPopup();
                return;
            }
        }

        /// <summary>
        /// Input handling for the end-of-season sequence (old rank → rewards → new
        /// placement). One clear action: Enter/Space advances to the next screen.
        /// Left/Right still browse multiple reward items when present.
        /// </summary>
        private void HandleSeasonInput()
        {
            // Browse reward items if there is more than one
            if (_holdRepeater.Check(KeyCode.LeftArrow, () => MovePrevious())) return;
            if (_holdRepeater.Check(KeyCode.RightArrow, () => MoveNext())) return;
            if (Input.GetKeyDown(KeyCode.Home)) { MoveFirst(); return; }
            if (Input.GetKeyDown(KeyCode.End)) { MoveLast(); return; }

            // Enter/Space = advance. Consume so the key never double-fires into the
            // game's own reward controller (which would advance twice / skip a screen).
            bool enter = InputManager.GetKeyDownAndConsume(KeyCode.Return)
                       | InputManager.GetKeyDownAndConsume(KeyCode.KeypadEnter);
            bool space = InputManager.GetKeyDownAndConsume(KeyCode.Space);
            if (enter || space)
            {
                if (IsSeasonRevealBusy())
                {
                    // The game absorbs clicks while revealing (stuck counter) — tell
                    // the user to wait rather than letting a press appear to do nothing.
                    _announcer.AnnounceInterrupt(Strings.SeasonRevealing);
                }
                else
                {
                    AdvanceSeason();
                }
                return;
            }

            // Backspace re-reads the current screen. The season sequence can't be
            // dismissed mid-way (a background click just advances), so treating
            // Backspace as "dismiss" would be misleading and could skip unread info.
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                _lastSeasonAnnouncement = null; // force re-announce
                _announcer.AnnounceInterrupt(GetActivationAnnouncement());
                return;
            }
        }

        /// <summary>
        /// Advance the end-of-season sequence to the next screen by clicking the
        /// background blocker (the game maps that to "continue" / advance phase).
        /// </summary>
        private void AdvanceSeason()
        {
            Log.Msg("{NavigatorId}", $"Advancing season screen (phase={_seasonPhase})");
            _announcer.Announce(Strings.Continuing, AnnouncementPriority.Normal);
            _lastSeasonAnnouncement = null; // let the next phase announce even if text repeats
            _awaitingSeasonReveal = false;  // leaving this phase — drop any pending reveal poll
            _seasonRevealPrefabSince = -1f;
            ClickBackgroundBlocker();
            ForceRescan();
        }

        /// <summary>
        /// Dismiss the rewards popup by clicking the Background_ClickBlocker.
        /// Copied exactly from GeneralMenuNavigator.DismissRewardsPopup().
        /// </summary>
        private bool DismissRewardsPopup()
        {
            Log.Msg("{NavigatorId}", $"Dismissing rewards popup");

            if (ClickBackgroundBlocker())
            {
                _announcer.Announce(Strings.Continuing, AnnouncementPriority.Normal);
                ForceRescan();
                return true;
            }

            var screenspacePopups = GameObject.Find("Canvas - Screenspace Popups");
            if (screenspacePopups == null)
            {
                Log.Msg("{NavigatorId}", $"Canvas - Screenspace Popups not found");
                return false;
            }

            Transform rewardsController = null;
            foreach (Transform child in screenspacePopups.transform)
            {
                if (child.name.Contains("ContentController") && child.name.Contains("Rewards") &&
                    child.gameObject.activeInHierarchy)
                {
                    rewardsController = child;
                    break;
                }
            }

            if (rewardsController == null)
            {
                Log.Msg("{NavigatorId}", $"Rewards controller not found");
                return false;
            }

            var dismissButton = rewardsController.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => (t.name.Contains("Dismiss") || t.name.Contains("Close") ||
                                      t.name.Contains("Continue") || t.name.Contains("Back")) &&
                                     t.gameObject.activeInHierarchy);

            if (dismissButton != null)
            {
                Log.Msg("{NavigatorId}", $"Clicking {dismissButton.name} to dismiss rewards popup");
                _announcer.Announce(Strings.Continuing, AnnouncementPriority.Normal);
                UIActivator.Activate(dismissButton.gameObject);
                ForceRescan();
                return true;
            }

            Log.Msg("{NavigatorId}", $"No dismiss element found in rewards popup");
            return false;
        }

        /// <summary>
        /// Click the active Background_ClickBlocker inside the rewards controller.
        /// The game routes this to OnBackgroundClicked_Unity → OnClaimClicked_Unity,
        /// which advances the reward/season sequence. Returns false if not found.
        /// </summary>
        private bool ClickBackgroundBlocker()
        {
            var screenspacePopups = GameObject.Find("Canvas - Screenspace Popups");
            if (screenspacePopups == null) return false;

            Transform rewardsController = null;
            foreach (Transform child in screenspacePopups.transform)
            {
                if (child.name.Contains("ContentController") && child.name.Contains("Rewards") &&
                    child.gameObject.activeInHierarchy)
                {
                    rewardsController = child;
                    break;
                }
            }
            if (rewardsController == null) return false;

            var clickBlocker = rewardsController.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "Background_ClickBlocker" && t.gameObject.activeInHierarchy);
            if (clickBlocker == null) return false;

            Log.Msg("{NavigatorId}", $"Clicking Background_ClickBlocker");
            UIActivator.SimulatePointerClick(clickBlocker.gameObject);
            return true;
        }

        #endregion

        #region ForceRescan override

        /// <summary>
        /// Override ForceRescan to suppress duplicate announcements.
        /// Only announces if the element count changed since last rescan.
        /// </summary>
        public override void ForceRescan()
        {
            if (!_isActive) return;

            Log.Msg("{NavigatorId}", $"ForceRescan triggered");

            _elements.Clear();
            _currentIndex = -1;

            DiscoverElements();

            if (_elements.Count > 0)
            {
                _currentIndex = 0;
                Log.Msg("{NavigatorId}", $"Rescan found {_elements.Count} elements");
                UpdateEventSystemSelection();

                string announcement = GetActivationAnnouncement();

                if (_seasonPhase != SeasonPhase.NotSeason)
                {
                    // Season screens: dedup on the spoken text (element counts flip
                    // 1↔9↔1 between phases, so count-based dedup re-announces every time).
                    if (announcement != _lastSeasonAnnouncement)
                    {
                        _lastSeasonAnnouncement = announcement;
                        _announcer.AnnounceInterrupt(announcement);
                    }
                }
                else if (_elements.Count != _lastRescanElementCount)
                {
                    _lastRescanElementCount = _elements.Count;
                    _announcer.AnnounceInterrupt(announcement);
                }
            }
            else
            {
                Log.Msg("{NavigatorId}", $"Rescan found no elements");
            }
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            _lastRescanElementCount = 0;
            // Seed the season dedup with the text TryActivate is about to speak, so the
            // first post-activation rescan with identical text doesn't repeat it.
            _lastSeasonAnnouncement = _seasonPhase != SeasonPhase.NotSeason
                ? GetActivationAnnouncement()
                : null;
        }

        #endregion

        #region Lifecycle

        protected override bool ValidateElements()
        {
            if (_activePopup == null || !_activePopup.activeInHierarchy)
            {
                Log.Msg("{NavigatorId}", $"Popup no longer active");
                return false;
            }

            return base.ValidateElements();
        }

        public override void OnSceneChanged(string sceneName)
        {
            if (_isActive)
            {
                Deactivate();
            }
            // Only reset _activePopup — forces re-detection of the popup.
            // Do NOT clear _packSetNames here: the reward popup (Screenspace Popups canvas)
            // survives scene changes, and the ToAdd queue is already consumed by then.
            // Pack names are cleared by ClearPopupState() when the popup actually disappears.
            _activePopup = null;
            _seasonEndState = 0;
            _seasonPhase = SeasonPhase.NotSeason;
            _lastSeasonAnnouncement = null;
            _awaitingSeasonReveal = false;
            _seasonRevealPrefabSince = -1f;
            _popupDetectedTime = -1f;
            _timeoutFallbackFired = false;
            _revealingWasSeen = false;
        }

        #endregion
    }
}
