namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Pure URL / iframe classification helpers for the embedded payment browser.
    /// Extracted from <see cref="WebBrowserAccessibility"/> so the security
    /// heuristics can be unit-tested without Unity / ZFBrowser dependencies.
    ///
    /// Key distinction this class encodes: a *visual CAPTCHA* (reCAPTCHA, hCaptcha,
    /// Arkose, Cloudflare Turnstile, PayPal's ddc/captcha) cannot be solved without
    /// sight and means the user is blocked. A *3-D Secure bank challenge*
    /// (Xsolla → checkout.com / credorax → the card issuer's ACS) is the opposite:
    /// it is normally an accessible text / PIN / passphrase form we WANT to read.
    /// Earlier code flagged any URL containing "/challenge" or "/captcha" as a
    /// visual CAPTCHA, which wrongly suppressed reading of 3DS challenge forms
    /// (e.g. 3ds.consorsfinanz.de/v2Form). We now key visual-CAPTCHA detection on
    /// known vendor tokens only.
    /// </summary>
    internal static class WebBrowserSecurity
    {
        // Hostnames / path tokens that identify a user-facing *visual* CAPTCHA.
        // A match means the page (or its content iframe) is a sight-required blocker.
        // Deliberately vendor-specific: generic words like "challenge" are NOT here,
        // because 3-D Secure bank pages legitimately use them.
        internal static readonly string[] UserFacingCaptchaTokens =
        {
            "recaptcha",       // google reCAPTCHA (recaptcha.net, google.com/recaptcha, gstatic.com/recaptcha)
            "hcaptcha",        // hCaptcha (hcaptcha.com, newassets.hcaptcha.com)
            "arkoselabs",      // Arkose Labs / FunCaptcha
            "funcaptcha",
            "challenges.cloudflare.com",  // Cloudflare Turnstile / challenge platform
            "turnstile",
            "ddc.paypal.com/captcha",     // PayPal's device-data-collection captcha (blocker variant)
        };

        /// <summary>
        /// True only when the URL points directly at a known visual-CAPTCHA vendor.
        /// 3-D Secure bank authentication URLs (which use "/challenge" paths) are
        /// intentionally NOT matched — they are accessible forms, not visual CAPTCHAs.
        /// Genuine visual CAPTCHAs are otherwise caught at the iframe level by
        /// <see cref="ContainsUserFacingCaptchaVendor"/>.
        /// </summary>
        internal static bool IsCaptchaUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            string lower = url.ToLowerInvariant();
            foreach (var token in UserFacingCaptchaTokens)
            {
                if (lower.Contains(token)) return true;
            }
            return false;
        }

        /// <summary>
        /// URL patterns PayPal uses when credentials were rejected but the user is
        /// being bounced back to the login page to retry (not a visual CAPTCHA).
        /// These used to be treated as hard CAPTCHA but that produced a false warning
        /// — the user saw another login page, not a challenge.
        /// </summary>
        internal static bool IsLoginFailureUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            string lower = url.ToLowerInvariant();

            // PayPal: Base64 "adsddcaptcha" param added to failed login redirects
            // e.g. &YWRzZGRjYXB0Y2hh=1
            if (lower.Contains("ywrzzgrjyxb0y2hh"))
                return true;

            // PayPal: step-up auth flow combined with login failure
            // e.g. /signin/return?flowFrom=anw-stepup&...&failedBecause=invalid_input
            if (lower.Contains("stepup") && lower.Contains("failedbecause"))
                return true;

            return false;
        }

        // PayPal login-form hosts where the email field is a React-controlled input.
        // Typing via execCommand('insertText') updates the DOM but PayPal's React state
        // stays empty → server rejects with failedBecause=invalid_input + adsddcaptcha
        // regardless of the password. On these URLs the email field must take the same
        // passthrough path as the password field. Scoped narrowly so unrelated paypal.com
        // pages (and other checkout hosts like Xsolla card forms) keep the JS input path.
        internal static bool IsPayPalLoginPage(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            string lower = url.ToLowerInvariant();
            if (!lower.Contains("paypal.com")) return false;
            return lower.Contains("/signin")
                || lower.Contains("/agreements/approve")
                || lower.Contains("/webapps/hermes")
                || lower.Contains("/checkoutweb");
        }

        /// <summary>
        /// Inspects the JSON emitted by DetectCrossOriginIframesScript for a known
        /// visual-CAPTCHA vendor among the page's cross-origin iframes.
        /// </summary>
        internal static bool ContainsUserFacingCaptchaVendor(string iframeJson)
        {
            if (string.IsNullOrEmpty(iframeJson)) return false;
            string lower = iframeJson.ToLowerInvariant();
            foreach (var token in UserFacingCaptchaTokens)
            {
                if (lower.Contains(token)) return true;
            }
            return false;
        }

        /// <summary>
        /// Number of cross-origin iframes reported by DetectCrossOriginIframesScript
        /// (the "crossOrigin" count in its JSON). A positive count on an otherwise
        /// element-less payment page is the signature of an embedded bank / 3-D Secure
        /// challenge whose content is in an unreadable cross-origin frame.
        /// </summary>
        internal static int CrossOriginIframeCount(string iframeJson)
        {
            if (string.IsNullOrEmpty(iframeJson)) return 0;
            const string key = "\"crossorigin\":";
            string lower = iframeJson.ToLowerInvariant();
            int i = lower.IndexOf(key, System.StringComparison.Ordinal);
            if (i < 0) return 0;
            i += key.Length;
            while (i < lower.Length && lower[i] == ' ') i++;
            int start = i;
            while (i < lower.Length && lower[i] >= '0' && lower[i] <= '9') i++;
            if (i == start) return 0;
            return int.TryParse(lower.Substring(start, i - start), out int v) ? v : 0;
        }

        /// <summary>
        /// True when the page's content lives in an unreadable cross-origin iframe
        /// AND it is not a known visual-CAPTCHA vendor — i.e. the embedded bank /
        /// 3-D Secure challenge case (checkout.com "confirm on smartphone" etc.).
        /// Used to replace a silent loading loop with actionable guidance.
        /// </summary>
        internal static bool IsEmbeddedBankChallenge(string iframeJson)
        {
            return CrossOriginIframeCount(iframeJson) > 0
                && !ContainsUserFacingCaptchaVendor(iframeJson);
        }
    }
}
