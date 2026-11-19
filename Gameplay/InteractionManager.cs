using System;
using System.Collections.Generic;
using UnityEngine;
using Il2Cpp;
using KamlaAccessibility.Core;
using KamlaAccessibility.Speech;
using KamlaAccessibility.Audio;

namespace KamlaAccessibility.Gameplay
{
    public class InteractionOption
    {
        public string Title { get; set; }
        public Action Action { get; set; }
        public GameObject Target { get; }

        public InteractionOption(string title, Action action, GameObject target)
        {
            Title = title;
            Action = action;
            Target = target;
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
        private int _selectedOption;

        public bool IsMenuActive => _isMenuActive;

        internal void Update(PlayerMovement player, PuzzleAccessibility puzzles, GameObject focusedObject, GameObject arrivedTarget)
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
                TriggerInteraction(player, puzzles, arrivedTarget != null ? arrivedTarget : focusedObject);
            }
        }

        private void TriggerInteraction(PlayerMovement player, PuzzleAccessibility puzzles, GameObject focusedObject)
        {
            if (!player.CanInteract)
            {
                PrismSpeechProvider.Speak("You cannot interact right now.", true);
                return;
            }
            Vector3 playerPos = player.transform.position;
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
                        bool pickedUp = TryPickUp(player, item);
                        AudioCueGenerator.PlayNavSelect();
                        PrismSpeechProvider.Speak(pickedUp ? $"Picked up {itemName}." :
                            $"Could not pick up {itemName}. Move closer or check what you are carrying.", true);
                    }, item.gameObject));
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
                        if (PuzzleAccessibility.CanReach(player, k.gameObject, 2.75f))
                            PuzzleAccessibility.WithInteract(k.keyCollect, k);
                        if (k.HasCollectedKey) AudioCueGenerator.PlayNavSelect();
                        PrismSpeechProvider.Speak(k.HasCollectedKey ? $"Collected {keyName}." :
                            $"Could not collect {keyName}. Move closer and try again.", true);
                    }, k.gameObject));
                }
            }

            // 2c. Check Notes
            var notes = UnityEngine.Object.FindObjectsOfType<Note>();
            foreach (var n in notes)
            {
                if (n != null && n.gameObject.activeInHierarchy &&
                    Vector3.Distance(playerPos, n.transform.position) <= interactRadius &&
                    seenObjects.Add(n.gameObject.GetInstanceID()))
                {
                    options.Add(new InteractionOption($"Read {HotspotManager.CleanNoteName(n)}", () =>
                    {
                        string noteText = !string.IsNullOrEmpty(n.noteString) ? n.noteString : "Blank Note";
                        n.isReadOnce = true;
                        AudioCueGenerator.PlayNavSelect();
                        PrismSpeechProvider.Speak($"Note: {noteText}", true);
                    }, n.gameObject));
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
                        if (NavigationHelper.TryToggleDoor(dsc)) AudioCueGenerator.PlayNavSelect();
                        PrismSpeechProvider.Speak(dsc.open ? $"{doorName} is open." : $"{doorName} is closed.", true);
                    }, dsc.gameObject));
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
                        if (NavigationHelper.TryToggleDoor(door)) AudioCueGenerator.PlayNavSelect();
                        PrismSpeechProvider.Speak(door.isOpen ? $"{doorName} is open." : $"{doorName} is closed.", true);
                    }, door.gameObject));
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
                        PuzzleAccessibility.WithInteract(dl.playerDoorOpen);
                        AudioCueGenerator.PlayNavSelect();
                        bool unlocked = dl.isUnlocked || dl.IsDoorUnlocked;
                        PrismSpeechProvider.Speak(unlocked ? $"{doorName} unlocked." : $"{doorName} is locked.", true);
                    }, dl.gameObject));
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
                    string doorName = HotspotManager.CleanDoorName(id.gameObject);
                    options.Add(new InteractionOption($"Try to open {doorName}", () =>
                    {
                        PuzzleAccessibility.WithInteract(id.playerDoorOpen);
                        AudioCueGenerator.PlayNavSelect();
                        PrismSpeechProvider.Speak(id.isOpened ? $"{doorName} is open." : $"{doorName} is closed.", true);
                    }, id.gameObject));
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
                    }, alm.gameObject));
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
                    }, c.gameObject));
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
                    }, dr.gameObject));
                }
            }

            puzzles.AddOptions(player, options);
            options.RemoveAll(option => !PuzzleAccessibility.CanReach(player, option.Target, 3.8f));
            if (focusedObject != null)
            {
                if (AccessibilityCoordinator.Instance?.Hotspots.ArrivedTarget == focusedObject)
                {
                    var selectedAction = options.Find(option => option.Target == focusedObject);
                    if (selectedAction != null) selectedAction.Action.Invoke();
                    else PrismSpeechProvider.Speak("The selected object has no available action from here.", true);
                    return;
                }
                int focusedIndex = options.FindIndex(option => option.Target == focusedObject ||
                    focusedObject.transform.IsChildOf(option.Target.transform) ||
                    option.Target.transform.IsChildOf(focusedObject.transform));
                if (focusedIndex > 0)
                {
                    var focused = options[focusedIndex];
                    options.RemoveAt(focusedIndex);
                    options.Insert(0, focused);
                }
            }

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
            _selectedOption = 0;

            AudioCueGenerator.PlayNavSelect();
            var sb = new System.Text.StringBuilder();
            sb.Append("Multiple options. Up and Down browse all actions, Enter selects. ");
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

            int direction = Input.GetKeyDown(KeyCode.UpArrow) ? -1 : Input.GetKeyDown(KeyCode.DownArrow) ? 1 : 0;
            if (direction != 0)
            {
                _selectedOption = (_selectedOption + direction + _activeOptions.Count) % _activeOptions.Count;
                PrismSpeechProvider.Speak($"{_activeOptions[_selectedOption].Title}, {_selectedOption + 1} of {_activeOptions.Count}.", true);
                return;
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ExecuteOption(_selectedOption);
                return;
            }

            // Keys 1 to 9
            for (int i = 0; i < Math.Min(_activeOptions.Count, 9); i++)
            {
                KeyCode numKey = KeyCode.Alpha1 + i;
                KeyCode padKey = KeyCode.Keypad1 + i;

                if (Input.GetKeyDown(numKey) || Input.GetKeyDown(padKey))
                {
                    ExecuteOption(i);
                    return;
                }
            }
        }

        internal static bool TryPickUp(PlayerMovement player, ItemPicker item)
        {
            if (player == null || !player.CanInteract || item == null ||
                !PuzzleAccessibility.CanReach(player, item.gameObject, 3f)) return false;

            // The automatic pickup helper touches the courtyard objective panel even in the Outer Area.
            // The normal interaction takes the game's separate path for objects such as the pump handle.
            var collider = item.coll;
            bool wasEnabled = collider != null && collider.enabled;
            PuzzleAccessibility.WithInteract(item.Update, item);
            return item == null || !item.gameObject.activeInHierarchy || item.transform.IsChildOf(player.transform) ||
                (wasEnabled && collider != null && !collider.enabled);
        }

        private void ExecuteOption(int index)
        {
            var option = _activeOptions[index];
            CloseMenu();
            if (option.Target != null && option.Target.activeInHierarchy) option.Action.Invoke();
            else PrismSpeechProvider.Speak("That action is no longer available.", true);
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
