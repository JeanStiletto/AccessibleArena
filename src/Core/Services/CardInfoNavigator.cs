using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System.Collections.Generic;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Handles vertical navigation through card information blocks.
    /// When a card is focused, Arrow Up/Down navigates through:
    /// Name, Mana Cost, Type, Power/Toughness, Rules Text, Flavor Text, Artist
    ///
    /// Uses lazy loading: info blocks are only extracted when user presses arrow keys,
    /// not on focus change. This ensures fast navigation through many cards.
    /// </summary>
    public class CardInfoNavigator
    {
        private readonly IAnnouncementService _announcer;
        private List<CardInfoBlock> _blocks = new List<CardInfoBlock>();
        private GameObject _currentCard;
        private int _currentBlockIndex = -1;
        private bool _isActive;
        private bool _blocksLoaded;
        private ZoneType _currentZone = ZoneType.Hand;

        public bool IsActive => _isActive;
        public GameObject CurrentCard => _currentCard;

        public CardInfoNavigator(IAnnouncementService announcer)
        {
            _announcer = announcer;
        }

        /// <summary>
        /// Prepares card navigation for the given card without extracting info yet.
        /// Call this when focus changes to a card. Info is loaded lazily on first arrow press.
        /// </summary>
        public void PrepareForCard(GameObject cardElement, ZoneType zone = ZoneType.Hand)
        {
            if (cardElement == null)
            {
                Deactivate();
                return;
            }

            // If same card and same zone, keep current state
            if (_currentCard == cardElement && _currentZone == zone)
                return;

            // New card - prepare but don't load blocks yet
            _currentCard = cardElement;
            _currentZone = zone;
            _isActive = true;
            _blocksLoaded = false;
            _blocks.Clear();
            _currentBlockIndex = -1;

            // Log card name for correlation with announcements
            string cardName = CardDetector.GetCardName(cardElement);
            MelonLogger.Msg($"[CardInfo] Prepared for '{cardName}' ({cardElement.name}) in zone: {zone}");
        }

        /// <summary>
        /// Activates card info navigation for the given card GameObject.
        /// Returns true if the card has navigable info blocks.
        /// Uses the zone set by PrepareForCard, or defaults to Hand.
        /// </summary>
        public bool ActivateForCard(GameObject cardElement)
        {
            if (cardElement == null) return false;

            // Use CardDetector to get info blocks with current zone context
            _blocks = CardDetector.GetInfoBlocks(cardElement, _currentZone);
            _currentCard = cardElement;
            _currentBlockIndex = 0;
            _blocksLoaded = true;

            if (_blocks.Count == 0)
            {
                MelonLogger.Msg($"[CardInfo] No info blocks found for card");
                _isActive = false;
                return false;
            }

            _isActive = true;
            MelonLogger.Msg($"[CardInfo] Activated with {_blocks.Count} blocks for: {_blocks[0].Content}");

            // Announce first block (card name)
            _announcer.AnnounceInterrupt(FormatBlock(_blocks[0]));
            return true;
        }

        /// <summary>
        /// Invalidates cached info blocks so they are re-extracted on next arrow press.
        /// Preserves the current block index so the user stays on the same info field.
        /// </summary>
        public void InvalidateBlocks()
        {
            _blocksLoaded = false;
            _blocks.Clear();
        }

        /// <summary>
        /// Deactivates card info navigation.
        /// </summary>
        public void Deactivate()
        {
            _isActive = false;
            _currentCard = null;
            _blocks.Clear();
            _currentBlockIndex = -1;
            _blocksLoaded = false;
        }

        /// <summary>
        /// Gets extended card information: keyword descriptions and linked face info.
        /// Returns a formatted string for screen reader announcement, or null if not available.
        /// </summary>
        public string GetExtendedCardInfo()
        {
            if (!_isActive || _currentCard == null)
                return null;

            var parts = new List<string>();

            // Keyword descriptions
            var keywords = CardModelProvider.GetKeywordDescriptions(_currentCard);
            if (keywords.Count > 0)
            {
                string keywordText = $"{Strings.CardInfoKeywords}: {string.Join(". ", keywords)}";
                parts.Add(keywordText);
            }

            // Linked face info
            var linkedFace = CardModelProvider.GetLinkedFaceInfo(_currentCard);
            if (linkedFace.HasValue)
            {
                var (label, faceInfo) = linkedFace.Value;
                var faceParts = new List<string>();
                if (!string.IsNullOrEmpty(faceInfo.Name))
                    faceParts.Add(faceInfo.Name);
                if (!string.IsNullOrEmpty(faceInfo.ManaCost))
                    faceParts.Add(faceInfo.ManaCost);
                if (!string.IsNullOrEmpty(faceInfo.TypeLine))
                    faceParts.Add(faceInfo.TypeLine);
                if (!string.IsNullOrEmpty(faceInfo.PowerToughness))
                    faceParts.Add(faceInfo.PowerToughness);
                if (!string.IsNullOrEmpty(faceInfo.RulesText))
                    faceParts.Add(faceInfo.RulesText);

                if (faceParts.Count > 0)
                {
                    string faceText = $"{label}: {string.Join(", ", faceParts)}";
                    parts.Add(faceText);
                }
            }

            if (parts.Count == 0)
                return Strings.NoExtendedCardInfo;

            return string.Join(". ", parts);
        }

        /// <summary>
        /// Handles input when card info navigation is active.
        /// Only responds to plain Arrow Up/Down without modifiers.
        /// Alt+Arrow is reserved for battlefield row navigation.
        /// Returns true if input was handled.
        /// </summary>
        public bool HandleInput()
        {
            // Debug: Log why HandleInput might return false early
            if (!_isActive)
            {
                // Only log occasionally to avoid spam - check if arrow pressed
                if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.UpArrow))
                    MelonLogger.Msg($"[CardInfo] HandleInput: Not active, ignoring arrow key");
                return false;
            }
            if (_currentCard == null)
            {
                if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.UpArrow))
                    MelonLogger.Msg($"[CardInfo] HandleInput: CurrentCard is null, ignoring arrow key");
                return false;
            }

            // Check for modifier keys - don't handle if any modifier is pressed
            bool hasModifier = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) ||
                               Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ||
                               Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            if (hasModifier)
            {
                if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.UpArrow))
                    MelonLogger.Msg($"[CardInfo] HandleInput: Modifier key held, ignoring arrow key");
                return false; // Let other navigators handle modified arrow keys
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                // Lazy load blocks on first navigation
                if (!_blocksLoaded)
                {
                    if (!LoadBlocks())
                        return false;
                }
                NavigateNext();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                // Lazy load blocks on first navigation
                if (!_blocksLoaded)
                {
                    if (!LoadBlocks())
                        return false;
                }
                NavigatePrevious();
                return true;
            }

            // Tab lets parent handle navigation (will deactivate via focus change)
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                return false; // Let parent handle Tab
            }

            return false;
        }

        /// <summary>
        /// Loads info blocks from the current card. Called lazily on first arrow press.
        /// </summary>
        private bool LoadBlocks()
        {
            if (_currentCard == null) return false;

            _blocks = CardDetector.GetInfoBlocks(_currentCard, _currentZone);
            _blocksLoaded = true;

            if (_blocks.Count == 0)
            {
                MelonLogger.Msg($"[CardInfo] No info blocks found for card");
                return false;
            }

            // Preserve block index on reload (e.g., after InvalidateBlocks), reset on first load
            if (_currentBlockIndex < 0 || _currentBlockIndex >= _blocks.Count)
                _currentBlockIndex = 0;
            MelonLogger.Msg($"[CardInfo] Lazy loaded {_blocks.Count} blocks, index {_currentBlockIndex}");
            return true;
        }

        private void NavigateNext()
        {
            if (_currentBlockIndex < _blocks.Count - 1)
            {
                _currentBlockIndex++;
                AnnounceCurrentBlock();
            }
            else
            {
                _announcer.AnnounceInterruptVerbose(Strings.EndOfCard);
            }
        }

        private void NavigatePrevious()
        {
            if (_currentBlockIndex > 0)
            {
                _currentBlockIndex--;
                AnnounceCurrentBlock();
            }
            else
            {
                _announcer.AnnounceInterruptVerbose(Strings.BeginningOfCard);
            }
        }

        private void AnnounceCurrentBlock()
        {
            if (_currentBlockIndex < 0 || _currentBlockIndex >= _blocks.Count) return;

            var block = _blocks[_currentBlockIndex];
            _announcer.AnnounceInterrupt(FormatBlock(block));
        }

        private static string FormatBlock(CardInfoBlock block)
        {
            bool showLabel = !block.IsVerbose ||
                             (AccessibleArenaMod.Instance?.Settings?.VerboseAnnouncements != false);
            return showLabel ? $"{block.Label}: {block.Content}" : block.Content;
        }
    }
}
