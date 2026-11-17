using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Il2Cpp;
using KamlaAccessibility.Speech;
using KamlaAccessibility.Audio;
using KamlaAccessibility.Gameplay;
using KamlaAccessibility.UI;
using MelonLoader;

namespace KamlaAccessibility.Core
{
    /// <summary>
    /// Pure C# central coordinator driven directly by MelonMod.OnUpdate(),
    /// immune to Unity scene unloads and IL2CPP domain reset quirks.
    /// </summary>
    public class AccessibilityCoordinator
    {
        public static AccessibilityCoordinator Instance { get; private set; }

        private readonly UIAccessibilityController _uiController = new UIAccessibilityController();
        private readonly HotspotManager _hotspotManager = new HotspotManager();
        private readonly InteractionManager _interactionManager = new InteractionManager();
        private readonly InteractionDescriber _interactionDescriber = new InteractionDescriber();

        private string _lastSceneName = string.Empty;

        public static void Initialize()
        {
            if (Instance == null)
            {
                Instance = new AccessibilityCoordinator();
                MelonLogger.Msg("[AccessibilityCoordinator] Initialized pure C# coordinator.");
            }
        }

        public void Update()
        {
            try
            {
                // 1. Check Scene changes
                CheckSceneChange();

                // 2. Global Hotkeys
                HandleGlobalHotkeys();

                // 3. UI Navigation (Menus, Pause, Settings)
                _uiController.Update();

                // 4. Gameplay: Hotspots, Auto-walk, Interaction, and Crosshair Look
                UpdateGameplay();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[AccessibilityCoordinator.Update] {ex.Message}");
            }
        }

        private void CheckSceneChange()
        {
            try
            {
                string activeScene = SceneManager.GetActiveScene().name;
                if (!string.IsNullOrEmpty(activeScene) && activeScene != _lastSceneName)
                {
                    _lastSceneName = activeScene;
                    OnSceneChanged(activeScene);
                }
            }
            catch { }
        }

        public void OnSceneChanged(string sceneName)
        {
            MelonLogger.Msg($"[AccessibilityCoordinator] Scene changed: '{sceneName}'");

            string friendlyName = sceneName;
            if (sceneName.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sceneName.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sceneName.IndexOf("Start", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                friendlyName = "Main Menu";
            }
            else if (sceneName.IndexOf("Game", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     sceneName.IndexOf("Haveli", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     sceneName.IndexOf("Level", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                friendlyName = "The Haveli";
            }

            AudioCueGenerator.PlayNavSelect();
            PrismSpeechProvider.Speak($"{friendlyName} loaded.", true);
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
                Transform pt = player != null ? player.transform : (Camera.main != null ? Camera.main.transform : null);
                WorldRadar.ScanNearby(pt);
            }

            // Keypad 8 or Alt + C: Compass Orientation
            if (Input.GetKeyDown(KeyCode.Keypad8) || ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && Input.GetKeyDown(KeyCode.C)))
            {
                var player = UnityEngine.Object.FindObjectOfType<PlayerMovement>();
                Transform pt = player != null ? player.transform : (Camera.main != null ? Camera.main.transform : null);
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

        private void UpdateGameplay()
        {
            var player = UnityEngine.Object.FindObjectOfType<PlayerMovement>();
            Transform playerTransform = player != null ? player.transform : (Camera.main != null ? Camera.main.transform : null);
            CharacterController controller = player != null ? player.Controller : null;

            if (playerTransform != null)
            {
                // Handle Hotspots & Auto-walk (Home, End, PageUp, PageDown, Delete)
                _hotspotManager.HandleInput(playerTransform, controller);

                // Handle Contextual Activation & Menus (Enter)
                _interactionManager.Update(playerTransform);

                // Handle Crosshair Look Narration
                UpdateCrosshairLook();
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
