using System;
using UnityEngine;
using KamlaAccessibility.Speech;
using KamlaAccessibility.Audio;

namespace KamlaAccessibility.Gameplay
{
    /// <summary>
    /// Tracks and announces interactable objects in front of the player (doors, drawers, items, puzzles).
    /// </summary>
    public class InteractionDescriber
    {
        private GameObject _lastTarget = null;
        private string _lastTargetName = string.Empty;
        private float _timeSinceLastAnnouncement = 0f;
        private const float MinReannounceTime = 1.5f;

        public void UpdateTarget(GameObject target, string interactionPrompt = null)
        {
            if (target == null)
            {
                _lastTarget = null;
                _lastTargetName = string.Empty;
                return;
            }

            _timeSinceLastAnnouncement += Time.deltaTime;

            if (target != _lastTarget || (_timeSinceLastAnnouncement > MinReannounceTime && string.IsNullOrEmpty(_lastTargetName)))
            {
                _lastTarget = target;
                _timeSinceLastAnnouncement = 0f;

                string desc = BuildDescription(target, interactionPrompt);
                if (!string.IsNullOrEmpty(desc) && desc != _lastTargetName)
                {
                    _lastTargetName = desc;
                    AudioCueGenerator.PlayNavTick();
                    PrismSpeechProvider.Speak(desc, true);
                }
            }
        }

        private string BuildDescription(GameObject go, string prompt)
        {
            if (go == null) return string.Empty;

            string name = CleanName(go.name);

            // Check for prompt
            if (!string.IsNullOrEmpty(prompt))
            {
                return $"{name}. {prompt}";
            }

            return name;
        }

        private string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Interactable";

            name = name.Replace("(Clone)", "").Trim();
            name = name.Replace("_", " ");

            // Clean up internal naming conventions
            if (name.IndexOf("Door", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Door";
            if (name.IndexOf("Drawer", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Drawer";
            if (name.IndexOf("Almirah", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Cupboard", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Cupboard";
            if (name.IndexOf("Chest", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Chest";
            if (name.IndexOf("SmudgeStick", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Smudge Stick";
            if (name.IndexOf("Key", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Key";
            if (name.IndexOf("Brick", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Puzzle Brick";
            if (name.IndexOf("Murti", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Ganesh Idol";
            if (name.IndexOf("Bell", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Temple Bell";
            if (name.IndexOf("Note", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Note";

            return name;
        }
    }
}
