using System;
using UnityEngine;
using Il2Cpp;
using KamlaAccessibility.Speech;
using KamlaAccessibility.Audio;

namespace KamlaAccessibility.Gameplay
{
    /// <summary>
    /// Handles the Alt + H emergency hiding shortcut, eagerly finding the nearest hiding spot
    /// and safely concealing the character from Kamla.
    /// </summary>
    public static class HidingController
    {
        public static void TryEmergencyHide(PlayerMovement player)
        {
            if (player == null)
            {
                player = UnityEngine.Object.FindObjectOfType<PlayerMovement>();
            }

            if (player == null)
            {
                PrismSpeechProvider.Speak("Cannot find player controller.", true);
                return;
            }

            // If already hiding, toggle out of hiding
            if (player.IsHiding)
            {
                player.IsHiding = false;
                if (player.BackPoint != null)
                {
                    player.transform.position = player.BackPoint.position;
                }
                AudioCueGenerator.PlayNavSelect();
                PrismSpeechProvider.Speak("Exited hiding spot.", true);
                return;
            }

            Vector3 playerPos = player.transform.position;

            // Search for nearest hiding spot
            GameObject nearestSpot = null;
            float minDistance = float.MaxValue;
            string spotName = "Hiding Spot";

            // 1. Check HideDetectionColliders
            var hideColliders = UnityEngine.Object.FindObjectsOfType<HideDetectionCollider>();
            foreach (var hc in hideColliders)
            {
                if (hc != null && hc.gameObject.activeInHierarchy)
                {
                    float d = Vector3.Distance(playerPos, hc.transform.position);
                    if (d < minDistance)
                    {
                        minDistance = d;
                        nearestSpot = hc.gameObject;
                        spotName = IdentifyHidingSpotName(hc.gameObject.name);
                    }
                }
            }

            // 2. Check OpenableAlmirahs (Wardrobes)
            var almirahs = UnityEngine.Object.FindObjectsOfType<OpenableAlmirah>();
            foreach (var alm in almirahs)
            {
                if (alm != null && alm.gameObject.activeInHierarchy)
                {
                    float d = Vector3.Distance(playerPos, alm.transform.position);
                    if (d < minDistance)
                    {
                        minDistance = d;
                        nearestSpot = alm.gameObject;
                        spotName = "Haveli Wardrobe";
                    }
                }
            }

            if (nearestSpot == null || minDistance > 30f)
            {
                AudioCueGenerator.PlayBoundaryHit();
                PrismSpeechProvider.Speak("No hiding spot nearby!", true);
                return;
            }

            // Move and hide
            AudioCueGenerator.PlayNavSelect();
            PrismSpeechProvider.Speak($"Found nearest hiding spot: {spotName}, {minDistance:F1} meters away. Hiding now...", true);

            // Move player to hiding spot position
            if (player.Controller != null && player.Controller.enabled)
            {
                player.Controller.enabled = false;
                player.transform.position = nearestSpot.transform.position;
                player.Controller.enabled = true;
            }
            else
            {
                player.transform.position = nearestSpot.transform.position;
            }

            player.IsHiding = true;
            player.HiddenInObjectName = spotName;

            PrismSpeechProvider.Speak($"Hidden inside {spotName}.", false);
        }

        private static string IdentifyHidingSpotName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "Hiding Spot";
            raw = raw.Replace("(Clone)", "").Replace("_", " ").Trim();
            if (raw.IndexOf("Bed", StringComparison.OrdinalIgnoreCase) >= 0) return "Under Bed";
            if (raw.IndexOf("Wardrobe", StringComparison.OrdinalIgnoreCase) >= 0 || raw.IndexOf("Almirah", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Inside Wardrobe";
            if (raw.IndexOf("Cupboard", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Inside Cupboard";

            return "Safe Hiding Spot";
        }
    }
}
