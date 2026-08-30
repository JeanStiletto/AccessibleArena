namespace AccessibleArena.Core.Services
{
    internal static class ConfirmationInputPolicy
    {
        internal static bool ShouldActivateCombatPrimary(
            bool backspaceDown,
            bool spaceDown,
            bool spaceBlocked)
        {
            return !backspaceDown && spaceDown && !spaceBlocked;
        }

        internal static T ResolveGroupedFocus<T>(
            T currentElement,
            bool isStandaloneGroup,
            T standaloneElement)
            where T : class
        {
            if (currentElement != null)
                return currentElement;

            return isStandaloneGroup ? standaloneElement : null;
        }
    }
}
