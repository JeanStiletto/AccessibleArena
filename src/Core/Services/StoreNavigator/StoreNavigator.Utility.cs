using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Models;
using System;
using System.Reflection;

namespace AccessibleArena.Core.Services
{
    public partial class StoreNavigator
    {
        #region Utility Discovery

        private void DiscoverUtilityElements()
        {
            // Payment info button
            AddUtilityElement(_paymentInfoButtonField, "Change payment method");

            // Redeem code input
            if (_redeemCodeInputField != null)
            {
                try
                {
                    var redeemObj = _redeemCodeInputField.GetValue(_controller);
                    if (redeemObj != null)
                    {
                        var redeemMb = redeemObj as MonoBehaviour;
                        if (redeemMb != null && redeemMb.gameObject != null && redeemMb.gameObject.activeInHierarchy)
                        {
                            string redeemLabel = UITextExtractor.GetText(redeemMb.gameObject);
                            if (string.IsNullOrWhiteSpace(redeemLabel))
                                redeemLabel = "Redeem code";

                            _tabs.Add(new TabInfo
                            {
                                TabComponent = null,
                                GameObject = redeemMb.gameObject,
                                DisplayName = redeemLabel,
                                FieldIndex = -1,
                                IsUtility = true
                            });
                        }
                    }
                }
                catch { /* Reflection may fail on different game versions */ }
            }

            // Drop rates link
            AddUtilityElement(_dropRatesLinkField, "Drop rates");

            // Pack progress meter (bonus pack progress info)
            AddPackProgressElement();
        }

        private void AddPackProgressElement()
        {
            try
            {
                // Find PackProgressMeter by GameObject name (type is in platform-specific assembly)
                var go = GameObject.Find("PackProgressMeter_Desktop_16x9(Clone)");
                if (go == null || !go.activeInHierarchy) return;

                // Extract specific text fields by GameObject name
                string goal = null;
                string title = null;

                foreach (var tmp in go.GetComponentsInChildren<Component>(true))
                {
                    if (tmp == null || tmp.GetType().Name != "TextMeshProUGUI") continue;

                    string name = tmp.gameObject.name;
                    if (name == "Text_GoalNumber")
                        goal = UITextExtractor.GetText(tmp.gameObject);
                    else if (name == "Text_Title")
                        title = UITextExtractor.GetText(tmp.gameObject);
                }

                if (string.IsNullOrEmpty(goal) && string.IsNullOrEmpty(title)) return;

                // Format: "0/10: Kaufe 10 weitere..."
                string text = !string.IsNullOrEmpty(goal) && !string.IsNullOrEmpty(title)
                    ? $"{goal}: {title}"
                    : goal ?? title;

                _tabs.Add(new TabInfo
                {
                    TabComponent = null,
                    GameObject = go,
                    DisplayName = $"Pack progress: {text}",
                    FieldIndex = -1,
                    IsUtility = true
                });

                MelonLogger.Msg($"[Store] Found pack progress: {text}");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[Store] Error finding pack progress: {ex.Message}");
            }
        }

        private void AddUtilityElement(FieldInfo field, string fallbackName)
        {
            if (field == null || _controller == null) return;

            try
            {
                var obj = field.GetValue(_controller) as GameObject;
                if (obj != null && obj.activeInHierarchy)
                {
                    // Read localized label from the element; fall back to English
                    string label = UITextExtractor.GetText(obj);
                    if (string.IsNullOrWhiteSpace(label))
                        label = fallbackName;

                    _tabs.Add(new TabInfo
                    {
                        TabComponent = null,
                        GameObject = obj,
                        DisplayName = label,
                        FieldIndex = -1,
                        IsUtility = true
                    });
                }
            }
            catch { /* Reflection may fail on different game versions */ }
        }

        #endregion

        #region Utility Activation

        private void ActivateUtilityElement(TabInfo tab)
        {
            // Payment button: call OnButton_PaymentSetup() directly on controller
            // On Steam, OpenPaymentSetup() is a no-op (GeneralStoreManager doesn't override it).
            // Payment methods are managed through Steam itself, so announce that to the user.
            if (tab.DisplayName == "Change payment method")
            {
                if (SteamOverlayBlocker.IsSteam)
                {
                    MelonLogger.Msg("[Store] Payment setup not available on Steam");
                    _announcer.AnnounceInterrupt(Strings.SteamPaymentNotAvailable);
                    return;
                }

                if (_onButtonPaymentSetupMethod != null && _controller != null)
                {
                    try
                    {
                        MelonLogger.Msg("[Store] Calling OnButton_PaymentSetup() via reflection");
                        _onButtonPaymentSetupMethod.Invoke(_controller, null);
                        return;
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Msg($"[Store] Error calling OnButton_PaymentSetup: {ex.Message}");
                    }
                }
            }

            // Pack progress: info-only, re-announce stored text
            if (tab.DisplayName.StartsWith("Pack progress:"))
            {
                _announcer.AnnounceInterrupt(tab.DisplayName);
                return;
            }

            // Default: use UIActivator for other utility elements
            UIActivator.Activate(tab.GameObject);
        }

        #endregion
    }
}
