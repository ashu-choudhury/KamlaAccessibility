using System;
using UnityEngine;
using Il2Cpp;
using KamlaAccessibility.Speech;
using KamlaAccessibility.Audio;

namespace KamlaAccessibility.Gameplay
{
    /// <summary>
    /// Reports full player health, stamina, carried items, location, and Kamla's threat status when pressing H.
    /// </summary>
    public static class StatusReporter
    {
        public static void ReportStatus(PlayerMovement player)
        {
            if (player == null)
            {
                player = UnityEngine.Object.FindObjectOfType<PlayerMovement>();
            }

            if (player == null)
            {
                PrismSpeechProvider.Speak("Player status unavailable.", true);
                return;
            }

            AudioCueGenerator.PlayNavSelect();

            var sb = new System.Text.StringBuilder();

            // 1. Lives / Health
            int lives = player.Lives;
            sb.Append($"Lives remaining: {lives}. ");

            // 2. Stamina
            if (player.staminaBar != null && player.staminaBar.MaxStamina > 0.01f)
            {
                float pct = Mathf.Clamp01(player.staminaBar.Stamina / player.staminaBar.MaxStamina) * 100f;
                sb.Append($"Stamina: {(int)pct} percent. ");
            }

            // 3. Location
            string location = "Inside the Haveli";
            if (player.IsInCourtyard) location = "Courtyard Altar area";
            else if (player.IsInMandir) location = "Temple Mandir";
            sb.Append($"Location: {location}. ");

            // 4. Hiding state
            if (player.IsHiding)
            {
                string spot = !string.IsNullOrEmpty(player.HiddenInObjectName) ? player.HiddenInObjectName : "Hiding Spot";
                sb.Append($"Currently hiding inside {spot}. ");
            }

            // 5. Carried item
            if (player.IsHavingItem)
            {
                string heldName = null;
                try
                {
                    if (player.ItemContainer != null && player.ItemContainer.childCount > 0)
                    {
                        var child = player.ItemContainer.GetChild(0);
                        if (child != null)
                        {
                            var ip = child.GetComponent<ItemPicker>();
                            if (ip != null)
                            {
                                heldName = HotspotManager.CleanItemName(ip);
                            }
                            else
                            {
                                var k = child.GetComponent<Key>();
                                if (k != null)
                                {
                                    heldName = HotspotManager.CleanKeyName(k);
                                }
                                else
                                {
                                    heldName = child.gameObject.name.Replace("(Clone)", "").Replace("_", " ").Trim();
                                }
                            }
                        }
                    }
                }
                catch { }

                if (!string.IsNullOrEmpty(heldName))
                {
                    sb.Append($"Holding {heldName} in hands. ");
                }
                else
                {
                    sb.Append("Carrying an item in hands. ");
                }
            }
            else
            {
                sb.Append("Hands empty. ");
            }

            // 6. Kamla threat status
            var enemy = UnityEngine.Object.FindObjectOfType<Enemy>();
            if (enemy != null && enemy.gameObject.activeInHierarchy)
            {
                float dist = Vector3.Distance(player.transform.position, enemy.transform.position);
                int clock = GetClockDirection(player.transform.forward, enemy.transform.position - player.transform.position);

                if (enemy.IsChasing)
                {
                    sb.Append($"ALERT! Kamla is chasing you, only {dist:F0} meters away at {clock} o'clock! ");
                }
                else if (enemy.IsInvestigating)
                {
                    sb.Append($"Kamla is nearby, investigating at {dist:F0} meters, {clock} o'clock. ");
                }
                else
                {
                    sb.Append($"Kamla is roaming, {dist:F0} meters away at {clock} o'clock. ");
                }
            }

            PrismSpeechProvider.Speak(sb.ToString(), true);
        }

        private static int GetClockDirection(Vector3 forward, Vector3 toTarget)
        {
            toTarget.y = 0;
            if (toTarget.sqrMagnitude < 0.001f) return 12;
            toTarget.Normalize();

            float angle = Vector3.SignedAngle(forward, toTarget, Vector3.up);
            if (angle < 0) angle += 360f;

            int hour = Mathf.RoundToInt(angle / 30f) % 12;
            return hour == 0 ? 12 : hour;
        }
    }
}
