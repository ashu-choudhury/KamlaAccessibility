using Il2Cpp;
using KamlaAccessibility.Speech;
using UnityEngine;

namespace KamlaAccessibility.Gameplay
{
    public static class StatusReporter
    {
        public static void ReportLives(PlayerMovement player)
        {
            PrismSpeechProvider.Speak(player != null ? $"Lives remaining: {player.Lives}." : "Lives unavailable.", true);
        }

        public static void ReportKamlaLocation(PlayerMovement player)
        {
            var enemy = player != null ? player.enemyRef : null;
            if (player != null && (enemy == null || !enemy.isActiveAndEnabled))
                enemy = Object.FindObjectOfType<Enemy>();
            if (enemy == null || !enemy.isActiveAndEnabled)
            {
                PrismSpeechProvider.Speak("Kamla's location is unavailable.", true);
                return;
            }

            Vector3 offset = enemy.transform.position - player.transform.position;
            Vector3 horizontal = NavigationHelper.Flat(offset);
            float angle = Vector3.SignedAngle(player.transform.forward, horizontal, Vector3.up);
            int clock = (Mathf.RoundToInt(angle / 30f) + 12) % 12;
            string height = Mathf.Abs(offset.y) >= 1.5f
                ? $", {Mathf.Abs(offset.y):F1} meters {(offset.y > 0f ? "above" : "below")} you" : "";
            PrismSpeechProvider.Speak($"Kamla is {horizontal.magnitude:F1} meters away at {(clock == 0 ? 12 : clock)} o'clock{height}.", true);
        }
    }
}
