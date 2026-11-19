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
        private static string _lastPrompt = string.Empty;
        private static float _lastSubtitleTime = 0f;
        private static string _pendingDialogue;
        private static int _dialogueSource;
        private static float _dialogueReadyAt;
        private static readonly Regex RichTextRegex = new Regex("<.*?>", RegexOptions.Compiled);

        public static void Reset()
        {
            _lastSubtitle = string.Empty;
            _lastSubtitleTime = 0f;
            ClearPendingDialogue();
            ClearPrompt();
        }

        public static void ClearPrompt() => _lastPrompt = string.Empty;
        internal static void ClearPendingDialogue() => _pendingDialogue = null;

        internal static void OnDialogueTextChanged(int source, string rawText)
        {
            if (!ModConfig.EnableSubtitles) return;

            string text = CleanText(rawText);
            if (_pendingDialogue != null && _dialogueSource == source && text == _pendingDialogue) return;
            if (_pendingDialogue != null && (_dialogueSource != source || !text.StartsWith(_pendingDialogue, StringComparison.Ordinal)))
                SpeakPendingDialogue();
            if (string.IsNullOrWhiteSpace(text)) return;

            _pendingDialogue = text;
            _dialogueSource = source;
            _dialogueReadyAt = UnityEngine.Time.unscaledTime + 0.25f;
        }

        internal static void Update()
        {
            if (_pendingDialogue != null && UnityEngine.Time.unscaledTime >= _dialogueReadyAt)
                SpeakPendingDialogue();
        }

        private static void SpeakPendingDialogue()
        {
            string text = _pendingDialogue;
            _pendingDialogue = null;
            OnSubtitleTriggered(text);
        }

        public static void OnSubtitleTriggered(string rawText, string context = "Subtitle")
        {
            if (!ModConfig.EnableSubtitles) return;
            if (string.IsNullOrWhiteSpace(rawText)) return;

            string clean = CleanText(rawText);
            if (string.IsNullOrWhiteSpace(clean)) return;
            if (context == "Prompt")
            {
                if (string.Equals(clean, _lastPrompt, StringComparison.Ordinal)) return;
                _lastPrompt = clean;
                PrismSpeechProvider.Speak(clean, false);
                return;
            }

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
