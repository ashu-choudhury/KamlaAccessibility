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
        private readonly SpatialBeacons _beacons = new SpatialBeacons();
        private readonly ObstacleAwareness _obstacles = new ObstacleAwareness();
        private readonly PuzzleAccessibility _puzzles = new PuzzleAccessibility();
        internal HotspotManager Hotspots => _hotspotManager;
        internal PuzzleAccessibility Puzzles => _puzzles;

        private int? _lastSceneHandle;
        private TPScript _lastCover;
        private string _lastErrorMessage;
        private float _nextErrorLog;

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
                if (!_puzzles.IsModalActive && !_interactionManager.IsMenuActive) _uiController.Update();
                _puzzles.UpdateRecipe();
                if (Application.isFocused) _puzzles.AnnounceSuitcaseExit();

                // 4. Gameplay: Hotspots, Auto-walk, Interaction, and Crosshair Look
                if (!_uiController.OptionsActive) UpdateGameplay();
            }
            catch (Exception ex)
            {
                if (_lastErrorMessage != ex.Message || Time.realtimeSinceStartup >= _nextErrorLog)
                {
                    _lastErrorMessage = ex.Message;
                    _nextErrorLog = Time.realtimeSinceStartup + 10f;
                    MelonLogger.Warning($"[AccessibilityCoordinator.Update] {ex}");
                }
                if (_hotspotManager.IsAutoWalking)
                    _hotspotManager.StopAutoWalk("Auto-walk stopped because movement failed.");
            }
        }

        private void CheckSceneChange()
        {
            var scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.name) || _lastSceneHandle == scene.handle) return;

            _lastSceneHandle = scene.handle;
            OnSceneChanged(scene.name);
        }

        private void OnSceneChanged(string sceneName)
        {
            _hotspotManager.ResetScene();
            _uiController.ResetOptions();
            _lastCover = null;
            _interactionManager.CloseMenu();
            _interactionDescriber.UpdateTarget(null);
            _beacons.ResetScene();
            _obstacles.Reset();
            _puzzles.Reset();
            SubtitleReader.Reset();
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
            if (Input.GetKeyDown(KeyCode.F3))
            {
                bool enabled = ModConfig.ToggleSubtitles();
                SubtitleReader.Reset();
                PrismSpeechProvider.Stop();
                PrismSpeechProvider.Speak(enabled ? "Subtitle reading on." : "Subtitle reading off.", true);
                MelonLogger.Msg($"[Accessibility] Subtitle reading {(enabled ? "on" : "off")}.");
            }

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

            if (Application.isFocused && (Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.K)) &&
                !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt))
            {
                var player = UnityEngine.Object.FindObjectOfType<PlayerMovement>();
                if (Input.GetKeyDown(KeyCode.L)) StatusReporter.ReportLives(player);
                else StatusReporter.ReportKamlaLocation(player);
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
            if (player != null && player.IsHiding && player.TpScriptRef != null) _lastCover = player.TpScriptRef;
            if (player != null && player.IsHiding && !player.IsGettingOut && !player.IsKilling &&
                Time.timeScale > 0f && Application.isFocused &&
                (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) &&
                !HidingController.TryLeave(player))
                PrismSpeechProvider.Speak("You are still in cover. Try again when the area is clear.", true);
            if (player == null || !player.isActiveAndEnabled || !Application.isFocused ||
                Time.timeScale <= 0f || player.IsKilling || player.isPlayingVid || player.IsGettingOut || player.IsHiding)
            {
                if (_hotspotManager.IsAutoWalking) _hotspotManager.StopAutoWalk();
                if (_interactionManager.IsMenuActive) _interactionManager.CloseMenu();
                _interactionDescriber.UpdateTarget(null);
                _beacons.Stop();
                _obstacles.Reset();
                _puzzles.StopRitual();
                return;
            }

            if (_lastCover != null && player.CanInteract)
            {
                var cover = _lastCover;
                _lastCover = null;
                HidingController.ClearObstructedExit(player, cover);
                var target = _hotspotManager.Selection;
                PrismSpeechProvider.Speak(target != null && target.GameObject != null && target.GameObject.activeInHierarchy
                    ? $"Out of cover. Press Delete to walk to {target.CleanName}." : "Out of cover.", true);
            }

            if (_puzzles.UpdateSuitcase(player) || _puzzles.UpdateRitual(player))
            {
                if (_hotspotManager.IsAutoWalking) _hotspotManager.StopAutoWalk();
                _beacons.Stop();
                _obstacles.Reset();
                return;
            }

            if (!_interactionManager.IsMenuActive) _hotspotManager.HandleInput(player);
            else if (_hotspotManager.IsAutoWalking) _hotspotManager.StopAutoWalk();
            if (_hotspotManager.IsAutoWalking)
            {
                _beacons.Stop();
                _interactionDescriber.UpdateTarget(null);
                return;
            }

            if (!_interactionManager.IsMenuActive)
            {
                _beacons.Update(player);
                _puzzles.UpdateTrail();
                _obstacles.Update(player, _hotspotManager.TakeBlockedDirection());
            }
            else
            {
                _beacons.Stop();
            }

            _interactionManager.Update(player, _puzzles, _obstacles.Focus != null
                ? _obstacles.Focus : _hotspotManager.Selection?.GameObject, _hotspotManager.ArrivedTarget);
            if (ModConfig.EnableSubtitles && _obstacles.Focus == null && !_interactionManager.IsMenuActive && !_puzzles.IsModalActive)
                UpdateCrosshairLook();
            else
                _interactionDescriber.UpdateTarget(null);
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
                           "F5 refreshes nearby objects and repeats active footstep directions. " +
                         "Hotspots announce distance and clock direction. Kamla has a directional warning sound. " +
                         "Delete key to auto-walk to selected object. " +
                         "Enter key to activate or interact. " +
                           "Up and Down browse the action menu. At a suitcase, type the digits with the number keys or number pad, then press Enter. " +
                         "F3 toggles subtitle and automatic crosshair reading. " +
                           "L reports lives remaining. K reports Kamla's location. " +
                         (ModConfig.AutomaticHiding ? "Automatic hiding is on when Kamla chases you. Move manually or press Delete to cancel. " : "Automatic hiding is off. ") +
                         "F10 to minimize graphics. " +
                         "Keypad 0 or Alt F for nearby radar. " +
                         "Keypad 8 or Alt C for compass. " +
                         "Left Control to silence.";
            AudioCueGenerator.PlayNavSelect();
            PrismSpeechProvider.Speak(msg, true);
        }
    }
}
