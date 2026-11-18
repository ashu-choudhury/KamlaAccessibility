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

            // 3. Items, Keys, Notes, and Books
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

            var book = GameObject.GetComponent<Book>();
            if (book != null)
            {
                return book.HasInteractedWithBook ? "Already Read" : "Unread";
            }

            var note = GameObject.GetComponent<Note>();
            if (note != null)
            {
                return note.isReadOnce ? "Read" : "Unread";
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
    /// Simulates genuine game walking with spatial footstep audio from SoundManager and NavMesh path following.
    /// </summary>
    public class HotspotManager
    {
        private static readonly HotspotCategory[] Categories = (HotspotCategory[])Enum.GetValues(typeof(HotspotCategory));
        private int _currentCategoryIndex = 0;
        private List<HotspotItem> _currentItems = new List<HotspotItem>();
        private int _currentItemIndex = 0;
        private Vector3 _lastScanPlayerPos = Vector3.negativeInfinity;

        // Auto-walk state
        private bool _isAutoWalking = false;
        private HotspotItem _targetItem = null;
        private List<Vector3> _waypoints = new List<Vector3>();
        private int _currentWaypointIndex = 0;
        private Vector3 _lastRecordedPos = Vector3.zero;
        private float _stuckTimer = 0f;
        private float _footstepTimer = 0f;

        public bool IsAutoWalking => _isAutoWalking;
        public HotspotItem CurrentTarget => _targetItem;

        public void HandleInput(PlayerMovement player)
        {
            if (player == null)
            {
                player = UnityEngine.Object.FindObjectOfType<PlayerMovement>();
            }

            Transform playerTransform = player != null ? player.transform : (Camera.main != null ? Camera.main.transform : null);

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
                UpdateAutoWalk(player);
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
                PrismSpeechProvider.Speak($"{catName} category. No accessible objects found nearby.", true);
            }
            else
            {
                PrismSpeechProvider.Speak($"{catName} category. {_currentItems.Count} accessible objects. Press Page Up or Page Down to browse.", true);
            }
        }

        private void CycleItem(Transform playerTransform, int direction)
        {
            Vector3 playerPos = playerTransform != null ? playerTransform.position : Vector3.zero;

            // Re-scan if category is empty or if player has moved significantly (> 3.5m)
            if (_currentItems.Count == 0 || Vector3.Distance(playerPos, _lastScanPlayerPos) > 3.5f)
            {
                HotspotCategory selectedCat = Categories[_currentCategoryIndex];
                ScanCategory(playerTransform, selectedCat);
                if (_currentItems.Count == 0)
                {
                    AudioCueGenerator.PlayBoundaryHit();
                    PrismSpeechProvider.Speak($"No accessible objects found in {FormatCategoryName(selectedCat)} category.", true);
                    return;
                }
            }

            _currentItemIndex = (_currentItemIndex + direction + _currentItems.Count) % _currentItems.Count;
            HotspotItem item = _currentItems[_currentItemIndex];

            if (item.GameObject == null || !item.GameObject.activeInHierarchy)
            {
                _currentItems.RemoveAt(_currentItemIndex);
                if (_currentItems.Count == 0)
                {
                    AudioCueGenerator.PlayBoundaryHit();
                    PrismSpeechProvider.Speak("No more accessible objects in this category.", true);
                    return;
                }
                CycleItem(playerTransform, 0);
                return;
            }

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
                PrismSpeechProvider.Speak("No object selected. Press Page Up or Page Down to select an accessible object first.", true);
                return;
            }

            _targetItem = _currentItems[_currentItemIndex];
            if (_targetItem.GameObject == null || !_targetItem.GameObject.activeInHierarchy)
            {
                PrismSpeechProvider.Speak("Selected object is no longer available.", true);
                return;
            }

            Vector3 playerPos = playerTransform != null ? playerTransform.position : Vector3.zero;

            // Calculate walkable NavMesh path
            if (!NavigationHelper.IsReachable(playerPos, _targetItem.Position, out _waypoints) || _waypoints.Count == 0)
            {
                AudioCueGenerator.PlayBoundaryHit();
                PrismSpeechProvider.Speak($"Cannot find a walkable path to {_targetItem.CleanName}. Path is obstructed or locked.", true);
                return;
            }

            _isAutoWalking = true;
            _currentWaypointIndex = (_waypoints.Count > 1) ? 1 : 0;
            _lastRecordedPos = playerPos;
            _stuckTimer = 0f;
            _footstepTimer = 0f;

            AudioCueGenerator.PlayNavSelect();
            PrismSpeechProvider.Speak($"Walking to {_targetItem.CleanName}...", true);
        }

        private void UpdateAutoWalk(PlayerMovement player)
        {
            if (player == null)
            {
                StopAutoWalk("Player controller unavailable.");
                return;
            }

            Transform playerTransform = player.transform;
            CharacterController controller = player.Controller;

            // Cancel if player touches manual controls
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) ||
                Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D) ||
                Input.GetKeyDown(KeyCode.Space))
            {
                StopAutoWalk("Auto-walk cancelled.");
                return;
            }

            if (_targetItem == null || _targetItem.GameObject == null || !_targetItem.GameObject.activeInHierarchy || playerTransform == null)
            {
                StopAutoWalk("Target lost or no longer available.");
                return;
            }

            Vector3 targetPos = _targetItem.Position;
            Vector3 playerPos = playerTransform.position;

            // Check arrival to target (horizontal distance, accounts for items on tables or altars)
            Vector3 toFinalTarget = targetPos - playerPos;
            toFinalTarget.y = 0;
            float distToFinal = toFinalTarget.magnitude;

            if (distToFinal <= 1.4f)
            {
                StopAutoWalk($"Arrived at {_targetItem.CleanName}. Press Enter to interact.");
                return;
            }

            if (_waypoints == null || _waypoints.Count == 0)
            {
                StopAutoWalk("Path lost.");
                return;
            }

            // Current target waypoint along NavMesh path corners
            Vector3 targetWaypoint = _currentWaypointIndex < _waypoints.Count ? _waypoints[_currentWaypointIndex] : targetPos;
            Vector3 toWaypoint = targetWaypoint - playerPos;
            toWaypoint.y = 0;
            float distToWaypoint = toWaypoint.magnitude;

            // If arrived at current waypoint corner, advance to next
            if (distToWaypoint <= 0.65f)
            {
                if (_currentWaypointIndex < _waypoints.Count - 1)
                {
                    _currentWaypointIndex++;
                    targetWaypoint = _waypoints[_currentWaypointIndex];
                    toWaypoint = targetWaypoint - playerPos;
                    toWaypoint.y = 0;
                    distToWaypoint = toWaypoint.magnitude;
                }
            }

            // Automatically open unlocked closed doors near the player along the path
            NavigationHelper.CheckAndOpenDoorsNearPlayer(playerPos, 2.5f);

            // Rotate smoothly towards the next waypoint
            if (toWaypoint.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toWaypoint.normalized);
                playerTransform.rotation = Quaternion.Slerp(playerTransform.rotation, targetRot, Time.deltaTime * 12f);
            }

            // Normal walking speed from the player's own speed property
            float walkSpeed = player.speed > 1.5f ? player.speed : 4.5f;
            Vector3 forwardMove = toWaypoint.normalized * walkSpeed * Time.deltaTime;

            // Inform the game's movement system and input manager
            if (player.inputManager != null)
            {
                player.inputManager.MoveInput = new Vector2(0f, 1f);
            }
            player.IsMoving = true;

            // Move the controller smoothly
            if (controller != null && controller.enabled)
            {
                controller.Move(forwardMove);
            }
            else
            {
                playerTransform.position += new Vector3(forwardMove.x, 0, forwardMove.z);
            }

            // Genuine game footstep audio trigger
            _footstepTimer += Time.deltaTime;
            float stepCadence = 0.38f;
            if (_footstepTimer >= stepCadence)
            {
                _footstepTimer = 0f;
                if (player.soundMan != null)
                {
                    try
                    {
                        player.soundMan.WalkingPlayer();
                    }
                    catch { }
                }
            }

            // Stuck detection & dynamic repathing
            _stuckTimer += Time.deltaTime;
            if (_stuckTimer >= 1.2f)
            {
                _stuckTimer = 0f;
                float movedDist = Vector3.Distance(playerPos, _lastRecordedPos);
                _lastRecordedPos = playerPos;

                if (movedDist < 0.22f)
                {
                    // Player is stuck against something (door, corner, or obstacle)
                    NavigationHelper.CheckAndOpenDoorsNearPlayer(playerPos, 3.0f);

                    // Recalculate path from current player position
                    if (NavigationHelper.IsReachable(playerPos, targetPos, out var newWaypoints) && newWaypoints.Count > 0)
                    {
                        _waypoints = newWaypoints;
                        _currentWaypointIndex = (_waypoints.Count > 1) ? 1 : 0;
                    }
                    else
                    {
                        StopAutoWalk("Path is blocked by an obstacle.");
                        return;
                    }
                }
            }
        }

        public void StopAutoWalk(string message = null)
        {
            _isAutoWalking = false;
            _targetItem = null;
            _waypoints.Clear();
            _currentWaypointIndex = 0;
            _stuckTimer = 0f;

            var player = UnityEngine.Object.FindObjectOfType<PlayerMovement>();
            if (player != null)
            {
                player.IsMoving = false;
                if (player.inputManager != null)
                {
                    player.inputManager.MoveInput = Vector2.zero;
                }
                if (player.soundMan != null)
                {
                    try
                    {
                        player.soundMan.WalkingPlayerStop();
                    }
                    catch { }
                }
            }

            if (!string.IsNullOrEmpty(message))
            {
                AudioCueGenerator.PlayNavSelect();
                PrismSpeechProvider.Speak(message, true);
            }
        }

        private bool TryAddHotspot(GameObject go, string cleanName, HotspotCategory cat, Vector3 playerPos, HashSet<int> seenObjects)
        {
            if (go == null || !go.activeInHierarchy) return false;
            if (go.scene.name == null) return false;

            int id = go.GetInstanceID();
            if (!seenObjects.Add(id)) return false;

            Vector3 targetPos = go.transform.position;
            float dist = Vector3.Distance(playerPos, targetPos);
            if (dist > 50f) return false;

            // Only include physically reachable hotspots
            if (!NavigationHelper.IsReachable(playerPos, targetPos)) return false;

            _currentItems.Add(new HotspotItem(go, cleanName, cat));
            return true;
        }

        public void ScanCategory(Transform playerTransform, HotspotCategory category)
        {
            _currentItems.Clear();
            _currentItemIndex = 0;

            if (playerTransform == null)
            {
                var p = UnityEngine.Object.FindObjectOfType<PlayerMovement>();
                if (p != null) playerTransform = p.transform;
                else if (Camera.main != null) playerTransform = Camera.main.transform;
            }

            Vector3 playerPos = playerTransform != null ? playerTransform.position : Vector3.zero;
            _lastScanPlayerPos = playerPos;
            var seenObjects = new HashSet<int>();

            switch (category)
            {
                case HotspotCategory.Doors:
                    // 1. DoorScriptCode (Primary Haveli doors)
                    var scriptDoors = UnityEngine.Object.FindObjectsOfType<DoorScriptCode>(true);
                    foreach (var d in scriptDoors)
                    {
                        if (d != null)
                            TryAddHotspot(d.gameObject, CleanDoorName(d.gameObject), HotspotCategory.Doors, playerPos, seenObjects);
                    }

                    // 2. Door
                    var doors = UnityEngine.Object.FindObjectsOfType<Door>(true);
                    foreach (var d in doors)
                    {
                        if (d != null)
                            TryAddHotspot(d.gameObject, CleanDoorName(d.gameObject), HotspotCategory.Doors, playerPos, seenObjects);
                    }

                    // 3. DoorLocked
                    var lockedDoors = UnityEngine.Object.FindObjectsOfType<DoorLocked>(true);
                    foreach (var ld in lockedDoors)
                    {
                        if (ld != null)
                            TryAddHotspot(ld.gameObject, "Locked " + CleanDoorName(ld.gameObject), HotspotCategory.Doors, playerPos, seenObjects);
                    }

                    // 4. MetalDoor
                    var metalDoors = UnityEngine.Object.FindObjectsOfType<MetalDoor>(true);
                    foreach (var md in metalDoors)
                    {
                        if (md != null)
                            TryAddHotspot(md.gameObject, "Metal Door", HotspotCategory.Doors, playerPos, seenObjects);
                    }

                    // 5. MetalLockedDoor
                    var metalLocked = UnityEngine.Object.FindObjectsOfType<MetalLockedDoor>(true);
                    foreach (var mld in metalLocked)
                    {
                        if (mld != null)
                            TryAddHotspot(mld.gameObject, "Locked Metal Door", HotspotCategory.Doors, playerPos, seenObjects);
                    }

                    // 6. OuterAreaDoor
                    var outerDoors = UnityEngine.Object.FindObjectsOfType<OuterAreaDoor>(true);
                    foreach (var od in outerDoors)
                    {
                        if (od != null)
                            TryAddHotspot(od.gameObject, "Outer Area Gate", HotspotCategory.Doors, playerPos, seenObjects);
                    }

                    // 7. IntroEventDoor
                    var introDoors = UnityEngine.Object.FindObjectsOfType<IntroEventDoor>(true);
                    foreach (var id in introDoors)
                    {
                        if (id != null)
                            TryAddHotspot(id.gameObject, "Entrance Gate", HotspotCategory.Doors, playerPos, seenObjects);
                    }
                    break;

                case HotspotCategory.Items:
                    // 1. ItemPicker (Ritual items, tools, potions, matches, etc.)
                    var items = UnityEngine.Object.FindObjectsOfType<ItemPicker>(true);
                    foreach (var it in items)
                    {
                        if (it != null && (playerTransform == null || !it.transform.IsChildOf(playerTransform)))
                        {
                            TryAddHotspot(it.gameObject, CleanItemName(it), HotspotCategory.Items, playerPos, seenObjects);
                        }
                    }

                    // 2. Key (Haveli keys)
                    var keys = UnityEngine.Object.FindObjectsOfType<Key>(true);
                    foreach (var k in keys)
                    {
                        if (k != null && !k.HasCollectedKey && (playerTransform == null || !k.transform.IsChildOf(playerTransform)))
                        {
                            TryAddHotspot(k.gameObject, CleanKeyName(k), HotspotCategory.Items, playerPos, seenObjects);
                        }
                    }

                    // 3. Book (Ancient Prayer Book)
                    var books = UnityEngine.Object.FindObjectsOfType<Book>(true);
                    foreach (var b in books)
                    {
                        if (b != null && (playerTransform == null || !b.transform.IsChildOf(playerTransform)))
                        {
                            TryAddHotspot(b.gameObject, "Ancient Prayer Book", HotspotCategory.Items, playerPos, seenObjects);
                        }
                    }

                    // 4. Note (Haveli Notes)
                    var notes = UnityEngine.Object.FindObjectsOfType<Note>(true);
                    foreach (var n in notes)
                    {
                        if (n != null && (playerTransform == null || !n.transform.IsChildOf(playerTransform)))
                        {
                            TryAddHotspot(n.gameObject, "Haveli Note", HotspotCategory.Items, playerPos, seenObjects);
                        }
                    }

                    // 5. ItemInCourtyard
                    var courtItems = UnityEngine.Object.FindObjectsOfType<ItemInCourtyard>(true);
                    foreach (var ci in courtItems)
                    {
                        if (ci != null)
                        {
                            string rName = FormatRecipeItem(ci.MyItem);
                            TryAddHotspot(ci.gameObject, rName, HotspotCategory.Items, playerPos, seenObjects);
                        }
                    }
                    break;

                case HotspotCategory.Containers:
                    var almirahs = UnityEngine.Object.FindObjectsOfType<OpenableAlmirah>(true);
                    foreach (var a in almirahs)
                    {
                        if (a != null)
                            TryAddHotspot(a.gameObject, "Haveli Almirah", HotspotCategory.Containers, playerPos, seenObjects);
                    }

                    var chests = UnityEngine.Object.FindObjectsOfType<OpenableChest>(true);
                    foreach (var c in chests)
                    {
                        if (c != null)
                            TryAddHotspot(c.gameObject, "Haveli Chest", HotspotCategory.Containers, playerPos, seenObjects);
                    }

                    var drawers = UnityEngine.Object.FindObjectsOfType<TableDrawer>(true);
                    foreach (var dr in drawers)
                    {
                        if (dr != null)
                            TryAddHotspot(dr.gameObject, "Table Drawer", HotspotCategory.Containers, playerPos, seenObjects);
                    }

                    var suitcases = UnityEngine.Object.FindObjectsOfType<Suitcase>(true);
                    foreach (var sc in suitcases)
                    {
                        if (sc != null)
                            TryAddHotspot(sc.gameObject, "Combination Suitcase", HotspotCategory.Containers, playerPos, seenObjects);
                    }
                    break;

                case HotspotCategory.RitualAndPuzzles:
                    var havan = GameObject.Find("PlaceCircleCourtyard") ?? GameObject.Find("HavanParticleSystem") ?? GameObject.Find("CourtyardMechanic");
                    if (havan != null)
                        TryAddHotspot(havan, "Courtyard Havan Altar", HotspotCategory.RitualAndPuzzles, playerPos, seenObjects);

                    var ganesh = GameObject.Find("GaneshMurtiManager") ?? GameObject.Find("GaneshMurti");
                    if (ganesh != null)
                        TryAddHotspot(ganesh, "Ganesh Idol", HotspotCategory.RitualAndPuzzles, playerPos, seenObjects);

                    var bells = UnityEngine.Object.FindObjectsOfType<HangingBell>(true);
                    foreach (var b in bells)
                    {
                        if (b != null)
                            TryAddHotspot(b.gameObject, "Temple Bell", HotspotCategory.RitualAndPuzzles, playerPos, seenObjects);
                    }

                    var bricks = UnityEngine.Object.FindObjectsOfType<PushableBrick>(true);
                    foreach (var br in bricks)
                    {
                        if (br != null)
                            TryAddHotspot(br.gameObject, "Puzzle Brick", HotspotCategory.RitualAndPuzzles, playerPos, seenObjects);
                    }
                    break;

                case HotspotCategory.HidingSpots:
                    var hides = UnityEngine.Object.FindObjectsOfType<HideDetectionCollider>(true);
                    foreach (var h in hides)
                    {
                        if (h != null)
                            TryAddHotspot(h.gameObject, "Hiding Spot", HotspotCategory.HidingSpots, playerPos, seenObjects);
                    }

                    var almHides = UnityEngine.Object.FindObjectsOfType<OpenableAlmirah>(true);
                    foreach (var al in almHides)
                    {
                        if (al != null)
                            TryAddHotspot(al.gameObject, "Wardrobe Hiding Spot", HotspotCategory.HidingSpots, playerPos, seenObjects);
                    }
                    break;

                case HotspotCategory.Kamla:
                    var enemy = UnityEngine.Object.FindObjectOfType<Enemy>();
                    if (enemy != null)
                        TryAddHotspot(enemy.gameObject, "Kamla", HotspotCategory.Kamla, playerPos, seenObjects);
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

            // 2. Check GameObject name against known Haveli item patterns
            string rawName = it.gameObject.name.Replace("(Clone)", "").Replace("_", " ").Trim();
            string recognized = FormatKnownItemName(rawName);
            if (!string.IsNullOrEmpty(recognized))
                return recognized;

            // 3. Check headhunted string if it contains a valid name
            if (!string.IsNullOrEmpty(it.headhunted) &&
                !it.headhunted.Contains("Holding", StringComparison.OrdinalIgnoreCase))
            {
                string h = FormatKnownItemName(it.headhunted.Trim());
                if (!string.IsNullOrEmpty(h)) return h;
                return it.headhunted.Trim();
            }

            // 4. Check RECIPE_ITEM enum ONLY if NOT None and NOT 0/Camphor unless confirmed by name
            try
            {
                if (it.MyItem != GlobalConstants.RECIPE_ITEM.None && it.MyItem != GlobalConstants.RECIPE_ITEM.Camphor)
                {
                    return FormatRecipeItem(it.MyItem);
                }
                else if (it.MyItem == GlobalConstants.RECIPE_ITEM.Camphor &&
                         (rawName.IndexOf("Camphor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          rawName.IndexOf("Kapoor", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return "Camphor (Kapoor)";
                }
            }
            catch { }

            return SplitCamelCase(rawName);
        }

        public static string CleanKeyName(Key k)
        {
            if (k == null) return "Key";
            if (!string.IsNullOrEmpty(k.keyName)) return FormatKnownItemName(k.keyName) ?? SplitCamelCase(k.keyName);
            string raw = k.gameObject.name.Replace("(Clone)", "").Replace("_", " ").Trim();
            return FormatKnownItemName(raw) ?? SplitCamelCase(raw);
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
            if (raw.IndexOf("Room", StringComparison.OrdinalIgnoreCase) >= 0) return SplitCamelCase(raw);
            return string.IsNullOrEmpty(raw) ? "Haveli Door" : SplitCamelCase(raw);
        }

        public static string FormatKnownItemName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string lower = name.ToLowerInvariant();

            if (lower.Contains("camphor") || lower.Contains("kapoor")) return "Camphor (Kapoor)";
            if (lower.Contains("salt") || lower.Contains("saltbowl")) return "Salt Bowl";
            if (lower.Contains("thali") || lower.Contains("poojathali")) return "Puja Thali";
            if (lower.Contains("chalisa") || lower.Contains("hanuman")) return "Hanuman Chalisa";
            if (lower.Contains("gangajal") || lower.Contains("holywater")) return "Holy Water (Gangajal)";
            if (lower.Contains("kalash") || lower.Contains("kalashpot")) return "Kalash Pot";
            if (lower.Contains("ganesh") || lower.Contains("murti")) return "Ganesh Idol";
            if (lower.Contains("nazar") || lower.Contains("battu")) return "Nazar Battu";
            if (lower.Contains("smudge") || lower.Contains("dhoop") || lower.Contains("stick")) return "Smudge Stick (Dhoop)";
            if (lower.Contains("umbrella") || lower.Contains("chatri")) return "Umbrella";
            if (lower.Contains("match") || lower.Contains("matchbox")) return "Matches";
            if (lower.Contains("key")) return SplitCamelCase(name);
            if (lower.Contains("prayer") || lower.Contains("book")) return "Ancient Prayer Book";
            if (lower.Contains("trishul")) return "Trishul";
            if (lower.Contains("diya") || lower.Contains("lamp")) return "Oil Lamp (Diya)";
            if (lower.Contains("bell") || lower.Contains("ghanti")) return "Ritual Bell";

            return null;
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

        public static string SplitCamelCase(string str)
        {
            if (string.IsNullOrEmpty(str)) return "Item";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                if (char.IsUpper(c) && i > 0 && !char.IsUpper(str[i - 1]) && str[i - 1] != ' ')
                {
                    sb.Append(' ');
                }
                sb.Append(c);
            }
            return sb.ToString().Trim();
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
