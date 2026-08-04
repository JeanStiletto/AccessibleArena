namespace AccessibleArena.Core.Constants
{
    /// <summary>
    /// Numeric mirror of the game's <c>CardHolderType</c> enum (Core.dll, root namespace).
    /// Used to compare a card's current holder without taking a compile-time dependency
    /// on the game enum — the reflected value arrives boxed and is unboxed as int.
    ///
    /// Only the values the mod actually reasons about are listed; the game enum also has
    /// OffCameraLibrary, Deckbuilder, Store and other menu-side holders.
    /// </summary>
    public static class CardHolderTypes
    {
        public const int Invalid = 0;
        public const int Library = 1;
        public const int Hand = 3;
        public const int Battlefield = 4;
        public const int Graveyard = 5;
        public const int Exile = 6;
        public const int Stack = 9;
        public const int Command = 10;
        public const int None = -1;
    }
}
