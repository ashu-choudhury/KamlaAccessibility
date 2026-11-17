using System;
using System.Runtime.InteropServices;
using KamlaAccessibility.Core;

namespace KamlaAccessibility.Audio
{
    /// <summary>
    /// Generates instant, low-latency audio feedback chimes using Windows native MessageBeep,
    /// completely decoupled from Unity audio buffers and IL2CPP marshaling.
    /// </summary>
    public static class AudioCueGenerator
    {
        [DllImport("user32.dll")]
        private static extern bool MessageBeep(uint uType);

        public static void PlayNavTick()
        {
            if (!ModConfig.EnableAudioCues) return;
            try { MessageBeep(0x00); } catch { }
        }

        public static void PlayNavSelect()
        {
            if (!ModConfig.EnableAudioCues) return;
            try { MessageBeep(0x40); } catch { } // Asterisk / Info
        }

        public static void PlayBoundaryHit()
        {
            if (!ModConfig.EnableAudioCues) return;
            try { MessageBeep(0x10); } catch { } // Hand / Stop
        }

        public static void PlayRadarBeep()
        {
            if (!ModConfig.EnableAudioCues) return;
            try { MessageBeep(0x00); } catch { }
        }

        public static void PlayObjectivePing()
        {
            if (!ModConfig.EnableAudioCues) return;
            try { MessageBeep(0x40); } catch { }
        }
    }
}
