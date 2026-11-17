using System;
using UnityEngine;
using UnityEngine.EventSystems;
using Il2Cpp;
using KamlaAccessibility.Speech;
using KamlaAccessibility.Audio;
using KamlaAccessibility.Gameplay;
using KamlaAccessibility.UI;
using MelonLoader;

namespace KamlaAccessibility.Core
{
    /// <summary>
    /// Central coordinator for UI navigation, Hotspot browsing, auto-walk,
    /// contextual Enter interaction, emergency hiding, and player status reporting.
    /// </summary>
    public class AccessibilityManager : MonoBehaviour
    {
        public static AccessibilityManager Instance { get; private set; }

        private readonly UIAccessibilityController _uiController = new UIAccessibilityController();
        private readonly HotspotManager _hotspotManager = new HotspotManager();
        private readonly InteractionManager _interactionManager = new InteractionManager();
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
            HandleGlobalHotkeys();

            // 1. Menu and UI Navigation
            _uiController.Update();

            // 2. Gameplay Navigation and Interactions (when not in a numbered action menu)
            var player = UnityEngine.Object.FindObjectOfType<PlayerMovement>();
            Transform playerTransform = player != null ? player.transform : (Camera.main != null ? Camera.main.transform : transform);
            CharacterController controller = player != null ? player.Controller : null;

            // Handle Hotspots & Auto-walk (Home, End, PageUp, PageDown, Delete)
            _hotspotManager.HandleInput(playerTransform, controller);

            // Handle Contextual Activation & Menus (Enter)
            _interactionManager.Update(playerTransform);

            // Handle Crosshair Look Narration
            UpdateCrosshairLook();
        }

        private void HandleGlobalHotkeys()
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

            // H: Health & Player Status
            if (Input.GetKeyDown(KeyCode.H) && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt))
            {
                var player = UnityEngine.Object.FindObjectOfType<PlayerMovement>();
                StatusReporter.ReportStatus(player);
            }

            // Alt + H: Emergency Hide
            if ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && Input.GetKeyDown(KeyCode.H))
            {
                var player = UnityEngine.Object.FindObjectOfType<PlayerMovement>();
                HidingController.TryEmergencyHide(player);
            }

            // Keypad 0 or Alt + F: Scan Nearby Radar
            if (Input.GetKeyDown(KeyCode.Keypad0) || ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && Input.GetKeyDown(KeyCode.F)))
            {
                var player = UnityEngine.Object.FindObjectOfType<PlayerMovement>();
                Transform pt = player != null ? player.transform : (Camera.main != null ? Camera.main.transform : transform);
                WorldRadar.ScanNearby(pt);
            }

            // Keypad 8 or Alt + C: Compass Orientation
            if (Input.GetKeyDown(KeyCode.Keypad8) || ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && Input.GetKeyDown(KeyCode.C)))
            {
                var player = UnityEngine.Object.FindObjectOfType<PlayerMovement>();
                Transform pt = player != null ? player.transform : (Camera.main != null ? Camera.main.transform : transform);
                WorldRadar.AnnounceCompass(pt);
            }

            // F10: Minimize Graphic Settings
            if (Input.GetKeyDown(KeyCode.F10))
            {
                GraphicsOptimizer.MinimizeGraphics();
            }

            // F1: Help / Shortcuts
            if (Input.GetKeyDown(KeyCode.F1))
            {
                SpeakHelp();
            }
        }

        private void UpdateCrosshairLook()
        {
            var cam = Camera.main;
            if (cam == null) return;

            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, 3.0f))
            {
                _interactionDescriber.UpdateTarget(hit.collider.gameObject);
            }
            else
            {
                _interactionDescriber.UpdateTarget(null);
            }
        }

        private void SpeakHelp()
        {
            string backend = PrismSpeechProvider.BackendName;
            string msg = $"Kamla Accessibility Mod Active. Speech backend: {backend}. " +
                         "Hotspot Navigation: Home and End to change category. " +
                         "Page Up and Page Down to cycle objects. " +
                         "Delete key to auto-walk to selected object. " +
                         "Enter key to activate or interact. " +
                         "H to check health and status. " +
                         "Alt H to eagerly hide in nearest hiding spot. " +
                         "F10 to minimize graphics. " +
                         "Keypad 0 or Alt F for nearby radar. " +
                         "Keypad 8 or Alt C for compass. " +
                         "Left Control to silence.";
            AudioCueGenerator.PlayNavSelect();
            PrismSpeechProvider.Speak(msg, true);
        }
    }
}
