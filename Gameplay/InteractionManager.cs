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
            float interactRadius = 3.0f;

            var options = new List<InteractionOption>();
            var seenObjects = new HashSet<int>();

            // 1. Check ItemPickers
            var items = UnityEngine.Object.FindObjectsOfType<ItemPicker>();
            foreach (var item in items)
            {
                if (item != null && item.gameObject.activeInHierarchy &&
                    Vector3.Distance(playerPos, item.transform.position) <= interactRadius &&
                    seenObjects.Add(item.gameObject.GetInstanceID()))
                {
                    string itemName = HotspotManager.CleanItemName(item);
                    options.Add(new InteractionOption($"Pick up {itemName}", () =>
                    {
                        item.AutoPickUp();
                        AudioCueGenerator.PlayNavSelect();
                        PrismSpeechProvider.Speak($"Picked up {itemName}.", true);
                    }));
                }
            }

            // 2. Check Keys
            var keys = UnityEngine.Object.FindObjectsOfType<Key>();
            foreach (var k in keys)
            {
                if (k != null && k.gameObject.activeInHierarchy && !k.HasCollectedKey &&
                    Vector3.Distance(playerPos, k.transform.position) <= interactRadius &&
                    seenObjects.Add(k.gameObject.GetInstanceID()))
                {
                    string keyName = HotspotManager.CleanKeyName(k);
                    options.Add(new InteractionOption($"Collect {keyName}", () =>
                    {
                        k.keyCollect();
                        AudioCueGenerator.PlayNavSelect();
                        PrismSpeechProvider.Speak($"Collected {keyName}.", true);
                    }));
                }
            }

            // 3. Check Doors (DoorScriptCode)
            var scriptDoors = UnityEngine.Object.FindObjectsOfType<DoorScriptCode>();
            foreach (var dsc in scriptDoors)
            {
                if (dsc != null && dsc.gameObject.activeInHierarchy &&
                    Vector3.Distance(playerPos, dsc.transform.position) <= interactRadius + 0.8f &&
                    seenObjects.Add(dsc.gameObject.GetInstanceID()))
                {
                    string doorName = HotspotManager.CleanDoorName(dsc.gameObject);
                    string actionVerb = dsc.open ? "Close" : "Open";
                    options.Add(new InteractionOption($"{actionVerb} {doorName}", () =>
                    {
                        dsc.playerDoorOpen();
                        AudioCueGenerator.PlayNavSelect();
                        PrismSpeechProvider.Speak($"{doorName} toggled.", true);
                    }));
                }
            }

            // 4. Check Standard Doors (Door)
            var doors = UnityEngine.Object.FindObjectsOfType<Door>();
            foreach (var door in doors)
            {
                if (door != null && door.gameObject.activeInHierarchy &&
                    Vector3.Distance(playerPos, door.transform.position) <= interactRadius + 0.8f &&
                    seenObjects.Add(door.gameObject.GetInstanceID()))
                {
                    string doorName = HotspotManager.CleanDoorName(door.gameObject);
                    string actionVerb = door.isOpen ? "Close" : "Open";
                    options.Add(new InteractionOption($"{actionVerb} {doorName}", () =>
                    {
                        door.playerDoorOpen();
                        AudioCueGenerator.PlayNavSelect();
                        PrismSpeechProvider.Speak($"{doorName} toggled.", true);
                    }));
                }
            }

            // 5. Check Locked Doors (DoorLocked)
            var lockedDoors = UnityEngine.Object.FindObjectsOfType<DoorLocked>();
            foreach (var dl in lockedDoors)
            {
                if (dl != null && dl.gameObject.activeInHierarchy &&
                    Vector3.Distance(playerPos, dl.transform.position) <= interactRadius + 0.8f &&
                    seenObjects.Add(dl.gameObject.GetInstanceID()))
                {
                    string doorName = HotspotManager.CleanDoorName(dl.gameObject);
                    options.Add(new InteractionOption($"Try to open {doorName}", () =>
                    {
                        dl.playerDoorOpen();
                        AudioCueGenerator.PlayNavSelect();
                        bool unlocked = dl.isUnlocked || dl.IsDoorUnlocked;
                        PrismSpeechProvider.Speak(unlocked ? $"{doorName} unlocked." : $"{doorName} is locked.", true);
                    }));
                }
            }

            // 6. Check Intro Event Doors
            var introDoors = UnityEngine.Object.FindObjectsOfType<IntroEventDoor>();
            foreach (var id in introDoors)
            {
                if (id != null && id.gameObject.activeInHierarchy &&
                    Vector3.Distance(playerPos, id.transform.position) <= interactRadius + 0.8f &&
                    seenObjects.Add(id.gameObject.GetInstanceID()))
                {
                    options.Add(new InteractionOption("Open Entrance Gate", () =>
                    {
                        id.playerDoorOpen();
                        AudioCueGenerator.PlayNavSelect();
                        PrismSpeechProvider.Speak("Entrance gate toggled.", true);
                    }));
                }
            }

            // 7. Check Almirahs
            var almirahs = UnityEngine.Object.FindObjectsOfType<OpenableAlmirah>();
            foreach (var alm in almirahs)
            {
                if (alm != null && alm.gameObject.activeInHierarchy &&
                    Vector3.Distance(playerPos, alm.transform.position) <= interactRadius &&
                    seenObjects.Add(alm.gameObject.GetInstanceID()))
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

            // 8. Check Chests
            var chests = UnityEngine.Object.FindObjectsOfType<OpenableChest>();
            foreach (var c in chests)
            {
                if (c != null && c.gameObject.activeInHierarchy &&
                    Vector3.Distance(playerPos, c.transform.position) <= interactRadius &&
                    seenObjects.Add(c.gameObject.GetInstanceID()))
                {
                    string state = c.isOpen ? "Close" : "Open";
                    options.Add(new InteractionOption($"{state} Chest", () =>
                    {
                        c.isOpen = !c.isOpen;
                        if (c.anim != null) c.anim.SetTrigger(c.isOpen ? "Open" : "Close");
                        AudioCueGenerator.PlayNavSelect();
                        PrismSpeechProvider.Speak($"Chest {(c.isOpen ? "opened" : "closed")}.", true);
                    }));
                }
            }

            // 9. Check Table Drawers
            var drawers = UnityEngine.Object.FindObjectsOfType<TableDrawer>();
            foreach (var dr in drawers)
            {
                if (dr != null && dr.gameObject.activeInHierarchy &&
                    Vector3.Distance(playerPos, dr.transform.position) <= interactRadius &&
                    seenObjects.Add(dr.gameObject.GetInstanceID()))
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
                options[0].Action?.Invoke();
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
