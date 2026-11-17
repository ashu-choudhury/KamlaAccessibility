using System;
using KamlaAccessibility.Core;

namespace KamlaAccessibility.Audio
{
    /// <summary>
    /// Audio cue dispatcher.
    /// Note: Windows native MessageBeep alert sounds have been completely removed
    /// per user request to prevent system dings and eliminate any audio thread lag.
    /// </summary>
    public static class AudioCueGenerator
    {
        public static void PlayNavTick()
        {
            // Intentionally silent - no Windows alert sounds
        }

        public static void PlayNavSelect()
        {
            // Intentionally silent - no Windows alert sounds
        }

        public static void PlayBoundaryHit()
        {
            // Intentionally silent - no Windows alert sounds
        }

        public static void PlayRadarBeep()
        {
            // Intentionally silent - no Windows alert sounds
        }

        public static void PlayObjectivePing()
        {
            // Intentionally silent - no Windows alert sounds
        }
    }
}
