namespace AccessibleArena.Core.Interfaces
{
    /// <summary>
    /// Abstraction over the native screen reader output (Prism P/Invoke).
    /// Injected into AnnouncementService so it can be replaced in tests.
    /// </summary>
    public interface IScreenReaderOutput
    {
        void Speak(string text, bool interrupt);

        /// <summary>
        /// Speaks through the SAPI urgent channel, which the user's screen reader cannot
        /// silence with its own cancel-on-keypress handling. Falls back to <see cref="Speak"/>
        /// with interrupt when SAPI is unavailable.
        /// </summary>
        void SpeakUrgent(string text);

        void Silence();
    }
}
