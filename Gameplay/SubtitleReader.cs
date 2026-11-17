using System;
using System.Text.RegularExpressions;
using KamlaAccessibility.Speech;
using KamlaAccessibility.Core;

namespace KamlaAccessibility.Gameplay
{
    /// <summary>
    /// Monitors and speaks in-game subtitles, dialogue, mantras, and lore text.
    /// </summary>
    public static class SubtitleReader
    {
        private static string _lastSubtitle = string.Empty;
        private static float _lastSubtitleTime = 0f;
        private static readonly Regex RichTextRegex = new Regex("<.*?>", RegexOptions.Compiled);

        public static void OnSubtitleTriggered(string rawText, string context = "Subtitle")
        {
            if (!ModConfig.EnableSubtitles) return;
            if (string.IsNullOrWhiteSpace(rawText)) return;

            string clean = CleanText(rawText);
            if (string.IsNullOrWhiteSpace(clean)) return;

            // Avoid repeating the same subtitle within 2 seconds
            float now = UnityEngine.Time.unscaledTime;
            if (clean.Equals(_lastSubtitle, StringComparison.OrdinalIgnoreCase) && (now - _lastSubtitleTime) < 2.0f)
            {
                return;
            }

            _lastSubtitle = clean;
            _lastSubtitleTime = now;

            PrismSpeechProvider.Speak(clean, false);
        }

        public static string CleanText(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            // Remove Unity rich text tags: <color=#...>, <b>, <i>, <size=...>
            string text = RichTextRegex.Replace(input, string.Empty);
            return text.Trim();
        }
    }
}
