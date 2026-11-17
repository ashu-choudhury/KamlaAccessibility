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

            // 1. Doors
            var dsc = GameObject.GetComponent<DoorScriptCode>();
            if (dsc != null)
            {
                return dsc.open ? "Open" : "Closed";
            }

            var door = GameObject.GetComponent<Door>();
            if (door != null)
            {
                return door.isOpen ? "Open" : "Closed";
            }

            var lockedDoor = GameObject.GetComponent<DoorLocked>();
            if (lockedDoor != null)
            {
                bool unlocked = lockedDoor.isUnlocked || lockedDoor.IsDoorUnlocked;
                return unlocked ? "Unlocked" : "Locked";
            }

            var metalDoor = GameObject.GetComponent<MetalDoor>();
            if (metalDoor != null)
            {
                return metalDoor.IsUnlocked ? "Unlocked" : "Locked";
            }

            var metalLocked = GameObject.GetComponent<MetalLockedDoor>();
            if (metalLocked != null)
            {
                return "Locked Metal Door";
            }

            var introDoor = GameObject.GetComponent<IntroEventDoor>();
            if (introDoor != null)
            {
                return introDoor.isOpened ? "Open" : "Closed";
            }

            // 2. Containers
            var almirah = GameObject.GetComponent<OpenableAlmirah>();
            if (almirah != null)
            {
                return almirah.isOpen ? "Open" : "Closed";
            }

            var chest = GameObject.GetComponent<OpenableChest>();
            if (chest != null)
            {
                return chest.isOpen ? "Open" : "Closed";
            }

            // 3. Items and Keys
            var key = GameObject.GetComponent<Key>();
            if (key != null)
            {
                return key.HasCollectedKey ? "Collected" : "Ready to pick up";
            }

            var item = GameObject.GetComponent<ItemPicker>();
            if (item != null)
            {
                return "Ready to pick up";
            }

            var courtItem = GameObject.GetComponent<ItemInCourtyard>();
            if (courtItem != null)
            {
                return courtItem.isItemPlaced ? "Placed on Altar" : "Ready for Ritual";
            }

            // 4. Enemy
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

            // Footstep timer
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
            var seenObjects = new HashSet<int>();

            switch (category)
            {
                case HotspotCategory.Doors:
                    // 1. DoorScriptCode (Primary Haveli doors)
                    var scriptDoors = UnityEngine.Object.FindObjectsOfType<DoorScriptCode>();
                    foreach (var d in scriptDoors)
                    {
                        if (d != null && d.gameObject.activeInHierarchy && seenObjects.Add(d.gameObject.GetInstanceID()))
                            _currentItems.Add(new HotspotItem(d.gameObject, CleanDoorName(d.gameObject), HotspotCategory.Doors));
                    }

                    // 2. Door
                    var doors = UnityEngine.Object.FindObjectsOfType<Door>();
                    foreach (var d in doors)
                    {
                        if (d != null && d.gameObject.activeInHierarchy && seenObjects.Add(d.gameObject.GetInstanceID()))
                            _currentItems.Add(new HotspotItem(d.gameObject, CleanDoorName(d.gameObject), HotspotCategory.Doors));
                    }

                    // 3. DoorLocked
                    var lockedDoors = UnityEngine.Object.FindObjectsOfType<DoorLocked>();
                    foreach (var ld in lockedDoors)
                    {
                        if (ld != null && ld.gameObject.activeInHierarchy && seenObjects.Add(ld.gameObject.GetInstanceID()))
                            _currentItems.Add(new HotspotItem(ld.gameObject, "Locked " + CleanDoorName(ld.gameObject), HotspotCategory.Doors));
                    }

                    // 4. MetalDoor
                    var metalDoors = UnityEngine.Object.FindObjectsOfType<MetalDoor>();
                    foreach (var md in metalDoors)
                    {
                        if (md != null && md.gameObject.activeInHierarchy && seenObjects.Add(md.gameObject.GetInstanceID()))
                            _currentItems.Add(new HotspotItem(md.gameObject, "Metal Door", HotspotCategory.Doors));
                    }

                    // 5. MetalLockedDoor
                    var metalLocked = UnityEngine.Object.FindObjectsOfType<MetalLockedDoor>();
                    foreach (var mld in metalLocked)
                    {
                        if (mld != null && mld.gameObject.activeInHierarchy && seenObjects.Add(mld.gameObject.GetInstanceID()))
                            _currentItems.Add(new HotspotItem(mld.gameObject, "Locked Metal Door", HotspotCategory.Doors));
                    }

                    // 6. OuterAreaDoor
                    var outerDoors = UnityEngine.Object.FindObjectsOfType<OuterAreaDoor>();
                    foreach (var od in outerDoors)
                    {
                        if (od != null && od.gameObject.activeInHierarchy && seenObjects.Add(od.gameObject.GetInstanceID()))
                            _currentItems.Add(new HotspotItem(od.gameObject, "Outer Area Gate", HotspotCategory.Doors));
                    }

                    // 7. IntroEventDoor
                    var introDoors = UnityEngine.Object.FindObjectsOfType<IntroEventDoor>();
                    foreach (var id in introDoors)
                    {
                        if (id != null && id.gameObject.activeInHierarchy && seenObjects.Add(id.gameObject.GetInstanceID()))
                            _currentItems.Add(new HotspotItem(id.gameObject, "Entrance Gate", HotspotCategory.Doors));
                    }
                    break;

                case HotspotCategory.Items:
                    // 1. ItemPicker (Ritual items, tools, potions, matches, etc.)
                    var items = UnityEngine.Object.FindObjectsOfType<ItemPicker>();
                    foreach (var it in items)
                    {
                        if (it != null && it.gameObject.activeInHierarchy && seenObjects.Add(it.gameObject.GetInstanceID()))
                            _currentItems.Add(new HotspotItem(it.gameObject, CleanItemName(it), HotspotCategory.Items));
                    }

                    // 2. Key (Haveli keys)
                    var keys = UnityEngine.Object.FindObjectsOfType<Key>();
                    foreach (var k in keys)
                    {
                        if (k != null && k.gameObject.activeInHierarchy && !k.HasCollectedKey && seenObjects.Add(k.gameObject.GetInstanceID()))
                            _currentItems.Add(new HotspotItem(k.gameObject, CleanKeyName(k), HotspotCategory.Items));
                    }

                    // 3. ItemInCourtyard
                    var courtItems = UnityEngine.Object.FindObjectsOfType<ItemInCourtyard>();
                    foreach (var ci in courtItems)
                    {
                        if (ci != null && ci.gameObject.activeInHierarchy && seenObjects.Add(ci.gameObject.GetInstanceID()))
                            _currentItems.Add(new HotspotItem(ci.gameObject, FormatRecipeItem(ci.MyItem), HotspotCategory.Items));
                    }
                    break;

                case HotspotCategory.Containers:
                    var almirahs = UnityEngine.Object.FindObjectsOfType<OpenableAlmirah>();
                    foreach (var a in almirahs)
                    {
                        if (a != null && a.gameObject.activeInHierarchy && seenObjects.Add(a.gameObject.GetInstanceID()))
                            _currentItems.Add(new HotspotItem(a.gameObject, "Haveli Almirah", HotspotCategory.Containers));
                    }

                    var chests = UnityEngine.Object.FindObjectsOfType<OpenableChest>();
                    foreach (var c in chests)
                    {
                        if (c != null && c.gameObject.activeInHierarchy && seenObjects.Add(c.gameObject.GetInstanceID()))
                            _currentItems.Add(new HotspotItem(c.gameObject, "Haveli Chest", HotspotCategory.Containers));
                    }

                    var drawers = UnityEngine.Object.FindObjectsOfType<TableDrawer>();
                    foreach (var dr in drawers)
                    {
                        if (dr != null && dr.gameObject.activeInHierarchy && seenObjects.Add(dr.gameObject.GetInstanceID()))
                            _currentItems.Add(new HotspotItem(dr.gameObject, "Table Drawer", HotspotCategory.Containers));
                    }

                    var suitcases = UnityEngine.Object.FindObjectsOfType<Suitcase>();
                    foreach (var sc in suitcases)
                    {
                        if (sc != null && sc.gameObject.activeInHierarchy && seenObjects.Add(sc.gameObject.GetInstanceID()))
                            _currentItems.Add(new HotspotItem(sc.gameObject, "Combination Suitcase", HotspotCategory.Containers));
                    }
                    break;

                case HotspotCategory.RitualAndPuzzles:
                    var havan = GameObject.Find("PlaceCircleCourtyard") ?? GameObject.Find("HavanParticleSystem") ?? GameObject.Find("CourtyardMechanic");
                    if (havan != null && seenObjects.Add(havan.GetInstanceID()))
                        _currentItems.Add(new HotspotItem(havan, "Courtyard Havan Altar", HotspotCategory.RitualAndPuzzles));

                    var ganesh = GameObject.Find("GaneshMurtiManager") ?? GameObject.Find("GaneshMurti");
                    if (ganesh != null && seenObjects.Add(ganesh.GetInstanceID()))
                        _currentItems.Add(new HotspotItem(ganesh, "Ganesh Idol", HotspotCategory.RitualAndPuzzles));

                    var bells = UnityEngine.Object.FindObjectsOfType<HangingBell>();
                    foreach (var b in bells)
                    {
                        if (b != null && b.gameObject.activeInHierarchy && seenObjects.Add(b.gameObject.GetInstanceID()))
                            _currentItems.Add(new HotspotItem(b.gameObject, "Temple Bell", HotspotCategory.RitualAndPuzzles));
                    }

                    var bricks = UnityEngine.Object.FindObjectsOfType<PushableBrick>();
                    foreach (var br in bricks)
                    {
                        if (br != null && br.gameObject.activeInHierarchy && seenObjects.Add(br.gameObject.GetInstanceID()))
                            _currentItems.Add(new HotspotItem(br.gameObject, "Puzzle Brick", HotspotCategory.RitualAndPuzzles));
                    }
                    break;

                case HotspotCategory.HidingSpots:
                    var hides = UnityEngine.Object.FindObjectsOfType<HideDetectionCollider>();
                    foreach (var h in hides)
                    {
                        if (h != null && h.gameObject.activeInHierarchy && seenObjects.Add(h.gameObject.GetInstanceID()))
                            _currentItems.Add(new HotspotItem(h.gameObject, "Hiding Spot", HotspotCategory.HidingSpots));
                    }

                    var almHides = UnityEngine.Object.FindObjectsOfType<OpenableAlmirah>();
                    foreach (var al in almHides)
                    {
                        if (al != null && al.gameObject.activeInHierarchy && seenObjects.Add(al.gameObject.GetInstanceID()))
                            _currentItems.Add(new HotspotItem(al.gameObject, "Wardrobe Hiding Spot", HotspotCategory.HidingSpots));
                    }
                    break;

                case HotspotCategory.Kamla:
                    var enemy = UnityEngine.Object.FindObjectOfType<Enemy>();
                    if (enemy != null && enemy.gameObject.activeInHierarchy && seenObjects.Add(enemy.gameObject.GetInstanceID()))
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

        public static string CleanItemName(ItemPicker it)
        {
            if (it == null) return "Item";

            // 1. Check ItemData ScriptableObject
            try
            {
                if (it.myItemData != null)
                {
                    if (!string.IsNullOrEmpty(it.myItemData.ItemNameForDisplay))
                        return it.myItemData.ItemNameForDisplay.Trim();
                    if (!string.IsNullOrEmpty(it.myItemData.ItemName))
                        return it.myItemData.ItemName.Trim();
                }
            }
            catch { }

            // 2. Check RECIPE_ITEM enum
            try
            {
                if (it.MyItem != GlobalConstants.RECIPE_ITEM.None)
                {
                    return FormatRecipeItem(it.MyItem);
                }
            }
            catch { }

            // 3. Check headhunted string if it contains a valid name (not the warning text)
            if (!string.IsNullOrEmpty(it.headhunted) &&
                !it.headhunted.Contains("Holding", StringComparison.OrdinalIgnoreCase))
            {
                return it.headhunted.Trim();
            }

            // 4. Fallback to cleaned GameObject name (e.g. "Key_Room1 (Clone)" -> "Key Room 1")
            string raw = it.gameObject.name.Replace("(Clone)", "").Replace("_", " ").Trim();
            return string.IsNullOrEmpty(raw) ? "Haveli Item" : raw;
        }

        public static string CleanKeyName(Key k)
        {
            if (k == null) return "Key";
            if (!string.IsNullOrEmpty(k.keyName)) return k.keyName.Trim();
            string raw = k.gameObject.name.Replace("(Clone)", "").Replace("_", " ").Trim();
            return string.IsNullOrEmpty(raw) ? "Haveli Key" : raw;
        }

        public static string CleanDoorName(GameObject go)
        {
            if (go == null) return "Haveli Door";
            string raw = go.name.Replace("(Clone)", "").Replace("_", " ").Trim();
            if (raw.IndexOf("Creepy", StringComparison.OrdinalIgnoreCase) >= 0) return "Creepy Door";
            if (raw.IndexOf("Metal", StringComparison.OrdinalIgnoreCase) >= 0) return "Metal Door";
            if (raw.IndexOf("Main", StringComparison.OrdinalIgnoreCase) >= 0 || raw.IndexOf("Outer", StringComparison.OrdinalIgnoreCase) >= 0) return "Main Haveli Gate";
            if (raw.IndexOf("Mandir", StringComparison.OrdinalIgnoreCase) >= 0 || raw.IndexOf("Temple", StringComparison.OrdinalIgnoreCase) >= 0) return "Mandir Door";
            if (raw.IndexOf("Courtyard", StringComparison.OrdinalIgnoreCase) >= 0) return "Courtyard Door";
            if (raw.IndexOf("Double", StringComparison.OrdinalIgnoreCase) >= 0) return "Double Door";
            if (raw.IndexOf("Room", StringComparison.OrdinalIgnoreCase) >= 0) return raw;
            return string.IsNullOrEmpty(raw) ? "Haveli Door" : raw;
        }

        public static string FormatRecipeItem(GlobalConstants.RECIPE_ITEM item)
        {
            switch (item)
            {
                case GlobalConstants.RECIPE_ITEM.Camphor: return "Camphor (Kapoor)";
                case GlobalConstants.RECIPE_ITEM.SaltBowl: return "Salt Bowl";
                case GlobalConstants.RECIPE_ITEM.Thali: return "Puja Thali";
                case GlobalConstants.RECIPE_ITEM.Chalisa: return "Hanuman Chalisa";
                case GlobalConstants.RECIPE_ITEM.Gangajal: return "Holy Water (Gangajal)";
                case GlobalConstants.RECIPE_ITEM.KalashPot: return "Kalash Pot";
                case GlobalConstants.RECIPE_ITEM.Ganesh: return "Ganesh Idol";
                case GlobalConstants.RECIPE_ITEM.NazarBattu: return "Nazar Battu";
                case GlobalConstants.RECIPE_ITEM.Smudge: return "Smudge Stick (Dhoop)";
                case GlobalConstants.RECIPE_ITEM.Umbrella: return "Umbrella";
                default: return item.ToString();
            }
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
