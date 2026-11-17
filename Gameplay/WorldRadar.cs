using System;
using System.Collections.Generic;
using UnityEngine;
using KamlaAccessibility.Speech;
using KamlaAccessibility.Audio;
using KamlaAccessibility.Core;

namespace KamlaAccessibility.Gameplay
{
    /// <summary>
    /// Scans nearby interactables and items and announces their distance and clock direction.
    /// Also announces compass directions.
    /// </summary>
    public static class WorldRadar
    {
        public static void ScanNearby(Transform playerTransform)
        {
            if (playerTransform == null)
            {
                var cam = Camera.main;
                if (cam != null) playerTransform = cam.transform;
            }

            if (playerTransform == null)
            {
                PrismSpeechProvider.Speak("Cannot determine player position.", true);
                return;
            }

            Vector3 playerPos = playerTransform.position;
            Vector3 playerForward = playerTransform.forward;
            playerForward.y = 0;
            playerForward.Normalize();

            float maxRange = ModConfig.RadarMaxRange;

            // Find all Colliders within maxRange
            var colliders = Physics.OverlapSphere(playerPos, maxRange);
            var itemsFound = new List<(string name, float distance, int clockHour, Vector3 pos)>();
            var seenNames = new HashSet<string>();

            foreach (var col in colliders)
            {
                if (col == null || !col.gameObject.activeInHierarchy) continue;

                var go = col.gameObject;
                string cleanName = IdentifyInteractable(go);
                if (string.IsNullOrEmpty(cleanName)) continue;

                Vector3 targetPos = col.bounds.center;
                float dist = Vector3.Distance(playerPos, targetPos);
                if (dist < 0.5f) continue; // Skip player's own colliders

                // Prevent listing duplicate items at the same spot
                string key = $"{cleanName}_{(int)targetPos.x}_{(int)targetPos.z}";
                if (seenNames.Contains(key)) continue;
                seenNames.Add(key);

                int clock = GetClockDirection(playerForward, targetPos - playerPos);
                itemsFound.Add((cleanName, dist, clock, targetPos));
            }

            if (itemsFound.Count == 0)
            {
                AudioCueGenerator.PlayBoundaryHit();
                PrismSpeechProvider.Speak("No nearby items or interactables found.", true);
                return;
            }

            // Sort by distance (closest first)
            itemsFound.Sort((a, b) => a.distance.CompareTo(b.distance));

            AudioCueGenerator.PlayRadarBeep();

            var sb = new System.Text.StringBuilder();
            sb.Append($"{itemsFound.Count} nearby: ");

            int count = Math.Min(itemsFound.Count, 5);
            for (int i = 0; i < count; i++)
            {
                var item = itemsFound[i];
                sb.Append($"{item.name}, {item.distance:F1} meters at {item.clockHour} o'clock");
                if (i < count - 1) sb.Append("; ");
            }

            PrismSpeechProvider.Speak(sb.ToString(), true);
        }

        public static void AnnounceCompass(Transform playerTransform)
        {
            if (playerTransform == null)
            {
                var cam = Camera.main;
                if (cam != null) playerTransform = cam.transform;
            }

            if (playerTransform == null)
            {
                PrismSpeechProvider.Speak("Cannot determine orientation.", true);
                return;
            }

            float yaw = playerTransform.eulerAngles.y;
            string direction = GetCompassHeading(yaw);
            AudioCueGenerator.PlayNavTick();
            PrismSpeechProvider.Speak($"Facing {direction}, {(int)yaw} degrees", true);
        }

        private static int GetClockDirection(Vector3 forward, Vector3 toTarget)
        {
            toTarget.y = 0;
            if (toTarget.sqrMagnitude < 0.001f) return 12;
            toTarget.Normalize();

            float angle = Vector3.SignedAngle(forward, toTarget, Vector3.up);
            if (angle < 0) angle += 360f;

            // Map [0, 360) to 12 clock hours
            // 345° to 15° = 12 o'clock
            int hour = Mathf.RoundToInt(angle / 30f) % 12;
            return hour == 0 ? 12 : hour;
        }

        private static string GetCompassHeading(float degrees)
        {
            degrees = (degrees % 360f + 360f) % 360f;
            if (degrees >= 337.5f || degrees < 22.5f) return "North";
            if (degrees < 67.5f) return "North-East";
            if (degrees < 112.5f) return "East";
            if (degrees < 157.5f) return "South-East";
            if (degrees < 202.5f) return "South";
            if (degrees < 247.5f) return "South-West";
            if (degrees < 292.5f) return "West";
            return "North-West";
        }

        private static string IdentifyInteractable(GameObject go)
        {
            string n = go.name;

            // Match known interactive object tags and names in Kamla
            if (n.IndexOf("Door", StringComparison.OrdinalIgnoreCase) >= 0) return "Door";
            if (n.IndexOf("Drawer", StringComparison.OrdinalIgnoreCase) >= 0) return "Drawer";
            if (n.IndexOf("Almirah", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Cupboard", StringComparison.OrdinalIgnoreCase) >= 0) return "Cupboard";
            if (n.IndexOf("Chest", StringComparison.OrdinalIgnoreCase) >= 0) return "Chest";
            if (n.IndexOf("SmudgeStick", StringComparison.OrdinalIgnoreCase) >= 0) return "Smudge Stick";
            if (n.IndexOf("Key", StringComparison.OrdinalIgnoreCase) >= 0) return "Key";
            if (n.IndexOf("Brick", StringComparison.OrdinalIgnoreCase) >= 0) return "Puzzle Brick";
            if (n.IndexOf("Murti", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Ganesh", StringComparison.OrdinalIgnoreCase) >= 0) return "Ganesh Idol";
            if (n.IndexOf("Bell", StringComparison.OrdinalIgnoreCase) >= 0) return "Temple Bell";
            if (n.IndexOf("Note", StringComparison.OrdinalIgnoreCase) >= 0) return "Note";
            if (n.IndexOf("Havan", StringComparison.OrdinalIgnoreCase) >= 0) return "Havan Altar";
            if (n.IndexOf("Matches", StringComparison.OrdinalIgnoreCase) >= 0) return "Matches";
            if (n.IndexOf("HolyWater", StringComparison.OrdinalIgnoreCase) >= 0) return "Holy Water";
            if (n.IndexOf("Kamla", StringComparison.OrdinalIgnoreCase) >= 0) return "Kamla";

            return null;
        }
    }
}
