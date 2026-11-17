using System;
using System.Collections.Generic;
using UnityEngine;
using Il2Cpp;
using KamlaAccessibility.Speech;
using KamlaAccessibility.Audio;

namespace KamlaAccessibility.Gameplay
{
    public enum HotspotCategory
    {
        Doors = 0,
        Items = 1,
        Containers = 2,
        RitualAndPuzzles = 3,
        HidingSpots = 4,
        Kamla = 5,
        AllObjects = 6
    }

    public class HotspotItem
    {
        public GameObject GameObject { get; set; }
        public string CleanName { get; set; }
        public HotspotCategory Category { get; set; }
        public Vector3 Position => GameObject != null ? GameObject.transform.position : Vector3.zero;

        public HotspotItem(GameObject go, string cleanName, HotspotCategory cat)
        {
            GameObject = go;
            CleanName = cleanName;
            Category = cat;
        }

        public string GetStateString()
        {
            if (GameObject == null) return "Gone";

            var door = GameObject.GetComponent<Door>();
            if (door != null)
            {
                return door.isOpen ? "Open" : "Closed";
            }

            var lockedDoor = GameObject.GetComponent<DoorLocked>();
            if (lockedDoor != null)
            {
                return "Locked";
            }

            var almirah = GameObject.GetComponent<OpenableAlmirah>();
            if (almirah != null)
            {
                return almirah.isOpen ? "Open" : "Closed";
            }

            var item = GameObject.GetComponent<ItemPicker>();
            if (item != null)
            {
                return "Ready to pick up";
            }

            var enemy = GameObject.GetComponent<Enemy>();
            if (enemy != null)
            {
                if (enemy.IsChasing) return "Chasing!";
                if (enemy.IsInvestigating) return "Investigating nearby";
                return "Roaming";
            }

            return "Interactive";
        }
    }

    /// <summary>
    /// Hotspot browsing and auto-walk navigation system using Home/End, PageUp/PageDown, and Delete.
    /// </summary>
    public class HotspotManager
    {
        private static readonly HotspotCategory[] Categories = (HotspotCategory[])Enum.GetValues(typeof(HotspotCategory));
        private int _currentCategoryIndex = 0;
        private List<HotspotItem> _currentItems = new List<HotspotItem>();
        private int _currentItemIndex = 0;

        // Auto-walk state
        private bool _isAutoWalking = false;
        private HotspotItem _targetItem = null;
        private float _footstepTimer = 0f;

        public bool IsAutoWalking => _isAutoWalking;
        public HotspotItem CurrentTarget => _targetItem;

        public void HandleInput(Transform playerTransform, CharacterController controller)
        {
            // Category navigation: Home (Previous), End (Next)
            if (Input.GetKeyDown(KeyCode.Home))
            {
                CycleCategory(playerTransform, -1);
            }
            else if (Input.GetKeyDown(KeyCode.End))
            {
                CycleCategory(playerTransform, 1);
            }

            // Hotspot navigation within category: Page Up (Previous), Page Down (Next)
            if (Input.GetKeyDown(KeyCode.PageUp))
            {
                CycleItem(playerTransform, -1);
            }
            else if (Input.GetKeyDown(KeyCode.PageDown))
            {
                CycleItem(playerTransform, 1);
            }

            // Auto-walk trigger: Delete key
            if (Input.GetKeyDown(KeyCode.Delete))
            {
                if (_isAutoWalking)
                {
                    StopAutoWalk("Auto-walk cancelled.");
                }
                else
                {
                    StartAutoWalk(playerTransform);
                }
            }

            // Update active auto-walk
            if (_isAutoWalking)
            {
                UpdateAutoWalk(playerTransform, controller);
            }
        }

        private void CycleCategory(Transform playerTransform, int direction)
        {
            _currentCategoryIndex = (_currentCategoryIndex + direction + Categories.Length) % Categories.Length;
            HotspotCategory selectedCat = Categories[_currentCategoryIndex];

            ScanCategory(playerTransform, selectedCat);

            AudioCueGenerator.PlayNavSelect();
            string catName = FormatCategoryName(selectedCat);
            if (_currentItems.Count == 0)
            {
                PrismSpeechProvider.Speak($"{catName} category. No objects found nearby.", true);
            }
            else
            {
                PrismSpeechProvider.Speak($"{catName} category. {_currentItems.Count} objects. Press Page Up or Page Down to browse.", true);
            }
        }

        private void CycleItem(Transform playerTransform, int direction)
        {
            if (_currentItems.Count == 0)
            {
                HotspotCategory selectedCat = Categories[_currentCategoryIndex];
                ScanCategory(playerTransform, selectedCat);
                if (_currentItems.Count == 0)
                {
                    AudioCueGenerator.PlayBoundaryHit();
                    PrismSpeechProvider.Speak($"No objects found in {FormatCategoryName(selectedCat)} category.", true);
                    return;
                }
            }

            _currentItemIndex = (_currentItemIndex + direction + _currentItems.Count) % _currentItems.Count;
            HotspotItem item = _currentItems[_currentItemIndex];

            if (item.GameObject == null)
            {
                _currentItems.RemoveAt(_currentItemIndex);
                CycleItem(playerTransform, 0);
                return;
            }

            Vector3 playerPos = playerTransform != null ? playerTransform.position : Vector3.zero;
            Vector3 forward = playerTransform != null ? playerTransform.forward : Vector3.forward;
            float dist = Vector3.Distance(playerPos, item.Position);
            int clock = GetClockDirection(forward, item.Position - playerPos);
            string state = item.GetStateString();

            AudioCueGenerator.PlayNavTick();
            PrismSpeechProvider.Speak($"{item.CleanName}, {dist:F1} meters at {clock} o'clock, {state}.", true);
        }

        private void StartAutoWalk(Transform playerTransform)
        {
            if (_currentItems.Count == 0 || _currentItemIndex >= _currentItems.Count)
            {
                PrismSpeechProvider.Speak("No object selected. Press Page Up or Page Down to select an object first.", true);
                return;
            }

            _targetItem = _currentItems[_currentItemIndex];
            if (_targetItem.GameObject == null)
            {
                PrismSpeechProvider.Speak("Selected object is no longer available.", true);
                return;
            }

            _isAutoWalking = true;
            _footstepTimer = 0f;
            AudioCueGenerator.PlayNavSelect();
            PrismSpeechProvider.Speak($"Walking to {_targetItem.CleanName}...", true);
        }

        private void UpdateAutoWalk(Transform playerTransform, CharacterController controller)
        {
            // Cancel if player touches manual controls
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) ||
                Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D) ||
                Input.GetKeyDown(KeyCode.Space))
            {
                StopAutoWalk("Auto-walk cancelled.");
                return;
            }

            if (_targetItem == null || _targetItem.GameObject == null || playerTransform == null)
            {
                StopAutoWalk("Target lost.");
                return;
            }

            Vector3 targetPos = _targetItem.Position;
            Vector3 playerPos = playerTransform.position;

            Vector3 toTarget = targetPos - playerPos;
            toTarget.y = 0; // Move horizontally
            float dist = toTarget.magnitude;

            // Stop when within arrival distance (~1.3 meters)
            if (dist <= 1.3f)
            {
                StopAutoWalk($"Arrived at {_targetItem.CleanName}. Press Enter to interact.");
                return;
            }

            // Rotate smoothly towards target
            if (toTarget.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toTarget);
                playerTransform.rotation = Quaternion.Slerp(playerTransform.rotation, targetRot, Time.deltaTime * 6f);
            }

            // Move forward
            Vector3 moveDir = toTarget.normalized * 3.5f * Time.deltaTime;
            moveDir.y = -9.8f * Time.deltaTime; // Gravity

            if (controller != null && controller.enabled)
            {
                controller.Move(moveDir);
            }
            else
            {
                playerTransform.position += new Vector3(moveDir.x, 0, moveDir.z);
            }

            // Procedural footstep audio tick
            _footstepTimer += Time.deltaTime;
            if (_footstepTimer >= 0.45f)
            {
                _footstepTimer = 0f;
                AudioCueGenerator.PlayNavTick();
            }
        }

        public void StopAutoWalk(string message = null)
        {
            _isAutoWalking = false;
            _targetItem = null;
            if (!string.IsNullOrEmpty(message))
            {
                AudioCueGenerator.PlayNavSelect();
                PrismSpeechProvider.Speak(message, true);
            }
        }

        public void ScanCategory(Transform playerTransform, HotspotCategory category)
        {
            _currentItems.Clear();
            _currentItemIndex = 0;
            Vector3 playerPos = playerTransform != null ? playerTransform.position : Vector3.zero;

            switch (category)
            {
                case HotspotCategory.Doors:
                    var doors = UnityEngine.Object.FindObjectsOfType<Door>();
                    foreach (var d in doors)
                        if (d != null && d.gameObject.activeInHierarchy)
                            _currentItems.Add(new HotspotItem(d.gameObject, CleanDoorName(d.gameObject.name), HotspotCategory.Doors));

                    var lockedDoors = UnityEngine.Object.FindObjectsOfType<DoorLocked>();
                    foreach (var ld in lockedDoors)
                        if (ld != null && ld.gameObject.activeInHierarchy)
                            _currentItems.Add(new HotspotItem(ld.gameObject, "Locked Haveli Door", HotspotCategory.Doors));
                    break;

                case HotspotCategory.Items:
                    var items = UnityEngine.Object.FindObjectsOfType<ItemPicker>();
                    foreach (var it in items)
                        if (it != null && it.gameObject.activeInHierarchy)
                            _currentItems.Add(new HotspotItem(it.gameObject, CleanItemName(it), HotspotCategory.Items));
                    break;

                case HotspotCategory.Containers:
                    var almirahs = UnityEngine.Object.FindObjectsOfType<OpenableAlmirah>();
                    foreach (var a in almirahs)
                        if (a != null && a.gameObject.activeInHierarchy)
                            _currentItems.Add(new HotspotItem(a.gameObject, "Haveli Almirah", HotspotCategory.Containers));

                    var drawers = UnityEngine.Object.FindObjectsOfType<TableDrawer>();
                    foreach (var dr in drawers)
                        if (dr != null && dr.gameObject.activeInHierarchy)
                            _currentItems.Add(new HotspotItem(dr.gameObject, "Table Drawer", HotspotCategory.Containers));
                    break;

                case HotspotCategory.RitualAndPuzzles:
                    var havan = GameObject.Find("PlaceCircleCourtyard") ?? GameObject.Find("HavanParticleSystem") ?? GameObject.Find("CourtyardMechanic");
                    if (havan != null) _currentItems.Add(new HotspotItem(havan, "Courtyard Havan Altar", HotspotCategory.RitualAndPuzzles));

                    var ganesh = GameObject.Find("GaneshMurtiManager") ?? GameObject.Find("GaneshMurti");
                    if (ganesh != null) _currentItems.Add(new HotspotItem(ganesh, "Ganesh Idol", HotspotCategory.RitualAndPuzzles));

                    var bells = UnityEngine.Object.FindObjectsOfType<HangingBell>();
                    foreach (var b in bells)
                        if (b != null && b.gameObject.activeInHierarchy)
                            _currentItems.Add(new HotspotItem(b.gameObject, "Temple Bell", HotspotCategory.RitualAndPuzzles));

                    var bricks = UnityEngine.Object.FindObjectsOfType<PushableBrick>();
                    foreach (var br in bricks)
                        if (br != null && br.gameObject.activeInHierarchy)
                            _currentItems.Add(new HotspotItem(br.gameObject, "Puzzle Brick", HotspotCategory.RitualAndPuzzles));
                    break;

                case HotspotCategory.HidingSpots:
                    var hides = UnityEngine.Object.FindObjectsOfType<HideDetectionCollider>();
                    foreach (var h in hides)
                        if (h != null && h.gameObject.activeInHierarchy)
                            _currentItems.Add(new HotspotItem(h.gameObject, "Hiding Spot", HotspotCategory.HidingSpots));

                    var almHides = UnityEngine.Object.FindObjectsOfType<OpenableAlmirah>();
                    foreach (var al in almHides)
                        if (al != null && al.gameObject.activeInHierarchy)
                            _currentItems.Add(new HotspotItem(al.gameObject, "Wardrobe Hiding Spot", HotspotCategory.HidingSpots));
                    break;

                case HotspotCategory.Kamla:
                    var enemy = UnityEngine.Object.FindObjectOfType<Enemy>();
                    if (enemy != null && enemy.gameObject.activeInHierarchy)
                        _currentItems.Add(new HotspotItem(enemy.gameObject, "Kamla", HotspotCategory.Kamla));
                    break;

                case HotspotCategory.AllObjects:
                    ScanCategory(playerTransform, HotspotCategory.Doors);
                    var dList = new List<HotspotItem>(_currentItems);
                    ScanCategory(playerTransform, HotspotCategory.Items);
                    dList.AddRange(_currentItems);
                    ScanCategory(playerTransform, HotspotCategory.Containers);
                    dList.AddRange(_currentItems);
                    ScanCategory(playerTransform, HotspotCategory.RitualAndPuzzles);
                    dList.AddRange(_currentItems);
                    ScanCategory(playerTransform, HotspotCategory.HidingSpots);
                    dList.AddRange(_currentItems);
                    ScanCategory(playerTransform, HotspotCategory.Kamla);
                    dList.AddRange(_currentItems);
                    _currentItems = dList;
                    break;
            }

            // Sort all by distance from player
            _currentItems.Sort((a, b) =>
                Vector3.Distance(playerPos, a.Position).CompareTo(Vector3.Distance(playerPos, b.Position)));
        }

        private static string FormatCategoryName(HotspotCategory cat)
        {
            switch (cat)
            {
                case HotspotCategory.Doors: return "Doors";
                case HotspotCategory.Items: return "Items and Pickups";
                case HotspotCategory.Containers: return "Containers and Drawers";
                case HotspotCategory.RitualAndPuzzles: return "Ritual and Puzzles";
                case HotspotCategory.HidingSpots: return "Hiding Spots";
                case HotspotCategory.Kamla: return "Kamla and Threats";
                case HotspotCategory.AllObjects: return "All Objects";
                default: return cat.ToString();
            }
        }

        private static string CleanItemName(ItemPicker it)
        {
            if (it == null) return "Item";
            if (!string.IsNullOrEmpty(it.textString)) return it.textString.Trim();
            if (!string.IsNullOrEmpty(it.headhunted)) return it.headhunted.Trim();
            return it.gameObject.name.Replace("(Clone)", "").Replace("_", " ").Trim();
        }

        private static string CleanDoorName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "Door";
            raw = raw.Replace("(Clone)", "").Replace("_", " ").Trim();
            if (raw.IndexOf("Creepy", StringComparison.OrdinalIgnoreCase) >= 0) return "Creepy Door";
            if (raw.IndexOf("Metal", StringComparison.OrdinalIgnoreCase) >= 0) return "Metal Door";
            if (raw.IndexOf("Main", StringComparison.OrdinalIgnoreCase) >= 0) return "Main Haveli Gate";
            return "Haveli Door";
        }

        private static int GetClockDirection(Vector3 forward, Vector3 toTarget)
        {
            toTarget.y = 0;
            if (toTarget.sqrMagnitude < 0.001f) return 12;
            toTarget.Normalize();

            float angle = Vector3.SignedAngle(forward, toTarget, Vector3.up);
            if (angle < 0) angle += 360f;

            int hour = Mathf.RoundToInt(angle / 30f) % 12;
            return hour == 0 ? 12 : hour;
        }
    }
}
