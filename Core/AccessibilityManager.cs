using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KamlaAccessibility.Speech;
using KamlaAccessibility.Audio;
using KamlaAccessibility.Gameplay;
using KamlaAccessibility.UI;
using MelonLoader;

namespace KamlaAccessibility.Core
{
    /// <summary>
    /// Central MonoBehaviour coordinating input hotkeys, UI selection announcements,
    /// and interaction narration.
    /// </summary>
    public class AccessibilityManager : MonoBehaviour
    {
        public static AccessibilityManager Instance { get; private set; }

        private GameObject _lastSelectedGameObject = null;
        private string _lastSpokenMenuText = string.Empty;
        private float _timeSinceLastSelectionCheck = 0f;

        private readonly InteractionDescriber _interactionDescriber = new InteractionDescriber();

        public AccessibilityManager(IntPtr ptr) : base(ptr) { }

        public static void EnsureInstance()
        {
            if (Instance == null)
            {
                var go = new GameObject("KamlaAccessibilityHost");
                UnityEngine.Object.DontDestroyOnLoad(go);
                Instance = go.AddComponent<AccessibilityManager>();
                AudioCueGenerator.Initialize(go);
                AudioBeacon.EnsureBeacon(go);
                MelonLogger.Msg("[KamlaAccessibility] AccessibilityManager component attached to host.");
            }
        }

        private void Start()
        {
            AudioCueGenerator.Initialize(gameObject);
        }

        private void Update()
        {
            HandleHotkeys();
            UpdateUINavigation();
            UpdatePlayerInteraction();
        }

        private void HandleHotkeys()
        {
            // Left or Right Control: Silence Speech
            if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl))
            {
                PrismSpeechProvider.Stop();
            }

            // Keypad 5 or Alt + R: Repeat Last Spoken Text
            if (Input.GetKeyDown(KeyCode.Keypad5) || ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && Input.GetKeyDown(KeyCode.R)))
            {
                PrismSpeechProvider.RepeatLast();
            }

            // Keypad 0 or Alt + F: Scan Nearby Radar
            if (Input.GetKeyDown(KeyCode.Keypad0) || ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && Input.GetKeyDown(KeyCode.F)))
            {
                Transform pt = GetPlayerTransform();
                WorldRadar.ScanNearby(pt);
            }

            // Keypad 8 or Alt + C: Compass Orientation
            if (Input.GetKeyDown(KeyCode.Keypad8) || ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && Input.GetKeyDown(KeyCode.C)))
            {
                Transform pt = GetPlayerTransform();
                WorldRadar.AnnounceCompass(pt);
            }

            // F1: Help / Shortcuts
            if (Input.GetKeyDown(KeyCode.F1))
            {
                SpeakHelp();
            }
        }

        private void UpdateUINavigation()
        {
            _timeSinceLastSelectionCheck += Time.unscaledDeltaTime;
            if (_timeSinceLastSelectionCheck < 0.05f) return;
            _timeSinceLastSelectionCheck = 0f;

            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            var current = eventSystem.currentSelectedGameObject;

            // If user pressed navigation keys (Arrows / Tab) and nothing is currently selected, auto-select first active button
            bool navPressed = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) ||
                              Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow) ||
                              Input.GetKeyDown(KeyCode.Tab);

            if (navPressed && (current == null || !current.activeInHierarchy))
            {
                FocusFirstInteractable();
                return;
            }

            // If selection changed, describe it
            if (current != null && current != _lastSelectedGameObject && current.activeInHierarchy)
            {
                _lastSelectedGameObject = current;
                string desc = MenuDescriber.DescribeElement(current);
                if (!string.IsNullOrEmpty(desc) && desc != _lastSpokenMenuText)
                {
                    _lastSpokenMenuText = desc;
                    AudioCueGenerator.PlayNavSelect();
                    PrismSpeechProvider.Speak(desc, true);
                }
            }
        }

        private void FocusFirstInteractable()
        {
            var selectables = UnityEngine.Object.FindObjectsOfType<Selectable>();
            foreach (var sel in selectables)
            {
                if (sel != null && sel.gameObject.activeInHierarchy && sel.interactable)
                {
                    sel.Select();
                    AudioCueGenerator.PlayNavTick();
                    return;
                }
            }
        }

        private void UpdatePlayerInteraction()
        {
            // Inspect what the camera/player is looking at
            var cam = Camera.main;
            if (cam == null) return;

            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, 3.5f))
            {
                var hitGo = hit.collider.gameObject;
                _interactionDescriber.UpdateTarget(hitGo);
            }
            else
            {
                _interactionDescriber.UpdateTarget(null);
            }
        }

        private Transform GetPlayerTransform()
        {
            var cam = Camera.main;
            if (cam != null) return cam.transform;

            return transform;
        }

        private void SpeakHelp()
        {
            string backend = PrismSpeechProvider.BackendName;
            string msg = $"Kamla Accessibility Mod Active. Speech backend: {backend}. " +
                         "Controls: Left Control to silence. " +
                         "Keypad 5 or Alt R to repeat. " +
                         "Keypad 0 or Alt F to scan nearby items. " +
                         "Keypad 8 or Alt C for compass. " +
                         "Arrow keys and Tab to navigate menus.";
            AudioCueGenerator.PlayNavSelect();
            PrismSpeechProvider.Speak(msg, true);
        }
    }
}
