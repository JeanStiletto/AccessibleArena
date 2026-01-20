namespace AccessibleArena.Core.Models
{
    public enum GameContext
    {
        Unknown,
        Loading,
        Login,
        MainMenu,
        Settings,
        GameModeSelection,
        DeckBuilder,
        Shop,
        Draft,
        PreGame,  // VS screen before duel starts
        Duel,     // Actual gameplay
        MatchResults
    }
}
