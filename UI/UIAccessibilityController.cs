using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Il2CppTMPro;
using KamlaAccessibility.Speech;
using KamlaAccessibility.Audio;
using KamlaAccessibility.Core;

namespace KamlaAccessibility.UI
{
    /// <summary>
    /// Ensures 100% mouse-free screen reader navigation for all menus, settings screens,
    /// and dialogs in Kamla.
    /// </summary>
    public class UIAccessibilityController
    {
        private GameObject _lastSelected = null;
        private string _lastSpoken = string.Empty;
        private float _lastSliderVal = -999f;
        private GameObject _lastCanvas = null;

        public void Update()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            // F10 hotkey to minimize graphics anytime in menu or gameplay
            if (Input.GetKeyDown(KeyCode.F10))
            {
                GraphicsOptimizer.MinimizeGraphics();
                return;
            }

            var current = eventSystem.currentSelectedGameObject;

            // Auto-focus first interactable when opening a new menu or canvas
            CheckActiveMenuChange();

            // If user pressed navigation keys (Arrows / Tab) and focus was lost, recover it
            bool navPressed = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) ||
                              Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow) ||
                              Input.GetKeyDown(KeyCode.Tab);

            if (navPressed && (current == null || !current.activeInHierarchy))
            {
                FocusFirstSelectable();
                return;
            }

            // Announce element when selection changes
            if (current != null && current.activeInHierarchy && current != _lastSelected)
            {
                _lastSelected = current;
                _lastSliderVal = -999f;

                string desc = MenuDescriber.DescribeElement(current);
                if (!string.IsNullOrEmpty(desc) && desc != _lastSpoken)
                {
                    _lastSpoken = desc;
                    AudioCueGenerator.PlayNavTick();
                    PrismSpeechProvider.Speak(desc, true);
                }
            }

            // Track slider value adjustments while on the same slider
            if (current != null && current.activeInHierarchy)
            {
                var slider = current.GetComponent<Slider>();
                if (slider != null)
                {
                    if (Math.Abs(slider.value - _lastSliderVal) > 0.005f)
                    {
                        if (_lastSliderVal != -999f)
                        {
                            float pct = Mathf.Clamp01((slider.value - slider.minValue) / (slider.maxValue - slider.minValue)) * 100f;
                            string label = MenuDescriber.GetElementText(current);
                            if (string.IsNullOrEmpty(label)) label = "Slider";
                            AudioCueGenerator.PlayNavTick();
                            PrismSpeechProvider.Speak($"{label}: {(int)pct} percent", true);
                        }
                        _lastSliderVal = slider.value;
                    }
                }
            }
        }

        private void CheckActiveMenuChange()
        {
            // Find active root canvas in scene
            var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
            GameObject activeCanvas = null;
            foreach (var c in canvases)
            {
                if (c != null && c.gameObject.activeInHierarchy && c.enabled)
                {
                    // Check if canvas has active selectables
                    if (c.GetComponentInChildren<Selectable>(false) != null)
                    {
                        activeCanvas = c.gameObject;
                        break;
                    }
                }
            }

            if (activeCanvas != null && activeCanvas != _lastCanvas)
            {
                _lastCanvas = activeCanvas;
                string menuTitle = CleanCanvasTitle(activeCanvas.name);
                PrismSpeechProvider.Speak($"{menuTitle} opened.", true);
                FocusFirstSelectable();
            }
        }

        public void FocusFirstSelectable()
        {
            var selectables = UnityEngine.Object.FindObjectsOfType<Selectable>();
            foreach (var sel in selectables)
            {
                if (sel != null && sel.gameObject.activeInHierarchy && sel.interactable)
                {
                    sel.Select();
                    _lastSelected = sel.gameObject;
                    AudioCueGenerator.PlayNavSelect();
                    string desc = MenuDescriber.DescribeElement(sel.gameObject);
                    PrismSpeechProvider.Speak(desc, true);
                    return;
                }
            }
        }

        private string CleanCanvasTitle(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Menu";
            name = name.Replace("Canvas", "").Replace("_", " ").Trim();
            if (name.IndexOf("Graphics", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Settings", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Settings Menu";
            if (name.IndexOf("Pause", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Pause Menu";
            if (name.IndexOf("Main", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Main Menu";
            if (name.IndexOf("Death", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Restart", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Game Over Menu";

            return name;
        }
    }
}
