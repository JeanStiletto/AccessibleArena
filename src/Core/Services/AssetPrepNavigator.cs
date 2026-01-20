using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System;
using TMPro;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for the AssetPrep (download) screen shown on fresh install.
    ///
    /// SAFETY: This navigator is designed to fail gracefully rather than block users.
    /// - Low priority so login screens take over immediately when available
    /// - All operations wrapped in try-catch
    /// - Only announces, doesn't block any keys unless buttons are confirmed active
    /// - Periodic progress announcements rather than continuous polling
    /// </summary>
    public class AssetPrepNavigator : BaseNavigator
    {
        // Screen detection
        private const string SCENE_NAME = "AssetPrep";
        private GameObject _assetPrepScreen;
        private Component _assetPrepScreenComponent;

        // UI elements (found via reflection to avoid hard dependencies)
        private TMP_Text _infoText;
        private TMP_Text _buildVersionText;
        private Button _downloadButton;
        private Button _retryButton;
        private Button _withoutDownloadButton;

        // Progress tracking
        private string _lastAnnouncedText = "";
        private float _lastProgressAnnounceTime;
        private const float ProgressAnnounceInterval = 5f; // Announce progress every 5 seconds

        public override string NavigatorId => "AssetPrep";
        public override string ScreenName => "Download screen";

        // Low priority - let login screens take over ASAP
        public override int Priority => 5;

        // Don't use card navigation on this screen
        protected override bool SupportsCardNavigation => false;

        public AssetPrepNavigator(IAnnouncementService announcer) : base(announcer) { }

        protected override bool DetectScreen()
        {
            try
            {
                // Only active in AssetPrep scene
                var scene = SceneManager.GetActiveScene();
                if (scene.name != SCENE_NAME)
                    return false;

                // Try to find the AssetPrepScreen component
                _assetPrepScreen = null;
                _assetPrepScreenComponent = null;

                // Search for AssetPrepScreen MonoBehaviour
                var allObjects = GameObject.FindObjectsOfType<MonoBehaviour>();
                foreach (var mb in allObjects)
                {
                    if (mb != null && mb.GetType().Name == "AssetPrepScreen")
                    {
                        _assetPrepScreen = mb.gameObject;
                        _assetPrepScreenComponent = mb;
                        break;
                    }
                }

                // If we found the screen component, try to get UI elements
                if (_assetPrepScreenComponent != null)
                {
                    TryGetUIElements();
                    return true;
                }

                // Even without the component, if we're in AssetPrep scene,
                // try to find text elements directly
                return TryFindTextElementsFallback();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[{NavigatorId}] DetectScreen error (safe to ignore): {ex.Message}");
                return false;
            }
        }

        private void TryGetUIElements()
        {
            try
            {
                if (_assetPrepScreenComponent == null) return;

                var type = _assetPrepScreenComponent.GetType();

                // Get InfoText field
                var infoTextField = type.GetField("InfoText");
                if (infoTextField != null)
                {
                    _infoText = infoTextField.GetValue(_assetPrepScreenComponent) as TMP_Text;
                }

                // Get BuildVersionText field
                var versionField = type.GetField("BuildVersionText");
                if (versionField != null)
                {
                    _buildVersionText = versionField.GetValue(_assetPrepScreenComponent) as TMP_Text;
                }

                // Get DownloadButton field
                var downloadField = type.GetField("DownloadButton");
                if (downloadField != null)
                {
                    _downloadButton = downloadField.GetValue(_assetPrepScreenComponent) as Button;
                }

                // Get RetryButton field
                var retryField = type.GetField("RetryButton");
                if (retryField != null)
                {
                    _retryButton = retryField.GetValue(_assetPrepScreenComponent) as Button;
                }

                // Get NpeWithoutDownloadButton field
                var withoutField = type.GetField("NpeWithoutDownloadButton");
                if (withoutField != null)
                {
                    _withoutDownloadButton = withoutField.GetValue(_assetPrepScreenComponent) as Button;
                }

                MelonLogger.Msg($"[{NavigatorId}] Found UI elements - InfoText:{_infoText != null}, Version:{_buildVersionText != null}, Download:{_downloadButton != null}, Retry:{_retryButton != null}, Without:{_withoutDownloadButton != null}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[{NavigatorId}] TryGetUIElements error: {ex.Message}");
            }
        }

        private bool TryFindTextElementsFallback()
        {
            try
            {
                // Fallback: search for TMP_Text components in scene
                var textComponents = GameObject.FindObjectsOfType<TMP_Text>();
                foreach (var text in textComponents)
                {
                    if (text == null || !text.gameObject.activeInHierarchy) continue;

                    string name = text.gameObject.name.ToLower();
                    if (name.Contains("info") || name.Contains("status") || name.Contains("progress"))
                    {
                        _infoText = text;
                    }
                    else if (name.Contains("version") || name.Contains("build"))
                    {
                        _buildVersionText = text;
                    }
                }

                // Also try to find buttons
                var buttons = GameObject.FindObjectsOfType<Button>();
                foreach (var btn in buttons)
                {
                    if (btn == null || !btn.gameObject.activeInHierarchy) continue;

                    string name = btn.gameObject.name.ToLower();
                    if (name.Contains("download") && !name.Contains("without"))
                    {
                        _downloadButton = btn;
                    }
                    else if (name.Contains("retry"))
                    {
                        _retryButton = btn;
                    }
                    else if (name.Contains("without"))
                    {
                        _withoutDownloadButton = btn;
                    }
                }

                // Return true if we found at least info text or any button
                return _infoText != null || _downloadButton != null || _retryButton != null;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[{NavigatorId}] TryFindTextElementsFallback error: {ex.Message}");
                return false;
            }
        }

        protected override void DiscoverElements()
        {
            try
            {
                // Add active buttons as navigable elements
                if (_downloadButton != null && _downloadButton.gameObject.activeInHierarchy && _downloadButton.interactable)
                {
                    AddButton(_downloadButton.gameObject, "Download");
                }

                if (_withoutDownloadButton != null && _withoutDownloadButton.gameObject.activeInHierarchy && _withoutDownloadButton.interactable)
                {
                    AddButton(_withoutDownloadButton.gameObject, "Continue without download");
                }

                if (_retryButton != null && _retryButton.gameObject.activeInHierarchy && _retryButton.interactable)
                {
                    AddButton(_retryButton.gameObject, "Retry");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[{NavigatorId}] DiscoverElements error: {ex.Message}");
            }
        }

        protected override bool ValidateElements()
        {
            try
            {
                // Still in AssetPrep scene?
                var scene = SceneManager.GetActiveScene();
                if (scene.name != SCENE_NAME)
                    return false;

                // Re-scan for buttons that may have become active
                RefreshButtons();

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void RefreshButtons()
        {
            try
            {
                // Re-discover elements if button states changed
                bool hadElements = _elements.Count > 0;
                _elements.Clear();
                DiscoverElements();

                // If we gained elements, announce
                if (!hadElements && _elements.Count > 0)
                {
                    _announcer.AnnounceInterrupt($"{_elements.Count} options available. {Models.Strings.NavigateWithArrows}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[{NavigatorId}] RefreshButtons error: {ex.Message}");
            }
        }

        protected override void OnActivated()
        {
            _lastAnnouncedText = "";
            _lastProgressAnnounceTime = Time.time;
        }

        protected override string GetActivationAnnouncement()
        {
            try
            {
                string announcement = ScreenName;

                // Add version info if available
                if (_buildVersionText != null && !string.IsNullOrEmpty(_buildVersionText.text))
                {
                    announcement += $". Version: {_buildVersionText.text}";
                }

                // Add current status
                string status = GetCurrentStatusText();
                if (!string.IsNullOrEmpty(status))
                {
                    announcement += $". {status}";
                }

                // Add button count
                if (_elements.Count > 0)
                {
                    announcement += $". {_elements.Count} options. {Models.Strings.NavigateWithArrows}";
                }
                else
                {
                    announcement += ". Please wait.";
                }

                return announcement;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[{NavigatorId}] GetActivationAnnouncement error: {ex.Message}");
                return ScreenName;
            }
        }

        private string GetCurrentStatusText()
        {
            try
            {
                if (_infoText != null && !string.IsNullOrEmpty(_infoText.text))
                {
                    return CleanStatusText(_infoText.text);
                }
            }
            catch { }

            return null;
        }

        private string CleanStatusText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Remove any rich text tags
            text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");
            return text.Trim();
        }

        public override void Update()
        {
            try
            {
                base.Update();

                // If active, periodically announce progress
                if (_isActive)
                {
                    CheckProgressUpdate();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[{NavigatorId}] Update error: {ex.Message}");
            }
        }

        private void CheckProgressUpdate()
        {
            try
            {
                // Only announce progress periodically
                if (Time.time - _lastProgressAnnounceTime < ProgressAnnounceInterval)
                    return;

                string currentText = GetCurrentStatusText();
                if (string.IsNullOrEmpty(currentText))
                    return;

                // Only announce if text changed
                if (currentText != _lastAnnouncedText)
                {
                    _lastAnnouncedText = currentText;
                    _lastProgressAnnounceTime = Time.time;

                    // Don't interrupt if user is navigating
                    _announcer.Announce(currentText, AnnouncementPriority.Normal);
                }
            }
            catch { }
        }

        protected override bool HandleCustomInput()
        {
            // F2 to repeat current status
            if (Input.GetKeyDown(KeyCode.F2))
            {
                try
                {
                    string status = GetCurrentStatusText();
                    if (!string.IsNullOrEmpty(status))
                    {
                        _announcer.AnnounceInterrupt(status);
                    }
                    else
                    {
                        _announcer.AnnounceInterrupt("Waiting for download screen...");
                    }
                }
                catch { }
                return true;
            }

            return false;
        }

        public override void OnSceneChanged(string sceneName)
        {
            // Deactivate when leaving AssetPrep scene
            if (_isActive && sceneName != SCENE_NAME)
            {
                MelonLogger.Msg($"[{NavigatorId}] Scene changed to {sceneName}, deactivating");
                Deactivate();
            }
        }
    }
}
