using MTGAAccessibility.Contexts.Base;
using MTGAAccessibility.Core.Interfaces;
using MTGAAccessibility.Core.Models;

namespace MTGAAccessibility.Contexts.MainMenu
{
    public class MainMenuContext : BaseMenuContext
    {
        private readonly IContextManager _contextManager;
        private readonly IShortcutRegistry _shortcuts;

        public override string ContextName => "Main Menu";

        public MainMenuContext(
            IAnnouncementService announcer,
            IContextManager contextManager,
            IShortcutRegistry shortcuts)
            : base(announcer)
        {
            _contextManager = contextManager;
            _shortcuts = shortcuts;

            RegisterShortcuts();
        }

        private void RegisterShortcuts()
        {
            _shortcuts.RegisterShortcut(UnityEngine.KeyCode.P, OnPlaySelect, "Play", GameContext.MainMenu);
            _shortcuts.RegisterShortcut(UnityEngine.KeyCode.D, OnDecksSelect, "Decks", GameContext.MainMenu);
            _shortcuts.RegisterShortcut(UnityEngine.KeyCode.S, OnStoreSelect, "Store", GameContext.MainMenu);
        }

        public override void Refresh()
        {
            ClearItems();

            AddMenuItem("Play", OnPlaySelect, "Start a game");
            AddMenuItem("Decks", OnDecksSelect, "Manage your decks");
            AddMenuItem("Store", OnStoreSelect, "Buy packs and items");
            AddMenuItem("Mastery", OnMasterySelect, "View mastery progress");
            AddMenuItem("Profile", OnProfileSelect, "View your profile");
            AddMenuItem("Settings", OnSettingsSelect, "Game settings");
            AddMenuItem("Quit", OnQuitSelect, "Exit the game");
        }

        private void OnPlaySelect()
        {
            Announcer.Announce(Core.Models.Strings.OpeningPlayModes);
            _contextManager.SetContext(GameContext.GameModeSelection);
        }

        private void OnDecksSelect()
        {
            Announcer.Announce(Core.Models.Strings.OpeningDeckManager);
            _contextManager.SetContext(GameContext.DeckBuilder);
        }

        private void OnStoreSelect()
        {
            Announcer.Announce(Core.Models.Strings.OpeningStore);
            _contextManager.SetContext(GameContext.Shop);
        }

        private void OnMasterySelect()
        {
            Announcer.Announce(Core.Models.Strings.OpeningMastery);
        }

        private void OnProfileSelect()
        {
            Announcer.Announce(Core.Models.Strings.OpeningProfile);
        }

        private void OnSettingsSelect()
        {
            Announcer.Announce(Core.Models.Strings.OpeningSettings);
            _contextManager.SetContext(GameContext.Settings);
        }

        private void OnQuitSelect()
        {
            Announcer.Announce(Core.Models.Strings.QuittingGame);
        }
    }
}
