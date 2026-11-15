namespace AccessibleArena.Core.Services
{
    internal static class PopupButtonFilter
    {
        internal static bool IsDismissOverlay(string objectName)
        {
            string name = objectName.ToLowerInvariant();
            return name.Contains("background") || name.Contains("overlay") ||
                   name.Contains("backdrop") || name.Contains("dismiss") ||
                   name.Contains("shield") ||
                   // BattlePassPurchaseConfirmation wires these fades to OnCancelClicked.
                   // Filter them before label deduplication: TopFade can read "3400",
                   // the same label as the actual mastery purchase button.
                   name == "topfade" || name == "bottomfade";
        }
    }
}
