using AccessibleArena.Core.Interfaces;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Production implementation of IScreenReaderOutput — delegates to the static Tolk P/Invoke wrapper.
    /// </summary>
    internal sealed class ScreenReaderAdapter : IScreenReaderOutput
    {
        public void Speak(string text, bool interrupt) => ScreenReaderOutput.Speak(text, interrupt);
        public void Silence() => ScreenReaderOutput.Silence();
    }
}
