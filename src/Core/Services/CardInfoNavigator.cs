using UnityEngine;
using MelonLoader;
using MTGAAccessibility.Core.Interfaces;
using System.Collections.Generic;

namespace MTGAAccessibility.Core.Services
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
        public void PrepareForCard(GameObject cardElement)
        {
            if (cardElement == null)
            {
                Deactivate();
                return;
            }

            // If same card, keep current state
            if (_currentCard == cardElement)
                return;

            // New card - prepare but don't load blocks yet
            _currentCard = cardElement;
            _isActive = true;
            _blocksLoaded = false;
            _blocks.Clear();
            _currentBlockIndex = -1;

            MelonLogger.Msg($"[CardInfo] Prepared for card: {cardElement.name}");
        }

        /// <summary>
        /// Activates card info navigation for the given card GameObject.
        /// Returns true if the card has navigable info blocks.
        /// </summary>
        public bool ActivateForCard(GameObject cardElement)
        {
            if (cardElement == null) return false;

            // Use CardDetector to get info blocks
            _blocks = CardDetector.GetInfoBlocks(cardElement);
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
            _announcer.AnnounceInterrupt($"{_blocks[0].Label}: {_blocks[0].Content}");
            return true;
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
        /// Handles input when card info navigation is active.
        /// Only responds to plain Arrow Up/Down without modifiers.
        /// Alt+Arrow is reserved for battlefield row navigation.
        /// Returns true if input was handled.
        /// </summary>
        public bool HandleInput()
        {
            if (!_isActive || _currentCard == null) return false;

            // Check for modifier keys - don't handle if any modifier is pressed
            bool hasModifier = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) ||
                               Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ||
                               Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            if (hasModifier)
            {
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

            _blocks = CardDetector.GetInfoBlocks(_currentCard);
            _blocksLoaded = true;

            if (_blocks.Count == 0)
            {
                MelonLogger.Msg($"[CardInfo] No info blocks found for card");
                return false;
            }

            _currentBlockIndex = 0;
            MelonLogger.Msg($"[CardInfo] Lazy loaded {_blocks.Count} blocks");
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
                _announcer.AnnounceInterrupt("End of card");
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
                _announcer.AnnounceInterrupt("Beginning of card");
            }
        }

        private void AnnounceCurrentBlock()
        {
            if (_currentBlockIndex < 0 || _currentBlockIndex >= _blocks.Count) return;

            var block = _blocks[_currentBlockIndex];
            _announcer.AnnounceInterrupt($"{block.Label}: {block.Content}");
        }
    }
}
