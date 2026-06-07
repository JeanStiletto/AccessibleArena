using NUnit.Framework;
using AccessibleArena.Core.Services;

namespace AccessibleArena.Tests
{
    /// <summary>
    /// Guards the visual-CAPTCHA vs 3-D Secure distinction. Regression target:
    /// a card payment 3DS challenge (Xsolla → checkout.com / credorax → issuer ACS)
    /// must NOT be flagged as an unsolvable visual CAPTCHA, while genuine vendor
    /// CAPTCHAs still are.
    /// </summary>
    [TestFixture]
    public class WebBrowserSecurityTests
    {
        // --- 3-D Secure challenge URLs must NOT be treated as visual CAPTCHAs ---

        [TestCase("https://x3d-redirect.credorax.net/challenge/request/10017646/394069b7")]
        [TestCase("https://3ds.consorsfinanz.de/challengeRequestBrowser")]
        [TestCase("https://3ds.consorsfinanz.de/v2Form")]
        [TestCase("https://authentication-devices.checkout.com/sessions-interceptor/sid_k3h?invoice=2040527417")]
        [TestCase("https://authentication-devices.checkout.com/sessions-interceptor/sid_k3h/device-information")]
        [TestCase("https://secure.xsolla.com/paystation4/?token=uppkfR9c_lc_de")]
        public void IsCaptchaUrl_ThreeDSecureAndPaymentUrls_ReturnsFalse(string url)
        {
            Assert.That(WebBrowserSecurity.IsCaptchaUrl(url), Is.False);
        }

        // --- Real visual-CAPTCHA vendor URLs are still flagged ---

        [TestCase("https://www.google.com/recaptcha/api2/anchor?ar=1")]
        [TestCase("https://newassets.hcaptcha.com/captcha/v1/")]
        [TestCase("https://client-api.arkoselabs.com/v2/")]
        [TestCase("https://challenges.cloudflare.com/turnstile/v0/")]
        [TestCase("https://geo.ddc.paypal.com/captcha/challenge")]
        public void IsCaptchaUrl_KnownVendors_ReturnsTrue(string url)
        {
            Assert.That(WebBrowserSecurity.IsCaptchaUrl(url), Is.True);
        }

        [TestCase(null)]
        [TestCase("")]
        public void IsCaptchaUrl_NullOrEmpty_ReturnsFalse(string url)
        {
            Assert.That(WebBrowserSecurity.IsCaptchaUrl(url), Is.False);
        }

        // --- Iframe-vendor probe (the primary signal for a real CAPTCHA) ---

        [Test]
        public void ContainsUserFacingCaptchaVendor_ReCaptchaIframe_ReturnsTrue()
        {
            string json = "{\"crossOrigin\":1,\"srcs\":[\"https://www.google.com/recaptcha/api2/anchor\"]}";
            Assert.That(WebBrowserSecurity.ContainsUserFacingCaptchaVendor(json), Is.True);
        }

        [Test]
        public void ContainsUserFacingCaptchaVendor_NoSrcCrossOriginIframe_ReturnsFalse()
        {
            // The checkout.com 3DS interstitial: a single cross-origin iframe with no
            // src. Not a visual-CAPTCHA vendor, so this must not trip the warning.
            string json = "{\"crossOrigin\":1,\"srcs\":[\"(no src)\"]}";
            Assert.That(WebBrowserSecurity.ContainsUserFacingCaptchaVendor(json), Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        public void ContainsUserFacingCaptchaVendor_NullOrEmpty_ReturnsFalse(string json)
        {
            Assert.That(WebBrowserSecurity.ContainsUserFacingCaptchaVendor(json), Is.False);
        }

        // --- Cross-origin iframe count / embedded bank challenge (B1 guidance trigger) ---

        [TestCase("{\"crossOrigin\":0,\"srcs\":[]}", 0)]
        [TestCase("{\"crossOrigin\":1,\"srcs\":[\"(no src)\"]}", 1)]
        [TestCase("{\"crossOrigin\":3,\"srcs\":[]}", 3)]
        [TestCase("{\"crossOrigin\": 2 ,\"srcs\":[]}", 2)]
        [TestCase("", 0)]
        [TestCase(null, 0)]
        [TestCase("not json", 0)]
        public void CrossOriginIframeCount_ParsesCount(string json, int expected)
        {
            Assert.That(WebBrowserSecurity.CrossOriginIframeCount(json), Is.EqualTo(expected));
        }

        [Test]
        public void IsEmbeddedBankChallenge_NoSrcCrossOriginIframe_ReturnsTrue()
        {
            // The checkout.com 3DS interstitial signature from the log.
            string json = "{\"crossOrigin\":1,\"srcs\":[\"(no src)\"]}";
            Assert.That(WebBrowserSecurity.IsEmbeddedBankChallenge(json), Is.True);
        }

        [Test]
        public void IsEmbeddedBankChallenge_NoCrossOriginIframe_ReturnsFalse()
        {
            // A slow but same-origin page (no cross-origin frame) is just loading.
            string json = "{\"crossOrigin\":0,\"srcs\":[]}";
            Assert.That(WebBrowserSecurity.IsEmbeddedBankChallenge(json), Is.False);
        }

        [Test]
        public void IsEmbeddedBankChallenge_VendorCaptchaIframe_ReturnsFalse()
        {
            // A real visual CAPTCHA must take the CAPTCHA path, not the 3DS guidance path.
            string json = "{\"crossOrigin\":1,\"srcs\":[\"https://newassets.hcaptcha.com/captcha/v1/\"]}";
            Assert.That(WebBrowserSecurity.IsEmbeddedBankChallenge(json), Is.False);
        }

        // --- PayPal helpers carried over unchanged ---

        [Test]
        public void IsLoginFailureUrl_StepUpWithFailedBecause_ReturnsTrue()
        {
            Assert.That(WebBrowserSecurity.IsLoginFailureUrl(
                "https://www.paypal.com/signin/return?flowFrom=anw-stepup&failedBecause=invalid_input"),
                Is.True);
        }

        [Test]
        public void IsPayPalLoginPage_SigninHost_ReturnsTrue()
        {
            Assert.That(WebBrowserSecurity.IsPayPalLoginPage("https://www.paypal.com/signin"), Is.True);
        }

        [Test]
        public void IsPayPalLoginPage_XsollaCardForm_ReturnsFalse()
        {
            Assert.That(WebBrowserSecurity.IsPayPalLoginPage("https://secure.xsolla.com/paystation4/"), Is.False);
        }
    }
}
