using System;
using System.IO;
using System.Runtime.InteropServices;

namespace AccessibleArena.Core.Utils
{
    /// <summary>
    /// Plays a short synthesized tone through the Windows default audio device, independent of
    /// the game's Wwise mixer and its in-game volume sliders.
    ///
    /// Why not reuse the game's own priority sound: replaying <c>sfx_ui_gain_priority</c>
    /// (Wwise event <c>sfx_priority_on</c>) succeeds without error but produces no audible output
    /// in the shipped build — it appears to be an unmapped/silent event, which is why the native
    /// priority cue was never heard. This routes around the game entirely.
    ///
    /// The cue is a single soft chime: a 440 Hz sine (plus a quiet octave for warmth) with a
    /// raised-cosine attack and a smooth exponential decay, so it swells in and fades out rather
    /// than clicking on and off. The WAV is generated once (16-bit PCM mono) and kept pinned so it
    /// can be handed to <c>winmm.PlaySound</c> with SND_MEMORY | SND_ASYNC — playback is
    /// non-blocking and mixes over NVDA speech on the same device.
    /// </summary>
    public static class ToneCue
    {
        [DllImport("winmm.dll", SetLastError = true)]
        private static extern bool PlaySound(IntPtr pszSound, IntPtr hmod, uint fdwSound);

        private const uint SND_ASYNC = 0x0001;     // play asynchronously (return immediately)
        private const uint SND_NODEFAULT = 0x0002; // no fallback "default" beep on failure
        private const uint SND_MEMORY = 0x0004;    // pszSound points at an in-memory WAV image

        // Cue shape — tweak these to retune the sound by ear.
        private const double FrequencyHz = 440.0;  // fundamental pitch (lower = warmer)
        private const int DurationMs = 300;
        private const double Amplitude = 0.4;      // peak level, 0..1
        private const int AttackMs = 12;           // soft fade-in to avoid a click
        private const double DecayRate = 4.5;      // higher = shorter, snappier tail
        private const int ReleaseMs = 90;          // cosine fade to true silence at the end

        private static byte[] _wav;
        private static GCHandle _pin;

        /// <summary>
        /// Play the priority cue. Non-blocking. Returns true if the OS accepted and started
        /// playback (so callers can log a real did-it-play signal), false on failure.
        /// </summary>
        public static bool Play()
        {
            try
            {
                EnsureWav();
                bool ok = PlaySound(_pin.AddrOfPinnedObject(), IntPtr.Zero, SND_MEMORY | SND_ASYNC | SND_NODEFAULT);
                if (!ok)
                    Log.Msg("ToneCue", $"PlaySound returned false (win32 err {Marshal.GetLastWin32Error()})");
                return ok;
            }
            catch (Exception ex)
            {
                Log.Msg("ToneCue", $"Play failed: {ex.Message}");
                return false;
            }
        }

        private static void EnsureWav()
        {
            if (_wav != null) return;
            _wav = BuildTone();
            _pin = GCHandle.Alloc(_wav, GCHandleType.Pinned);
            Log.Msg("ToneCue", $"tone generated ({_wav.Length} bytes)");
        }

        private static byte[] BuildTone()
        {
            const int sampleRate = 44100;
            const short bitsPerSample = 16;
            const short channels = 1;

            int total = sampleRate * DurationMs / 1000;
            int attack = Math.Max(1, sampleRate * AttackMs / 1000);
            int release = Math.Max(1, sampleRate * ReleaseMs / 1000);
            double ampPeak = Math.Max(0.0, Math.Min(1.0, Amplitude)) * short.MaxValue;

            short[] samples = new short[total];
            for (int i = 0; i < total; i++)
            {
                double t = (double)i / sampleRate;
                double env;
                if (i < attack)
                    env = 0.5 - 0.5 * Math.Cos(Math.PI * i / attack);       // raised-cosine attack
                else
                    env = Math.Exp(-DecayRate * ((double)(i - attack) / sampleRate)); // exp decay

                // Cosine release over the final samples so it fades all the way to silence.
                int fromEnd = total - i;
                if (fromEnd < release)
                    env *= 0.5 - 0.5 * Math.Cos(Math.PI * fromEnd / release);

                double v = Math.Sin(2.0 * Math.PI * FrequencyHz * t);
                v += 0.2 * Math.Sin(2.0 * Math.PI * 2.0 * FrequencyHz * t);  // gentle octave for warmth
                v /= 1.2;
                samples[i] = (short)(v * ampPeak * env);
            }

            int dataBytes = total * (bitsPerSample / 8);
            int blockAlign = channels * (bitsPerSample / 8);
            int byteRate = sampleRate * blockAlign;

            using (var ms = new MemoryStream(44 + dataBytes))
            using (var w = new BinaryWriter(ms))
            {
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataBytes);
                w.Write(new[] { 'W', 'A', 'V', 'E' });
                w.Write(new[] { 'f', 'm', 't', ' ' });
                w.Write(16);                 // fmt chunk size
                w.Write((short)1);           // PCM
                w.Write(channels);
                w.Write(sampleRate);
                w.Write(byteRate);
                w.Write((short)blockAlign);
                w.Write(bitsPerSample);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(dataBytes);
                foreach (short s in samples)
                    w.Write(s);
                w.Flush();
                return ms.ToArray();
            }
        }
    }
}
