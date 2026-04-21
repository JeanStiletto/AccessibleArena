using System;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Utils;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    public static partial class UITextExtractor
    {
        /// <summary>
        /// Extracts text from objective/quest elements with full context.
        /// For quests: includes description + progress (e.g., "Cast 20 spells, 14/20")
        /// For progress indicators: adds type prefix (e.g., "Daily: 250", "Weekly: 5/15")
        /// For wildcard progress: shows rarity and progress (e.g., "Rare Wildcard: 3/6")
        /// </summary>
        private static string TryGetObjectiveText(GameObject gameObject)
        {
            if (gameObject == null || gameObject.name != "ObjectiveGraphics")
                return null;

            var parent = gameObject.transform.parent;
            if (parent == null)
                return null;

            string parentName = parent.name;

            // Check for wildcard progress first (on Packs screen)
            // Parent names: "WildcardProgressUncommon", "Wildcard Progress Rare"
            if (parentName.Contains("WildcardProgress") || parentName.Contains("Wildcard Progress"))
            {
                return TryGetWildcardProgressText(gameObject, parentName);
            }

            // Extract objective type from parent name
            // Format: "Objective_Base(Clone) - QuestNormal" or "Objective_Base(Clone) - Daily"
            string objectiveType = null;
            int dashIndex = parentName.IndexOf(" - ");
            if (dashIndex >= 0 && dashIndex + 3 < parentName.Length)
            {
                objectiveType = parentName.Substring(dashIndex + 3).Trim();
            }

            // Achievement objectives: description is in Text_Description under TextLine (inactive).
            // Read it with includeInactive flag to get the achievement name.
            if (objectiveType == "Achievement")
            {
                string description = null;
                string progress = null;

                for (int i = 0; i < gameObject.transform.childCount; i++)
                {
                    var child = gameObject.transform.GetChild(i);
                    string childName = child.name;

                    if (childName == "TextLine")
                    {
                        // Text_Description is inside TextLine but the whole subtree is inactive
                        var tmpText = child.GetComponentInChildren<TMP_Text>(true);
                        if (tmpText != null)
                            description = CleanText(tmpText.text);
                    }
                    else if (childName == "Text_GoalProgress")
                    {
                        var tmpText = child.GetComponentInChildren<TMP_Text>();
                        if (tmpText != null)
                            progress = CleanText(tmpText.text);
                    }
                }

                string achieveLabel = LocaleManager.Instance?.Get("ObjectiveAchievement") ?? "Achievement";
                if (!string.IsNullOrEmpty(description))
                {
                    if (!string.IsNullOrEmpty(progress))
                        return $"{achieveLabel}: {description}, {progress}";
                    return $"{achieveLabel}: {description}";
                }
                if (!string.IsNullOrEmpty(progress))
                    return $"{achieveLabel}: {progress}";
            }

            // For quest objectives (QuestNormal), get description + progress
            if (objectiveType == "QuestNormal")
            {
                string description = null;
                string progress = null;
                string reward = null;

                // Look for TextLine (description), Text_GoalProgress (progress), Circle (reward)
                for (int i = 0; i < gameObject.transform.childCount; i++)
                {
                    var child = gameObject.transform.GetChild(i);
                    string childName = child.name;

                    if (childName == "TextLine")
                    {
                        var tmpText = child.GetComponentInChildren<TMP_Text>();
                        if (tmpText != null)
                            description = CleanText(tmpText.text);
                    }
                    else if (childName == "Text_GoalProgress")
                    {
                        var tmpText = child.GetComponentInChildren<TMP_Text>();
                        if (tmpText != null)
                            progress = CleanText(tmpText.text);
                    }
                    else if (childName == "Circle")
                    {
                        var tmpText = child.GetComponentInChildren<TMP_Text>();
                        if (tmpText != null)
                            reward = CleanText(tmpText.text);
                    }
                }

                // Build the label: "Quest description, progress, reward"
                if (!string.IsNullOrEmpty(description))
                {
                    var parts = new System.Collections.Generic.List<string> { description };
                    if (!string.IsNullOrEmpty(progress))
                        parts.Add(progress);
                    // Try popup HeaderString2 for fully localized reward text (handles gold, gems, packs)
                    string popupReward = TryGetObjectiveBubblePopupText(gameObject, "HeaderString2");
                    if (!string.IsNullOrEmpty(popupReward))
                        parts.Add(popupReward);
                    else if (!string.IsNullOrEmpty(reward))
                    {
                        string goldLabel = LocaleManager.Instance?.Get("CurrencyGold") ?? "Gold";
                        parts.Add($"{reward} {goldLabel}");
                    }
                    return string.Join(", ", parts);
                }
            }
            // For other objective types (Daily, Weekly, BattlePass), add type prefix
            else if (!string.IsNullOrEmpty(objectiveType))
            {
                string mainValue = null;
                string progressValue = null;
                string description = null;

                // Look for Circle (main display), Text_GoalProgress (detailed progress), TextLine (description)
                for (int i = 0; i < gameObject.transform.childCount; i++)
                {
                    var child = gameObject.transform.GetChild(i);
                    string childName = child.name;

                    if (childName == "Circle")
                    {
                        var tmpText = child.GetComponentInChildren<TMP_Text>();
                        if (tmpText != null)
                            mainValue = CleanText(tmpText.text);
                    }
                    else if (childName == "Text_GoalProgress")
                    {
                        var tmpText = child.GetComponentInChildren<TMP_Text>();
                        if (tmpText != null)
                            progressValue = CleanText(tmpText.text);
                    }
                    else if (childName == "TextLine")
                    {
                        var tmpText = child.GetComponentInChildren<TMP_Text>(true);
                        if (tmpText != null)
                            description = CleanText(tmpText.text);
                    }
                }

                // Clean up type names for readability — use localized labels
                string typeLabel = objectiveType;
                if (objectiveType == "BattlePass - Level")
                    typeLabel = LocaleManager.Instance?.Get("ObjectiveBattlePassLevel") ?? "Battle Pass Level";
                else if (objectiveType == "SparkRankTier1")
                    typeLabel = "Spark Rank";
                else if (objectiveType == "Daily")
                    typeLabel = LocaleManager.Instance?.Get("ObjectiveDaily") ?? "Daily";
                else if (objectiveType == "Weekly")
                    typeLabel = LocaleManager.Instance?.Get("ObjectiveWeekly") ?? "Weekly";
                else if (objectiveType == "Timer")
                {
                    // Empty quest slot with countdown — read popup text from ObjectiveBubble
                    // ObjectiveBubble._popupData holds localization keys for the popup:
                    //   HeaderString1 = "MainNav/Quest/Quest_Wait_Text"
                    //   FooterString  = "MainNav/Popups/QuestRewardPopupDetailsForWaiting"
                    string header = TryGetObjectiveBubblePopupText(gameObject, "HeaderString1");
                    string footer = TryGetObjectiveBubblePopupText(gameObject, "FooterString");

                    // Use game-localized header as label, fall back to our own locale string
                    string label = !string.IsNullOrEmpty(header)
                        ? header
                        : LocaleManager.Instance?.Get("ObjectiveQuestTimer") ?? "Next quest available soon";

                    if (!string.IsNullOrEmpty(footer) && footer != header)
                        label += $", {footer}";

                    return label;
                }

                // Build label based on objective type
                string winsLabel = LocaleManager.Instance?.Get("ObjectiveWins") ?? "wins";
                string goldLabel = LocaleManager.Instance?.Get("CurrencyGold") ?? "Gold";

                if (objectiveType == "Daily")
                {
                    // If game provides a TextLine description, use it.
                    // Otherwise fall back to the win-count format (Daily is always win-based).
                    var parts = new System.Collections.Generic.List<string>();
                    if (!string.IsNullOrEmpty(description))
                        parts.Add(description);
                    if (!string.IsNullOrEmpty(progressValue))
                        parts.Add(string.IsNullOrEmpty(description) ? $"{progressValue} {winsLabel}" : progressValue);
                    // Try popup HeaderString2 for localized reward, fall back to our locale
                    string popupReward = TryGetObjectiveBubblePopupText(gameObject, "HeaderString2");
                    if (!string.IsNullOrEmpty(popupReward))
                        parts.Add(popupReward);
                    else if (!string.IsNullOrEmpty(mainValue))
                        parts.Add($"{mainValue} {goldLabel}");
                    if (parts.Count > 0)
                        return $"{typeLabel}: {string.Join(", ", parts)}";
                }
                else if (objectiveType == "BattlePass - Level")
                {
                    // BattlePass: "Level 7, 400/1000 EP"
                    if (!string.IsNullOrEmpty(mainValue) && !string.IsNullOrEmpty(progressValue))
                        return $"{typeLabel}: {mainValue}, {progressValue}";
                    else if (!string.IsNullOrEmpty(mainValue))
                        return $"{typeLabel}: {mainValue}";
                }
                else
                {
                    // Weekly, SparkRank, etc: prefer description from TextLine, fall back to progress.
                    // Weekly is also win-based, so append localized "wins" when no description is available.
                    var parts = new System.Collections.Generic.List<string>();
                    if (!string.IsNullOrEmpty(description))
                        parts.Add(description);
                    if (!string.IsNullOrEmpty(progressValue))
                        parts.Add(string.IsNullOrEmpty(description) && objectiveType == "Weekly" ? $"{progressValue} {winsLabel}" : progressValue);
                    else if (!string.IsNullOrEmpty(mainValue))
                        parts.Add(mainValue);
                    if (parts.Count > 0)
                        return $"{typeLabel}: {string.Join(", ", parts)}";

                    // Fallback: scan all TMP_Text children (including inactive) for any readable text
                    var texts = gameObject.GetComponentsInChildren<TMP_Text>(true);
                    var fallbackParts = new System.Collections.Generic.List<string>();
                    foreach (var t in texts)
                    {
                        string v = CleanText(t.text);
                        if (!string.IsNullOrWhiteSpace(v) && !fallbackParts.Contains(v))
                            fallbackParts.Add(v);
                    }
                    if (fallbackParts.Count > 0)
                        return $"{typeLabel}: {string.Join(", ", fallbackParts)}";
                }
            }

            return null;
        }

        /// <summary>
        /// Extracts text from wildcard progress elements on the Packs screen.
        /// These show progress toward earning wildcards of specific rarities.
        /// Parent names: "WildcardProgressUncommon", "Wildcard Progress Rare"
        /// </summary>
        private static string TryGetWildcardProgressText(GameObject gameObject, string parentName)
        {
            // Extract rarity from parent name
            string rarity = null;
            string parentLower = parentName.ToLowerInvariant();
            if (parentLower.Contains("uncommon"))
                rarity = "Uncommon";
            else if (parentLower.Contains("rare"))
                rarity = "Rare";
            else if (parentLower.Contains("mythic"))
                rarity = "Mythic";
            else if (parentLower.Contains("common"))
                rarity = "Common";

            // Look for progress value in children (same structure as objectives)
            string progressValue = null;
            string fillPercentage = null;

            // Search all child transforms for text elements
            var allTexts = gameObject.GetComponentsInChildren<TMP_Text>(true);
            foreach (var tmpText in allTexts)
            {
                if (tmpText == null) continue;

                string objName = tmpText.gameObject.name;
                string content = CleanText(tmpText.text);

                if (string.IsNullOrEmpty(content)) continue;

                // Text_GoalProgress contains the fraction (e.g., "3/6")
                if (objName == "Text_GoalProgress" || objName.Contains("GoalProgress"))
                {
                    progressValue = content;
                }
                // TextLine may contain additional text
                else if (objName == "TextLine" || objName.Contains("TextLine"))
                {
                    // If it looks like a progress value, use it
                    if (content.Contains("/"))
                        progressValue = content;
                }
            }

            // Also check for Image fill amount as a fallback for percentage
            var images = gameObject.GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img == null) continue;
                string imgName = img.gameObject.name.ToLowerInvariant();
                if (imgName.Contains("fill") || imgName.Contains("progress"))
                {
                    if (img.type == Image.Type.Filled && img.fillAmount > 0 && img.fillAmount < 1)
                    {
                        int percent = Mathf.RoundToInt(img.fillAmount * 100);
                        fillPercentage = $"{percent}%";
                    }
                }
            }

            // Build the label
            string label = rarity != null ? $"{rarity} Wildcard" : "Wildcard";

            if (!string.IsNullOrEmpty(progressValue))
                return $"{label}: {progressValue}";
            else if (!string.IsNullOrEmpty(fillPercentage))
                return $"{label}: {fillPercentage}";
            else
                return label;
        }

        /// <summary>
        /// Extracts text from NPE (New Player Experience) objective elements.
        /// These are the tutorial stage indicators (Stage I, II, III, etc.) with completion status.
        /// Uses the NPEObjective component's Animator to reliably detect state
        /// (completed vs unlocked vs locked) and returns fully localized text.
        /// </summary>
        private sealed class NpeObjectiveHandles
        {
            public FieldInfo CircleText;
            public FieldInfo Animator;
        }

        private static Type _npeObjectiveType;
        private static bool _npeObjectiveTypeSearched;

        private static readonly ReflectionCache<NpeObjectiveHandles> _npeObjectiveCache = new ReflectionCache<NpeObjectiveHandles>(
            builder: t => new NpeObjectiveHandles
            {
                CircleText = t.GetField("_circleText", PrivateInstance),
                Animator = t.GetField("_animator", PrivateInstance),
            },
            validator: _ => true,
            logTag: "UITextExtractor",
            logSubject: "NPEObjective");

        private static string TryGetNPEObjectiveText(GameObject gameObject)
        {
            if (gameObject == null || !gameObject.name.StartsWith("Objective_NPE"))
                return null;

            if (!_npeObjectiveTypeSearched)
            {
                _npeObjectiveTypeSearched = true;
                _npeObjectiveType = FindType("NPEObjective");
            }

            NpeObjectiveHandles h = null;
            if (_npeObjectiveType != null && _npeObjectiveCache.EnsureInitialized(_npeObjectiveType))
                h = _npeObjectiveCache.Handles;

            // Get NPEObjective component
            Component npeObjective = null;
            if (_npeObjectiveType != null)
                npeObjective = gameObject.GetComponent(_npeObjectiveType);

            // Read roman numeral from _circleText (works even when inactive)
            string roman = null;
            if (npeObjective != null && h?.CircleText != null)
            {
                var tmp = h.CircleText.GetValue(npeObjective) as TMP_Text;
                if (tmp != null)
                {
                    string content = tmp.text?.Trim();
                    if (!string.IsNullOrEmpty(content) && content != "\u200B")
                        roman = StripRichText(content).Trim();
                }
            }

            // Fallback: scan TMP_Text children for roman numeral
            if (string.IsNullOrEmpty(roman))
            {
                foreach (var text in gameObject.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text == null) continue;
                    string content = text.text?.Trim();
                    if (string.IsNullOrEmpty(content)) continue;
                    content = StripRichText(content).Trim();
                    if (Regex.IsMatch(content, @"^[IVX]+$"))
                    {
                        roman = content;
                        break;
                    }
                }
            }

            // Determine state from Animator (pure reflection, same pattern as EventAccessor)
            string status = null;
            if (npeObjective != null && h?.Animator != null)
            {
                var animator = h.Animator.GetValue(npeObjective);
                if (animator != null)
                    status = GetNPEObjectiveStatus(animator);
            }

            // Build localized label
            string stageLabel = !string.IsNullOrEmpty(roman)
                ? Strings.NPE_Stage(roman) : Strings.NPE_Stage("?");

            if (!string.IsNullOrEmpty(status))
                return $"{stageLabel}, {status}";

            return stageLabel;
        }

        /// <summary>
        /// Determines the NPE objective status from its Animator via reflection.
        /// NPEObjective uses SetTrigger (not SetBool), so we check state names first,
        /// then fall back to GetBool parameters.
        ///
        /// Two-type chain: Animator.GetCurrentAnimatorStateInfo → AnimatorStateInfo.IsName.
        /// Each type seeded from a live instance at point of use.
        /// </summary>
        private sealed class AnimatorHandles
        {
            public MethodInfo GetStateInfo;   // GetCurrentAnimatorStateInfo(int)
            public MethodInfo GetBool;        // GetBool(string)
        }

        private sealed class AnimStateInfoHandles
        {
            public MethodInfo IsName;         // AnimatorStateInfo.IsName(string)
        }

        private static readonly ReflectionCache<AnimatorHandles> _animatorCache = new ReflectionCache<AnimatorHandles>(
            builder: t => new AnimatorHandles
            {
                GetStateInfo = t.GetMethod("GetCurrentAnimatorStateInfo", new[] { typeof(int) }),
                GetBool = t.GetMethod("GetBool", new[] { typeof(string) }),
            },
            validator: _ => true,
            logTag: "UITextExtractor",
            logSubject: "Animator");

        private static readonly ReflectionCache<AnimStateInfoHandles> _animStateInfoCache = new ReflectionCache<AnimStateInfoHandles>(
            builder: t => new AnimStateInfoHandles { IsName = t.GetMethod("IsName", new[] { typeof(string) }) },
            validator: h => h.IsName != null,
            logTag: "UITextExtractor",
            logSubject: "AnimatorStateInfo");

        private static string GetNPEObjectiveStatus(object animator)
        {
            _animatorCache.EnsureInitialized(animator.GetType());
            var a = _animatorCache.Handles;

            // Try checking animator state name via GetCurrentAnimatorStateInfo(0).IsName(...)
            if (a?.GetStateInfo != null)
            {
                var stateInfo = a.GetStateInfo.Invoke(animator, new object[] { 0 });
                if (stateInfo != null && _animStateInfoCache.EnsureInitialized(stateInfo.GetType()))
                {
                    var isName = _animStateInfoCache.Handles.IsName;

                    // Check completed state (trigger "toComplete")
                    if ((bool)isName.Invoke(stateInfo, new object[] { "Complete" })
                        || (bool)isName.Invoke(stateInfo, new object[] { "Completed" })
                        || (bool)isName.Invoke(stateInfo, new object[] { "toComplete" }))
                        return Strings.ColorChallengeNodeCompleted;

                    // Check locked state (trigger "toLock")
                    if ((bool)isName.Invoke(stateInfo, new object[] { "Lock" })
                        || (bool)isName.Invoke(stateInfo, new object[] { "Locked" })
                        || (bool)isName.Invoke(stateInfo, new object[] { "toLock" }))
                        return Strings.ColorChallengeNodeLocked;

                    // Check unlocked/normal state (trigger "toNormal" / "Unlock")
                    if ((bool)isName.Invoke(stateInfo, new object[] { "Normal" })
                        || (bool)isName.Invoke(stateInfo, new object[] { "Unlock" })
                        || (bool)isName.Invoke(stateInfo, new object[] { "Unlocked" })
                        || (bool)isName.Invoke(stateInfo, new object[] { "toNormal" })
                        || (bool)isName.Invoke(stateInfo, new object[] { "Idle" }))
                        return Strings.ColorChallengeNodeAvailable;
                }
            }

            // Fallback: try GetBool parameters (like CampaignGraphObjectiveBubble uses)
            if (a?.GetBool != null)
            {
                try
                {
                    bool completed = (bool)a.GetBool.Invoke(animator, new object[] { "Completed" });
                    if (completed) return Strings.ColorChallengeNodeCompleted;

                    bool locked = (bool)a.GetBool.Invoke(animator, new object[] { "Locked" });
                    if (locked) return Strings.ColorChallengeNodeLocked;

                    return Strings.ColorChallengeNodeAvailable;
                }
                catch { /* Parameter doesn't exist */ }
            }

            return null;
        }

        /// <summary>
        /// Reads a localized popup string from the ObjectiveBubble component on a parent.
        /// ObjectiveBubble._popupData holds MTGALocalizedString fields (HeaderString1, FooterString, etc.)
        /// that contain game localization keys like "MainNav/Quest/Quest_Wait_Text".
        /// </summary>
        private static string TryGetObjectiveBubblePopupText(GameObject gameObject, string fieldName)
        {
            if (gameObject == null) return null;

            // ObjectiveBubble is on the direct parent of ObjectiveGraphics
            var parent = gameObject.transform.parent;
            if (parent == null) return null;

            MonoBehaviour bubble = null;
            foreach (var comp in parent.GetComponents<MonoBehaviour>())
            {
                if (comp != null && comp.GetType().Name == "ObjectiveBubble")
                {
                    bubble = comp;
                    break;
                }
            }
            if (bubble == null) return null;

            try
            {
                // _popupData is protected: NonPublic | Instance
                var popupDataField = bubble.GetType().GetField("_popupData", PrivateInstance);
                if (popupDataField == null) return null;

                var popupData = popupDataField.GetValue(bubble);
                if (popupData == null) return null;

                // HeaderString1, FooterString, etc. are public fields of type MTGALocalizedString
                var stringField = popupData.GetType().GetField(fieldName, PublicInstance);
                if (stringField == null) return null;

                var locString = stringField.GetValue(popupData);
                if (locString == null) return null;

                // MTGALocalizedString.Key is a public FIELD (not property)
                var keyField = locString.GetType().GetField("Key", PublicInstance);
                string locKey = keyField?.GetValue(locString) as string;

                if (string.IsNullOrEmpty(locKey) || locKey == "MainNav/General/Empty_String")
                    return null;

                // MTGALocalizedString.ToString() resolves the localization directly
                string resolved = locString.ToString();
                if (!string.IsNullOrEmpty(resolved) && resolved != locKey)
                    return CleanText(resolved);
            }
            catch (System.Exception ex)
            {
                Log.Msg("UITextExtractor", $"Error reading ObjectiveBubble popup: {ex.Message}");
            }

            return null;
        }
    }
}
