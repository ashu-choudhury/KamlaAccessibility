using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Il2Cpp;
using KamlaAccessibility.Speech;
using KamlaAccessibility.Audio;
using KamlaAccessibility.Core;

namespace KamlaAccessibility.UI
{
    /// <summary>
    /// Ensures 100% mouse-free screen reader navigation for all menus, settings screens,
    /// and pause dialogs in Kamla.
    /// </summary>
    public class UIAccessibilityController
    {
        private GameObject _lastSelected = null;
        private float _lastSliderVal = -999f;
        private GameObject _lastActiveCanvas = null;
        private float _menuCheckTimer = 0f;
        private readonly OptionsNavigation _options = new();
        internal bool OptionsActive { get; private set; }

        public void Update()
        {
            bool wasInOptions = OptionsActive;
            OptionsActive = _options.Update();
            if (OptionsActive) return;
            if (wasInOptions)
            {
                _lastActiveCanvas = null;
                _lastSelected = null;
                _menuCheckTimer = 0.2f;
            }
            // Escape key handling for Pause Menu
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                OnEscapePressed();
            }

            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            // Periodically check if a new canvas or dialog popped up (e.g. Pause Menu, Death, Settings)
            _menuCheckTimer += Time.unscaledDeltaTime;
            if (_menuCheckTimer >= 0.2f)
            {
                _menuCheckTimer = 0f;
                CheckActiveMenuChange();
            }

            var current = eventSystem.currentSelectedGameObject;
            if (current == null || !current.activeInHierarchy)
            {
                _lastSelected = null;
            }

            // If user pressed navigation keys (Arrows / Tab) and focus was lost, recover it
            bool navPressed = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) ||
                              Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow) ||
                              Input.GetKeyDown(KeyCode.Tab);

            if (navPressed && (current == null || !current.activeInHierarchy))
            {
                FocusFirstSelectable();
                return;
            }

            // Tab cycling through selectables manually if Unity's navigation doesn't cycle
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                CycleNextSelectable(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1);
                return;
            }

            // Announce element when selection changes
            if (current != null && current.activeInHierarchy && current != _lastSelected)
            {
                _lastSelected = current;
                _lastSliderVal = -999f;

                string desc = MenuDescriber.DescribeElement(current);
                if (!string.IsNullOrEmpty(desc))
                {
                    AudioCueGenerator.PlayNavTick();
                    PrismSpeechProvider.Speak(desc, true);
                }
            }

            // Track slider adjustments while on the same slider
            if (current != null && current.activeInHierarchy)
            {
                var slider = current.GetComponent<Slider>();
                if (slider != null)
                {
                    if (Math.Abs(slider.value - _lastSliderVal) > 0.005f)
                    {
                        if (_lastSliderVal != -999f && slider.maxValue > slider.minValue)
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

        internal void ResetOptions()
        {
            _options.Reset();
            OptionsActive = false;
        }

        private void OnEscapePressed()
        {
            // Give the pause menu a moment to toggle its canvas
            MelonLoader.MelonCoroutines.Start(DelayedPauseCheck());
        }

        private System.Collections.IEnumerator DelayedPauseCheck()
        {
            yield return new WaitForSecondsRealtime(0.08f);

            var pauseManager = UnityEngine.Object.FindObjectOfType<PauseMenuAndRecipeManager>();
            if (pauseManager != null && pauseManager.gameObject.activeInHierarchy)
            {
                PrismSpeechProvider.Speak("Pause Menu.", true);
                FocusFirstSelectable();
            }
        }

        private void CheckActiveMenuChange()
        {
            var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
            GameObject activeCanvas = null;

            foreach (var c in canvases)
            {
                if (c != null && c.gameObject.activeInHierarchy && c.enabled)
                {
                    // Ignore HUD/crosshair canvases
                    string cName = c.gameObject.name;
                    if (cName.IndexOf("Crosshair", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        cName.IndexOf("HUD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        cName.IndexOf("Stamina", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }

                    if (c.GetComponentInChildren<Selectable>(false) != null)
                    {
                        activeCanvas = c.gameObject;
                        break;
                    }
                }
            }

            if (activeCanvas != _lastActiveCanvas)
            {
                _lastActiveCanvas = activeCanvas;
                if (activeCanvas == null) return;
                string title = CleanCanvasTitle(activeCanvas.name);
                FocusFirstSelectable($"{title} opened.");
            }
        }

        public void FocusFirstSelectable(string announcement = null)
        {
            var selectables = GetActiveSelectables();
            if (selectables.Count > 0)
            {
                var target = selectables[0];
                target.Select();
                _lastSelected = target.gameObject;
                _lastSliderVal = -999f;
                AudioCueGenerator.PlayNavSelect();
                string desc = MenuDescriber.DescribeElement(target.gameObject);
                PrismSpeechProvider.Speak(string.IsNullOrEmpty(announcement) ? desc : $"{announcement} {desc}", true);
            }
            else
            {
                PrismSpeechProvider.Speak(announcement, true);
            }
        }

        private void CycleNextSelectable(int direction)
        {
            var selectables = GetActiveSelectables();
            if (selectables.Count == 0) return;

            var current = EventSystem.current.currentSelectedGameObject;
            int currentIndex = selectables.FindIndex(s => s.gameObject == current);
            int nextIndex = currentIndex < 0
                ? (direction > 0 ? 0 : selectables.Count - 1)
                : (currentIndex + direction + selectables.Count) % selectables.Count;

            var target = selectables[nextIndex];
            target.Select();
            _lastSelected = target.gameObject;
            _lastSliderVal = -999f;
            AudioCueGenerator.PlayNavTick();
            string desc = MenuDescriber.DescribeElement(target.gameObject);
            PrismSpeechProvider.Speak(desc, true);
        }

        private List<Selectable> GetActiveSelectables()
        {
            var results = new List<Selectable>();
            var all = UnityEngine.Object.FindObjectsOfType<Selectable>();
            foreach (var sel in all)
            {
                if (sel != null && sel.isActiveAndEnabled && sel.IsInteractable())
                {
                    // Filter out HUD elements
                    string pName = sel.transform.root.name;
                    if (pName.IndexOf("HUD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        pName.IndexOf("Crosshair", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }
                    results.Add(sel);
                }
            }
            return results;
        }

        private string CleanCanvasTitle(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Menu";
            name = name.Replace("Canvas", "").Replace("_", " ").Trim();
            if (string.IsNullOrEmpty(name)) return "Menu";
            if (name.IndexOf("Graphics", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Settings", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Settings Menu";
            if (name.IndexOf("Pause", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Pause Menu";
            if (name.IndexOf("Main", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Main Menu";
            if (name.IndexOf("Death", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Restart", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Game Over Menu";
            if (name.IndexOf("Recipe", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Recipe Book";

            return name;
        }
    }
}
