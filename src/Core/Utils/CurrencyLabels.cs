using AccessibleArena.Core.Models;

namespace AccessibleArena.Core.Utils
{
    /// <summary>
    /// Shared helpers for labeling currency-typed payment buttons. The game conveys currency via
    /// sprite icons next to a numeric price; screen readers see only the number. These helpers
    /// resolve the localized currency name from a reflected button field's name and combine it
    /// with the price text in the canonical "{price} {currency}" form.
    ///
    /// Used by event-page payment buttons (`_payWithGemsButton`, `_payWithGoldButton`),
    /// the mastery confirmation modal (Gem/Coin/Free button fields), and any other place that
    /// matches a per-currency button by reflected field name.
    /// </summary>
    public static class CurrencyLabels
    {
        /// <summary>
        /// Map a reflected button field name to its localized currency name. Substring match
        /// covers naming variants the game uses (`_payWithGemsButton`, `_buyGemButton`,
        /// `CoinPurchaseButton`, `_payWithGoldButton`, `_freePurchaseButton`, …).
        /// Returns null when the field is not a currency-typed payment button — caller should
        /// fall through to the button's own (already-localized) text in that case.
        /// </summary>
        public static string FromFieldName(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName)) return null;
            if (fieldName.Contains("Gem")) return Strings.CurrencyGems;
            if (fieldName.Contains("Gold") || fieldName.Contains("Coin")) return Strings.CurrencyGold;
            if (fieldName.Contains("Free")) return Strings.PrizeWallSphereCost;
            return null;
        }

        /// <summary>
        /// Combine price and currency into the canonical screen-reader form.
        /// Returns null when the price is empty (no useful announcement); returns the price
        /// alone when no currency name is supplied (e.g. real-money buttons).
        /// </summary>
        public static string FormatPrice(string priceText, string currencyName)
        {
            if (string.IsNullOrEmpty(priceText)) return null;
            if (string.IsNullOrEmpty(currencyName)) return priceText;
            return $"{priceText} {currencyName}";
        }
    }
}
