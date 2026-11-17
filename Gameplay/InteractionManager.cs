using System;
using System.Collections.Generic;
using UnityEngine;
using Il2Cpp;
using KamlaAccessibility.Speech;
using KamlaAccessibility.Audio;

namespace KamlaAccessibility.Gameplay
{
    public class InteractionOption
    {
        public string Title { get; set; }
        public Action Action { get; set; }

        public InteractionOption(string title, Action action)
        {
            Title = title;
            Action = action;
        }
    }

    /// <summary>
    /// Handles mouse-free interaction with the Enter key, automatically performing
    /// the primary action or opening an audio numbered choice menu when multiple options are present.
    /// </summary>
    public class InteractionManager
    {
        private bool _isMenuActive = false;
        private List<InteractionOption> _activeOptions = new List<InteractionOption>();

        public bool IsMenuActive => _isMenuActive;

        public void Update(Transform playerTransform)
        {
            // If the numbered menu is active, listen for 1-9 or Escape
            if (_isMenuActive)
            {
                HandleMenuInput();
                return;
            }

            // Enter key triggers interaction
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                TriggerInteraction(playerTransform);
            }
        }

        private void TriggerInteraction(Transform playerTransform)
        {
            if (playerTransform == null)
            {
                var cam = Camera.main;
                if (cam != null) playerTransform = cam.transform;
            }

            if (playerTransform == null) return;

            Vector3 playerPos = playerTransform.position;
            float interactRadius = 2.4f;

            var options = new List<InteractionOption>();

            // 1. Check ItemPickers
            var items = UnityEngine.Object.FindObjectsOfType<ItemPicker>();
            foreach (var item in items)
            {
                if (item != null && item.gameObject.activeInHierarchy && Vector3.Distance(playerPos, item.transform.position) <= interactRadius)
                {
                    string itemName = !string.IsNullOrEmpty(item.textString) ? item.textString : item.gameObject.name;
                    options.Add(new InteractionOption($"Pick up {itemName}", () =>
                    {
                        item.AutoPickUp();
                        AudioCueGenerator.PlayNavSelect();
                        PrismSpeechProvider.Speak($"Picked up {itemName}.", true);
                    }));
                }
            }

            // 2. Check Doors
            var doors = UnityEngine.Object.FindObjectsOfType<Door>();
            foreach (var door in doors)
            {
                if (door != null && door.gameObject.activeInHierarchy && Vector3.Distance(playerPos, door.transform.position) <= interactRadius + 0.6f)
                {
                    string state = door.isOpen ? "Close" : "Open";
                    options.Add(new InteractionOption($"{state} Door", () =>
                    {
                        door.playerDoorOpen();
                        AudioCueGenerator.PlayNavSelect();
                        PrismSpeechProvider.Speak($"Door toggled.", true);
                    }));
                }
            }

            // 3. Check Almirahs
            var almirahs = UnityEngine.Object.FindObjectsOfType<OpenableAlmirah>();
            foreach (var alm in almirahs)
            {
                if (alm != null && alm.gameObject.activeInHierarchy && Vector3.Distance(playerPos, alm.transform.position) <= interactRadius)
                {
                    string state = alm.isOpen ? "Close" : "Open";
                    options.Add(new InteractionOption($"{state} Almirah", () =>
                    {
                        if (alm.lGate != null) alm.lGate.SetTrigger(alm.isOpen ? "Close" : "Open");
                        if (alm.rGate != null) alm.rGate.SetTrigger(alm.isOpen ? "Close" : "Open");
                        alm.isOpen = !alm.isOpen;
                        AudioCueGenerator.PlayNavSelect();
                        PrismSpeechProvider.Speak($"Almirah {(alm.isOpen ? "opened" : "closed")}.", true);
                    }));
                }
            }

            // 4. Check Table Drawers
            var drawers = UnityEngine.Object.FindObjectsOfType<TableDrawer>();
            foreach (var dr in drawers)
            {
                if (dr != null && dr.gameObject.activeInHierarchy && Vector3.Distance(playerPos, dr.transform.position) <= interactRadius)
                {
                    options.Add(new InteractionOption("Toggle Drawer", () =>
                    {
                        if (dr.anim != null) dr.anim.SetTrigger("Open");
                        AudioCueGenerator.PlayNavSelect();
                        PrismSpeechProvider.Speak("Drawer toggled.", true);
                    }));
                }
            }

            // Handle options
            if (options.Count == 0)
            {
                AudioCueGenerator.PlayBoundaryHit();
                PrismSpeechProvider.Speak("Nothing close enough to interact with.", true);
                return;
            }

            if (options.Count == 1)
            {
                // Execute directly!
                options[0].Action.Invoke();
                return;
            }

            // Multiple options: Open audio numbered menu
            _activeOptions = options;
            _isMenuActive = true;

            AudioCueGenerator.PlayNavSelect();
            var sb = new System.Text.StringBuilder();
            sb.Append("Multiple options: ");
            for (int i = 0; i < Math.Min(options.Count, 9); i++)
            {
                sb.Append($"Press {i + 1} for {options[i].Title}. ");
            }
            PrismSpeechProvider.Speak(sb.ToString(), true);
        }

        private void HandleMenuInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseMenu("Menu cancelled.");
                return;
            }

            // Keys 1 to 9
            for (int i = 0; i < Math.Min(_activeOptions.Count, 9); i++)
            {
                KeyCode numKey = KeyCode.Alpha1 + i;
                KeyCode padKey = KeyCode.Keypad1 + i;

                if (Input.GetKeyDown(numKey) || Input.GetKeyDown(padKey))
                {
                    var opt = _activeOptions[i];
                    _isMenuActive = false;
                    opt.Action?.Invoke();
                    return;
                }
            }
        }

        public void CloseMenu(string msg = null)
        {
            _isMenuActive = false;
            _activeOptions.Clear();
            if (!string.IsNullOrEmpty(msg))
            {
                PrismSpeechProvider.Speak(msg, true);
            }
        }
    }
}
