namespace AccessibleArena.Core.Interfaces
{
    /// <summary>
    /// Abstraction over the native screen reader output (Tolk P/Invoke).
    /// Injected into AnnouncementService so it can be replaced in tests.
    /// </summary>
    public interface IScreenReaderOutput
    {
        void Speak(string text, bool interrupt);
        void Silence();
    }
}
