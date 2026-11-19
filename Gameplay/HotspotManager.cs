using System;
using System.Collections.Generic;
using UnityEngine;
using Il2Cpp;
using MelonLoader;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using KamlaAccessibility.Core;
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
        public Transform ApproachPoint { get; }
        public Collider InteractionCollider { get; }
        public bool IsClue { get; }
        public Vector3 Position => ApproachPoint != null ? ApproachPoint.position :
            InteractionCollider != null && InteractionCollider.enabled ? InteractionCollider.bounds.center :
            GameObject.transform.position;

        public HotspotItem(GameObject go, string cleanName, HotspotCategory cat, Transform approachPoint = null,
            bool isClue = false)
        {
            GameObject = go;
            CleanName = cleanName;
            Category = cat;
            ApproachPoint = approachPoint;
            InteractionCollider = approachPoint == null ? FindInteractionCollider(go) : null;
            IsClue = isClue;
        }

        internal static Collider FindInteractionCollider(GameObject go)
        {
            var pump = go.GetComponent<Handpump>();
            if (pump != null && pump.triggerCollider != null && pump.triggerCollider.enabled) return pump.triggerCollider;

            // Prefer a collider on the actual interactive object over decorative child meshes.
            Collider solid = null;
            foreach (var collider in go.GetComponents<Collider>())
            {
                if (collider == null || !collider.enabled) continue;
                if (collider.isTrigger) return collider;
                solid ??= collider;
            }
            return solid;
        }

        public string GetStateString()
        {
            if (GameObject == null) return "Gone";

            // 1. Doors
            var gate = GameObject.GetComponent<MainGateOuterArea>();
            if (gate != null) return gate.isOpen ? "Open" : "Closed";

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
                return introDoor.isOpened ? "Open" : introDoor.IsEventComplete ? "Closed" : "Locked from behind";
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
                return PuzzleAccessibility.IsRitualPlaced(courtItem) ? "Placed on Altar" : "Ready for Ritual";
            }

            var statue = GameObject.GetComponent<StatueRotate>();
            if (statue != null) return $"Orientation {statue.rotateCode + 1}";

            var brick = GameObject.GetComponent<PushableBrick>();
            if (brick != null) return brick.isPushed ? "Pushed" : "Ready to push";

            var suitcase = GameObject.GetComponent<Suitcase>();
            if (suitcase != null) return suitcase.unlocked ? "Unlocked" : "Locked";

            var cabinet = GameObject.GetComponent<GlassShelf>();
            if (cabinet != null) return cabinet.unlocked ? "Unlocked" : "Locked";

            var pump = GameObject.GetComponent<Handpump>();
            if (pump != null) return PuzzleAccessibility.DescribePump(pump);

            var fire = GameObject.GetComponent<FireBox>();
            if (fire != null) return fire.hasExtinguishedFire ? "Extinguished" :
                fire.count > 0 && fire.handpumpRef != null && !fire.handpumpRef.hasFilledWater
                    ? "One pour done. Refill the water pot" :
                fire.count > 0 ? "One pour done. Needs the second pour" :
                fire.handpumpRef != null && !fire.handpumpRef.hasFilledWater ? "Needs a bucket of water from the handpump" : "Burning";

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
        private HotspotItem _selectedItem;

        private HotspotItem _targetItem;
        private List<Vector3> _waypoints;
        private Vector3 _plannedTargetPosition;
        private int _currentWaypointIndex;
        private Vector3 _lastRecordedPos;
        private float _stuckTimer;
        private float _hideEntryTimer;
        private bool _retriedBlockedPath;
        private bool _handledThreat;
        private bool _retryHiding;
        private bool _failedNearbyCoverSearch;
        private readonly HashSet<int> _triedHidingSpots = new HashSet<int>();
        private PlayerMovement _routePlayer;
        private TPScript _hideDestination;
        private Vector2 _routeInput;
        private Vector3 _blockedDirection;
        private float _nextLocalRefresh;
        private PlayerMovement _localObserver;
        private Vector3? _failedApproach;
        private GameObject _arrivedTarget;
        private bool _passingGate;
        private float _gateWaitUntil;
        private float _blockedDoorWaitUntil;

        internal GameObject ArrivedTarget => _arrivedTarget;

        public bool IsAutoWalking => _targetItem != null;
        public HotspotItem Selection => _selectedItem;

        public Vector3 TakeBlockedDirection()
        {
            Vector3 direction = _blockedDirection;
            _blockedDirection = Vector3.zero;
            return direction;
        }

        public bool TryGetMovement(PlayerMovement player, out Vector2 movement, out bool sprint)
        {
            movement = _routeInput;
            sprint = _hideDestination != null;
            return _routePlayer == player && IsAutoWalking && Application.isFocused &&
                !player.IsHiding && !player.IsKilling && !player.IsGettingOut && !player.isPlayingVid &&
                Time.timeScale > 0f && player.Controller != null && player.Controller.enabled;
        }

        public void HandleInput(PlayerMovement player)
        {
            if (Input.GetKeyDown(KeyCode.F5))
            {
                RefreshLocalHotspots(player);
                var category = Categories[_currentCategoryIndex];
                string name = FormatCategoryName(category);
                string count = _currentItems.Count == 0 ? $"No local {name.ToLowerInvariant()} nearby." :
                    $"{name}. {_currentItems.Count} local objects. Page Up and Page Down browse.";
                PrismSpeechProvider.Speak($"{count} {PuzzleAccessibility.DescribeFootsteps(player)}".Trim(), true);
            }
            else if (!IsAutoWalking && Time.unscaledTime >= _nextLocalRefresh) RefreshLocalHotspots(player);
            if (Input.GetKeyDown(KeyCode.Home)) CycleCategory(player.transform, -1);
            else if (Input.GetKeyDown(KeyCode.End)) CycleCategory(player.transform, 1);

            if (Input.GetKeyDown(KeyCode.PageUp)) CycleItem(player.transform, -1);
            else if (Input.GetKeyDown(KeyCode.PageDown)) CycleItem(player.transform, 1);

            var input = PlayerInputManager.Instance;
            bool manualMove = (input != null && input.MoveInput.sqrMagnitude > 0.04f) || Input.GetKeyDown(KeyCode.Space);
            UpdateWalking(player, manualMove, Input.GetKeyDown(KeyCode.Delete));
        }

        private void UpdateWalking(PlayerMovement player, bool manualMove, bool delete)
        {
            if (manualMove) _arrivedTarget = null;
            var enemy = player.enemyRef;
            if (manualMove || delete || !ModConfig.AutomaticHiding || enemy == null || !enemy.isActiveAndEnabled)
                _retryHiding = false;
            bool threatened = ModConfig.AutomaticHiding && HidingController.IsThreatNearby(player, enemy, _handledThreat);
            if (!threatened && !_retryHiding && _hideDestination == null)
            {
                _handledThreat = false;
                _failedNearbyCoverSearch = false;
                _triedHidingSpots.Clear();
            }

            if (threatened && enemy.IsChasing && _failedNearbyCoverSearch)
            {
                _handledThreat = false;
                _failedNearbyCoverSearch = false;
            }

            if (IsAutoWalking && (delete || manualMove))
            {
                bool takingOverFromHiding = _hideDestination != null;
                StopAutoWalk("Auto-walk cancelled.");
                if (takingOverFromHiding) _handledThreat = threatened;
                return;
            }

            if ((_retryHiding || threatened && !_handledThreat) && !manualMove && player.CanInteract)
            {
                _handledThreat = true;
                string warning = _retryHiding ? "Trying another hiding spot." :
                    enemy.IsChasing ? "Kamla is chasing you." : "Kamla is close.";
                _retryHiding = false;
                if (HidingController.TryFindRoute(player, _triedHidingSpots, out var spot, out var route))
                {
                    _triedHidingSpots.Add(spot.GetInstanceID());
                    var target = new HotspotItem(spot.gameObject, HidingController.GetName(spot), HotspotCategory.HidingSpots, spot.BPoint);
                    BeginRoute(player, target, route, spot);
                    PrismSpeechProvider.Speak($"{warning} Heading for {target.CleanName}.", true);
                }
                else
                {
                    _failedNearbyCoverSearch = !enemy.IsChasing;
                    PrismSpeechProvider.Speak($"{warning} No reachable hiding spot nearby.", true);
                }
            }
            else if (delete)
            {
                StartAutoWalk(player);
            }

            if (IsAutoWalking) UpdateAutoWalk(player);
        }

        private void RefreshLocalHotspots(PlayerMovement player)
        {
            _nextLocalRefresh = Time.unscaledTime + 1f;
            ScanCategory(player.transform, Categories[_currentCategoryIndex]);
            // The local list follows the player, but Delete can retry a selected destination after a chase.
            if (_selectedItem != null && (_selectedItem.GameObject == null ||
                !_selectedItem.GameObject.activeInHierarchy || _selectedItem.GameObject.scene != player.gameObject.scene))
                _selectedItem = null;
            if (_arrivedTarget != null && !PuzzleAccessibility.CanReach(player, _arrivedTarget, 3.8f))
                _arrivedTarget = null;
        }

        private void CycleCategory(Transform playerTransform, int direction)
        {
            _currentCategoryIndex = (_currentCategoryIndex + direction + Categories.Length) % Categories.Length;
            _selectedItem = null;
            _arrivedTarget = null;
            HotspotCategory selectedCat = Categories[_currentCategoryIndex];

            ScanCategory(playerTransform, selectedCat);

            AudioCueGenerator.PlayNavSelect();
            string catName = FormatCategoryName(selectedCat);
            MelonLogger.Msg($"[Navigation] {catName}: {_currentItems.Count} nearby hotspots.");
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
            HotspotCategory selectedCat = Categories[_currentCategoryIndex];

            ScanCategory(playerTransform, selectedCat);

            if (_currentItems.Count == 0)
            {
                _selectedItem = null;
                AudioCueGenerator.PlayBoundaryHit();
                PrismSpeechProvider.Speak($"No accessible objects found in {FormatCategoryName(selectedCat)} category.", true);
                return;
            }

            int currentIndex = _selectedItem == null
                ? -1
                : _currentItems.FindIndex(item => item.GameObject == _selectedItem.GameObject);
            int nextIndex = currentIndex < 0
                ? (direction > 0 ? 0 : _currentItems.Count - 1)
                : (currentIndex + direction + _currentItems.Count) % _currentItems.Count;
            HotspotItem item = _currentItems[nextIndex];

            Vector3 forward = playerTransform != null ? playerTransform.forward : Vector3.forward;
            float dist = Vector3.Distance(playerPos, item.Position);
            int clock = GetClockDirection(forward, item.Position - playerPos);
            string state = item.GetStateString();

            _selectedItem = item;
            _arrivedTarget = null;
            AudioCueGenerator.PlayNavTick();
            PrismSpeechProvider.Speak($"{item.CleanName}, {dist:F1} meters at {clock} o'clock, {state}.", true);
        }

        private void StartAutoWalk(PlayerMovement player)
        {
            _arrivedTarget = null;
            HotspotItem target = _selectedItem;
            if (target == null)
            {
                PrismSpeechProvider.Speak("No object selected. Press Page Up or Page Down to choose a hotspot.", true);
                return;
            }
            if (target.GameObject == null || !target.GameObject.activeInHierarchy)
            {
                _selectedItem = null;
                PrismSpeechProvider.Speak("Selected object is no longer available.", true);
                return;
            }
            if (target.Category != HotspotCategory.HidingSpots && NavigationHelper.IsAtInteraction(player, target))
            {
                ArriveAtTarget(player, target);
                return;
            }
            if (!NavigationHelper.TryFindPath(player, target, out var route))
            {
                AudioCueGenerator.PlayBoundaryHit();
                PrismSpeechProvider.Speak($"No walkable route to {target.CleanName} from here.", true);
                _blockedDirection = target.Position - player.transform.position;
                MelonLogger.Msg($"[Navigation] No route to {target.CleanName}, {Vector3.Distance(player.transform.position, target.Position):F1} meters away, in {target.GameObject.scene.name}.");
                return;
            }

            BeginRoute(player, target, route);
            AudioCueGenerator.PlayNavSelect();
            Vector3 firstLeg = route[route.Count - 1];
            foreach (var point in route)
            {
                if (NavigationHelper.Flat(point - player.transform.position).sqrMagnitude <= 0.35f * 0.35f) continue;
                firstLeg = point;
                break;
            }
            int clock = GetClockDirection(player.transform.forward, firstLeg - player.transform.position);
            PrismSpeechProvider.Speak($"Walking to {target.CleanName}. First direction, {clock} o'clock.", true);
        }

        internal void FollowFootsteps(PlayerMovement player, FootstepSmudgeStick trail)
        {
            var source = trail.footstepMan?.footstepAudioSource;
            if (trail.isNear || !player.IsHavingItem || source == null ||
                !source.activeInHierarchy || !trail.footstepMan.isSoundActive)
            {
                PrismSpeechProvider.Speak("The footstep trail is unavailable right now.", true);
                return;
            }

            _selectedItem = new HotspotItem(trail.gameObject, "Footstep trail", HotspotCategory.RitualAndPuzzles,
                source.transform);
            StartAutoWalk(player);
            if (!IsAutoWalking) PrismSpeechProvider.Speak(PuzzleAccessibility.DescribeFootsteps(player), false);
        }

        private void BeginRoute(PlayerMovement player, HotspotItem target, List<Vector3> route, TPScript hideDestination = null)
        {
            _routePlayer = player;
            _targetItem = target;
            _waypoints = route;
            _plannedTargetPosition = target.Position;
            _hideDestination = hideDestination;
            _currentWaypointIndex = 0;
            _lastRecordedPos = player.transform.position;
            _stuckTimer = 0f;
            _blockedDoorWaitUntil = 0f;
            _hideEntryTimer = 0f;
            _retriedBlockedPath = false;
            _failedApproach = null;
            _arrivedTarget = null;
            _passingGate = false;
            _routeInput = Vector2.zero;
            MelonLogger.Msg($"[Navigation] Started route to {target.CleanName}, {route.Count} waypoints.");
        }

        private void UpdateAutoWalk(PlayerMovement player)
        {
            if (player != _routePlayer || PlayerInputManager.Instance == null ||
                player.Controller == null || !player.Controller.enabled)
            {
                StopAutoWalk("Player movement is unavailable.");
                return;
            }
            if (_targetItem.GameObject == null || !_targetItem.GameObject.activeInHierarchy)
            {
                var encounter = _targetItem.GameObject != null
                    ? _targetItem.GameObject.GetComponent<ColliderForCryingEvent>() : null;
                if (encounter != null && encounter.cryingKamlaMan != null && encounter.cryingKamlaMan.ActivateEvent)
                {
                    StopAutoWalk("The crying encounter has started.");
                    return;
                }
                FailRoute("The target is no longer available.");
                return;
            }
            if (_hideDestination != null && _hideDestination.IsNear &&
                (_hideDestination.isRayLooking || PuzzleAccessibility.CanReach(player, _hideDestination.gameObject, 2f)) &&
                HidingController.TryEnter(player, _hideDestination))
            {
                OnHidingEntered();
                return;
            }
            if (!_passingGate && _hideDestination == null && NavigationHelper.IsAtInteraction(player, _targetItem))
            {
                ArriveAtTarget(player, _targetItem);
                return;
            }

            var controller = player.Controller;
            Vector3 position = player.transform.position;
            Vector3 feet = NavigationHelper.GetFeet(controller);
            _routeInput = Vector2.zero;
            if ((_targetItem.Position - _plannedTargetPosition).sqrMagnitude > 0.75f * 0.75f &&
                _targetItem.GameObject.GetComponent<ItemPicker>() != null)
            {
                if (!NavigationHelper.TryFindPath(player, _targetItem, out var updatedRoute))
                {
                    FailRoute($"{_targetItem.CleanName} moved. No walkable route to its new location. Press Delete to retry.");
                    return;
                }

                _waypoints = updatedRoute;
                _currentWaypointIndex = 0;
                _plannedTargetPosition = _targetItem.Position;
                _lastRecordedPos = position;
                _stuckTimer = 0f;
                _blockedDoorWaitUntil = 0f;
                _retriedBlockedPath = false;
                _failedApproach = null;
            }
            const float finalTolerance = 0.12f;

            while (_currentWaypointIndex < _waypoints.Count)
            {
                Vector3 next = _waypoints[_currentWaypointIndex] - feet;
                float tolerance = _currentWaypointIndex == _waypoints.Count - 1 ? finalTolerance : 0.24f;
                if (NavigationHelper.Flat(next).sqrMagnitude > tolerance * tolerance || Mathf.Abs(next.y) > 0.9f) break;
                _currentWaypointIndex++;
            }

            if (_currentWaypointIndex == _waypoints.Count)
            {
                if (_hideDestination == null)
                {
                    if (_passingGate) StopAutoWalk("Through the main gate.");
                    else if (CanPickUpFromHere(player, _targetItem)) ArriveAtTarget(player, _targetItem);
                    else if (!_failedApproach.HasValue &&
                            NavigationHelper.TryFindPath(player, _targetItem, out var approach, feet))
                    {
                        _failedApproach = feet;
                        _waypoints = approach;
                        _currentWaypointIndex = 0;
                    }
                    else StopAutoWalk($"Could not reach an interaction position for {_targetItem.CleanName}.");
                }
                else if (HidingController.TryEnter(player, _hideDestination))
                {
                    OnHidingEntered();
                }
                else
                {
                    _hideEntryTimer += Time.deltaTime;
                    if (_hideEntryTimer >= 1.25f) FailRoute("Reached the hiding spot, but could not get inside.");
                }
                return;
            }

            Vector3 direction = NavigationHelper.Flat(_waypoints[_currentWaypointIndex] - feet);
            if (_passingGate && Time.unscaledTime <= _gateWaitUntil &&
                NavigationHelper.FindBlockingCollider(player, direction, 0.65f) != null) return;
            if (direction.sqrMagnitude > 0.001f)
            {
                player.transform.rotation = Quaternion.LookRotation(direction);
                float speed = _hideDestination != null ? player.sprintSpeed : player.speed;
                _routeInput = new Vector2(0f, Mathf.Clamp(direction.magnitude / Mathf.Max(0.1f, speed * Time.deltaTime), 0.2f, 1f));
            }

            _stuckTimer += Time.deltaTime;
            if (_stuckTimer < (_hideDestination != null ? 0.45f : 1f)) return;

            float moved = Vector3.Distance(position, _lastRecordedPos);
            _lastRecordedPos = position;
            _stuckTimer = 0f;
            if (moved >= (_hideDestination != null ? 0.12f : 0.18f))
            {
                _blockedDoorWaitUntil = 0f;
                _retriedBlockedPath = false;
                return;
            }
            if (_hideDestination == null && !_passingGate && CanPickUpFromHere(player, _targetItem))
            {
                ArriveAtTarget(player, _targetItem);
                return;
            }

            if (!_passingGate && !_retriedBlockedPath)
            {
                if (direction.sqrMagnitude > 0.001f &&
                    (_blockedDoorWaitUntil == 0f || Time.unscaledTime < _blockedDoorWaitUntil) &&
                    NavigationHelper.TryOpenBlockingDoor(player, direction.normalized))
                {
                    if (_blockedDoorWaitUntil == 0f) _blockedDoorWaitUntil = Time.unscaledTime + 2f;
                    return;
                }
                _retriedBlockedPath = true;

                List<Vector3> route = null;
                bool found = direction.sqrMagnitude > 0.001f &&
                    NavigationHelper.TryBypassRouteObstruction(player, direction.normalized, _waypoints, _currentWaypointIndex, out route);
                if (!found) found = _hideDestination != null
                    ? NavigationHelper.TryFindCoverDetour(player, _targetItem.Position, out route)
                    : NavigationHelper.TryFindPath(player, _targetItem, out route,
                        preferPhysicalRoute: direction.sqrMagnitude > 0.001f &&
                            NavigationHelper.FindBlockingCollider(player, direction.normalized, 0.85f) != null);
                if (found)
                {
                    _waypoints = route;
                    _currentWaypointIndex = 0;
                    return;
                }
            }
            _blockedDirection = direction;
            if (_hideDestination == null && direction.sqrMagnitude > 0.001f)
            {
                var obstacle = NavigationHelper.FindBlockingCollider(player, direction.normalized, 0.85f);
                var brick = obstacle != null ? obstacle.GetComponentInParent<PushableBrick>() : null;
                if (brick != null && PuzzleAccessibility.CanReach(player, brick.gameObject, 3f))
                {
                    bool canPush = !brick.isPushed && brick.brickMan != null && brick.brickMan.pushEnabled;
                    FailRoute(canPush
                        ? $"A {PuzzleAccessibility.DescribeBrick(brick)} blocks the route. Press Enter to push it, then Delete to retry."
                        : $"A {PuzzleAccessibility.DescribeBrick(brick)} blocks the route. The puzzle mechanism must be activated first.");
                    if (canPush) _arrivedTarget = brick.gameObject;
                    _blockedDirection = Vector3.zero;
                    return;
                }
            }
            FailRoute($"The route is blocked before {_targetItem.CleanName}.");
        }

        private static bool CanPickUpFromHere(PlayerMovement player, HotspotItem target)
        {
            var item = target.GameObject.GetComponent<ItemPicker>();
            return item != null && PuzzleAccessibility.CanReach(player, item.gameObject, 3f) && item.canPickItem();
        }

        private void ArriveAtTarget(PlayerMovement player, HotspotItem target)
        {
            if (target.GameObject.GetComponent<ColliderForCryingEvent>() != null)
            {
                StopAutoWalk("The crying encounter has started.");
                return;
            }
            // Turning the camera can move the carried smudge stick out of the reveal area.
            if (target.GameObject.GetComponent<FootstepSmudgeStick>() == null)
            {
                Vector3 face = NavigationHelper.Flat(target.Position - player.transform.position);
                if (face.sqrMagnitude > 0.01f) player.transform.rotation = Quaternion.LookRotation(face);
            }
            StopAutoWalk($"At {target.CleanName}. Press Enter to {(target.IsClue ? "read it" : "use it")}.");
            _arrivedTarget = target.GameObject;
        }

        internal void WalkThroughGate(PlayerMovement player, MainGateOuterArea gate)
        {
            var target = new HotspotItem(gate.gameObject, "Main Haveli Gate", HotspotCategory.Doors);
            Vector3 feet = NavigationHelper.GetFeet(player.Controller);
            Vector3 direction = NavigationHelper.Flat(target.Position - feet);
            if (direction.sqrMagnitude < 0.04f) direction = NavigationHelper.Flat(player.transform.forward);
            Vector3 destination = feet + direction.normalized * (direction.magnitude + 0.8f);
            BeginRoute(player, target, new List<Vector3> { destination });
            _passingGate = true;
            _gateWaitUntil = Time.unscaledTime + 3f;
        }

        private void FailRoute(string message)
        {
            if (_hideDestination != null && _triedHidingSpots.Count < 3)
                _retryHiding = true;
            StopAutoWalk(message);
        }

        public void ResetScene()
        {
            StopAutoWalk();
            _handledThreat = false;
            _retryHiding = false;
            _failedNearbyCoverSearch = false;
            _triedHidingSpots.Clear();
            _blockedDirection = Vector3.zero;
            _selectedItem = null;
            _arrivedTarget = null;
            _currentItems.Clear();
        }

        public void StopAutoWalk(string message = null)
        {
            _targetItem = null;
            _routePlayer = null;
            _hideDestination = null;
            _passingGate = false;
            _routeInput = Vector2.zero;
            _waypoints = null;

            if (!string.IsNullOrEmpty(message))
            {
                MelonLogger.Msg($"[Navigation] {message}");
                AudioCueGenerator.PlayNavSelect();
                PrismSpeechProvider.Speak(message, true);
            }
        }

        internal void OnHidingEntered()
        {
            if (IsAutoWalking)
                StopAutoWalk(_hideDestination != null ? "Hidden. Press Enter to leave cover when the area is clear." : null);
        }
        private void TryAddHotspot(GameObject go, string cleanName, HotspotCategory cat, Vector3 playerPos,
            HashSet<int> seenObjects, Transform approachPoint = null, bool isClue = false)
        {
            if (go == null || !go.activeInHierarchy || !go.scene.isLoaded) return;

            var item = new HotspotItem(go, cleanName, cat, approachPoint, isClue);
            bool exterior = string.Equals(go.scene.name, "Outer Area", StringComparison.OrdinalIgnoreCase);
            bool outdoorLandmark = exterior && (go.GetComponent<MainGateOuterArea>() != null ||
                go.GetComponent<Handpump>() != null || go.GetComponent<FireBox>() != null);
            float range = outdoorLandmark ? 95f : exterior ? 18f : 12f;
            if (Vector3.Distance(playerPos, item.Position) <= range && IsLocal(item, playerPos, range, exterior) &&
                seenObjects.Add(go.GetInstanceID()))
            {
                _currentItems.Add(item);
            }
        }

        private bool IsLocal(HotspotItem item, Vector3 position, float range, bool exterior)
        {
            if (_localObserver == null) return false;
            if (!exterior && Mathf.Abs(item.Position.y - position.y) > 2.1f) return false;
            Vector3 eyes = _localObserver.mainCam != null ? _localObserver.mainCam.transform.position : position;
            if (item.ApproachPoint != null && item.GameObject.GetComponent<FootstepSmudgeStick>() is { } trail && trail != null)
                return trail.isNear || !Physics.Linecast(eyes, item.Position, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (PuzzleAccessibility.CanReachFrom(_localObserver, item.GameObject, position, eyes, range)) return true;
            // A nearby door may expose a trigger at its frame while the closed leaf blocks its center.
            return item.Category == HotspotCategory.Doors && Vector3.Distance(item.Position, position) < 2.1f &&
                   PuzzleAccessibility.CanReachFrom(_localObserver, item.GameObject, position, position, range);
        }

        private void ScanCategory(Transform playerTransform, HotspotCategory category)
        {
            _currentItems.Clear();
            _localObserver = playerTransform.GetComponent<PlayerMovement>();
            Vector3 playerPos = playerTransform.position;
            var seenObjects = new HashSet<int>();
            if (category == HotspotCategory.AllObjects)
            {
                foreach (var entry in Categories)
                {
                    if (entry != HotspotCategory.AllObjects) CollectCategory(playerTransform, entry, seenObjects);
                }
            }
            else
            {
                CollectCategory(playerTransform, category, seenObjects);
            }
            _currentItems.Sort((a, b) =>
            {
                int distance = (a.Position - playerPos).sqrMagnitude.CompareTo((b.Position - playerPos).sqrMagnitude);
                return distance != 0 ? distance : a.GameObject.GetInstanceID().CompareTo(b.GameObject.GetInstanceID());
            });
        }

        private void CollectCategory(Transform playerTransform, HotspotCategory category, HashSet<int> seenObjects)
        {
            Vector3 playerPos = playerTransform.position;
            switch (category)
            {
                case HotspotCategory.Doors:
                    foreach (var gate in UnityEngine.Object.FindObjectsOfType<MainGateOuterArea>())
                    {
                        TryAddHotspot(gate.gameObject, "Main Haveli Gate", category, playerPos, seenObjects);
                    }
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
                            TryAddHotspot(id.gameObject, CleanDoorName(id.gameObject), HotspotCategory.Doors, playerPos, seenObjects);
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

                    // 3. Key book
                    var books = UnityEngine.Object.FindObjectsOfType<Book>(true);
                    foreach (var b in books)
                    {
                        if (b != null && (playerTransform == null || !b.transform.IsChildOf(playerTransform)))
                        {
                            TryAddHotspot(b.gameObject, "Key book", HotspotCategory.Items, playerPos, seenObjects);
                        }
                    }

                    // 4. Note (Haveli Notes)
                    var notes = UnityEngine.Object.FindObjectsOfType<Note>(true);
                    foreach (var n in notes)
                    {
                        if (n != null && (playerTransform == null || !n.transform.IsChildOf(playerTransform)))
                        {
                            TryAddHotspot(n.gameObject, CleanNoteName(n), HotspotCategory.Items, playerPos, seenObjects);
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
                    foreach (var court in UnityEngine.Object.FindObjectsOfType<CourtYardMechanic>())
                        TryAddHotspot(court.gameObject, "Courtyard ritual altar", category, playerPos, seenObjects);
                    foreach (var circle in UnityEngine.Object.FindObjectsOfType<ItemInCourtyard>())
                        TryAddHotspot(circle.gameObject, $"{FormatRecipeItem(circle.MyItem)} offering circle",
                            category, playerPos, seenObjects);
                    foreach (var altar in UnityEngine.Object.FindObjectsOfType<GaneshMurtiManager>())
                        TryAddHotspot(altar.gameObject, "Ganesh altar", category, playerPos, seenObjects);
                    foreach (var encounter in UnityEngine.Object.FindObjectsOfType<ColliderForCryingEvent>())
                        if (encounter.cryingKamlaMan != null && !encounter.cryingKamlaMan.ActivateEvent)
                            TryAddHotspot(encounter.gameObject, "Sound of crying", category, playerPos, seenObjects);

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
                            TryAddHotspot(br.gameObject, PuzzleAccessibility.DescribeBrick(br), category, playerPos, seenObjects);
                    }
                    foreach (var statue in UnityEngine.Object.FindObjectsOfType<StatueRotate>())
                        TryAddHotspot(statue.gameObject, $"Statue {statue.statueNumber + 1}", category, playerPos, seenObjects);
                    foreach (var suitcase in UnityEngine.Object.FindObjectsOfType<Suitcase>())
                    {
                        TryAddHotspot(suitcase.gameObject, "Combination suitcase", category, playerPos, seenObjects);
                        AddClueHotspots(suitcase.numberDecals, "Suitcase clue", playerPos, seenObjects);
                    }
                    foreach (var puzzle in UnityEngine.Object.FindObjectsOfType<StatuePuzzleManager>())
                        AddClueHotspots(puzzle.numberDecals, "Statue clue", playerPos, seenObjects);
                    foreach (var trail in UnityEngine.Object.FindObjectsOfType<FootstepSmudgeStick>())
                    {
                        var manager = trail.footstepMan;
                        if (trail.isActive && manager != null && manager.isSoundActive && manager.footstepAudioSource != null)
                            TryAddHotspot(trail.gameObject, "Footstep trail", category, playerPos, seenObjects,
                                manager.footstepAudioSource.transform);
                    }
                    foreach (var key in UnityEngine.Object.FindObjectsOfType<FootstepKey>())
                        TryAddHotspot(key.gameObject, "Footstep key", category, playerPos, seenObjects);
                    foreach (var pump in UnityEngine.Object.FindObjectsOfType<Handpump>())
                        TryAddHotspot(pump.gameObject, "Handpump", category, playerPos, seenObjects);
                    foreach (var fire in UnityEngine.Object.FindObjectsOfType<FireBox>())
                        if (!fire.hasExtinguishedFire)
                            TryAddHotspot(fire.gameObject, "Fire", category, playerPos, seenObjects);
                    foreach (var shelf in UnityEngine.Object.FindObjectsOfType<GlassShelf>())
                        TryAddHotspot(shelf.gameObject, "Glass cabinet", category, playerPos, seenObjects);
                    foreach (var mechanism in UnityEngine.Object.FindObjectsOfType<ItemDetectForWallSlide>())
                        TryAddHotspot(mechanism.gameObject, "Sliding wall mechanism", category, playerPos, seenObjects);
                    foreach (var item in UnityEngine.Object.FindObjectsOfType<ItemInCourtyard>())
                        TryAddHotspot(item.gameObject, $"{FormatRecipeItem(item.MyItem)} ritual circle", category, playerPos, seenObjects);
                    break;

                case HotspotCategory.HidingSpots:
                    foreach (var spot in UnityEngine.Object.FindObjectsOfType<TPScript>())
                    {
                        if (spot.isActiveAndEnabled && spot.TpPoint != null && spot.BPoint != null)
                            TryAddHotspot(spot.gameObject, HidingController.GetName(spot), category, playerPos, seenObjects, spot.BPoint);
                    }
                    break;

                case HotspotCategory.Kamla:
                    var enemy = UnityEngine.Object.FindObjectOfType<Enemy>();
                    if (enemy != null)
                        TryAddHotspot(enemy.gameObject, "Kamla", HotspotCategory.Kamla, playerPos, seenObjects);
                    break;

            }
        }

        private void AddClueHotspots(Il2CppReferenceArray<GameObject> decals, string name, Vector3 playerPos,
            HashSet<int> seenObjects)
        {
            if (decals == null) return;
            for (int i = 0; i < decals.Length; i++)
            {
                var clue = decals[i];
                if (PuzzleAccessibility.IsClueRevealed(clue))
                      TryAddHotspot(clue, $"{name} {i + 1}", HotspotCategory.RitualAndPuzzles, playerPos, seenObjects, isClue: true);
            }
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

            string rawName = it.gameObject.name.Replace("(Clone)", "").Replace("_", " ").Trim();
            if (it.myItemData != null)
            {
                string name = CleanItemLabel(it.myItemData.ItemNameForDisplay) ?? CleanItemLabel(it.myItemData.ItemName);
                if (name != null) return FormatKnownItemName(name) ?? name;
            }

            string recognized = FormatKnownItemName(rawName);
            if (recognized != null) return recognized;

            if (!it.isNotCourtyardItem && it.MyItem != GlobalConstants.RECIPE_ITEM.None &&
                it.MyItem != GlobalConstants.RECIPE_ITEM.Camphor)
                return FormatRecipeItem(it.MyItem);

            return CleanItemLabel(rawName) ?? "Unidentified item";
        }

        private static string CleanItemLabel(string label)
        {
            label = SubtitleReader.CleanText(label);
            if (string.IsNullOrWhiteSpace(label) ||
                label.StartsWith("Cannot ", StringComparison.OrdinalIgnoreCase) ||
                label.StartsWith("Can't ", StringComparison.OrdinalIgnoreCase) ||
                label.StartsWith("You ", StringComparison.OrdinalIgnoreCase) ||
                label.StartsWith("Press ", StringComparison.OrdinalIgnoreCase) ||
                label.StartsWith("Hold ", StringComparison.OrdinalIgnoreCase) ||
                label.Contains("being chased", StringComparison.OrdinalIgnoreCase))
                return null;
            return SplitCamelCase(label);
        }

        public static string CleanKeyName(Key k)
        {
            if (k == null) return "Key";
            if (!string.IsNullOrEmpty(k.keyName)) return FormatKnownItemName(k.keyName) ?? SplitCamelCase(k.keyName);
            string raw = k.gameObject.name.Replace("(Clone)", "").Replace("_", " ").Trim();
            return FormatKnownItemName(raw) ?? SplitCamelCase(raw);
        }

        public static string CleanNoteName(Note note)
        {
            if (note == null) return "Haveli note";
            string name = note.gameObject.name.Replace("(Clone)", "").Replace("_", " ").Trim();
            return name.StartsWith("Note ", StringComparison.OrdinalIgnoreCase) ? $"Haveli {name}" : "Haveli note";
        }

        public static string CleanDoorName(GameObject go)
        {
            if (go == null) return "Haveli Door";
            string raw = go.name.Replace("(Clone)", "").Replace("_", " ").Trim();
            if (raw.Contains("Kamla Room", StringComparison.OrdinalIgnoreCase)) return "Kamla's room door";
            if (raw.Contains("Umbrella", StringComparison.OrdinalIgnoreCase)) return "Umbrella room door";
            if (raw.StartsWith("Old Interior door", StringComparison.OrdinalIgnoreCase))
            {
                string suffix = raw.Substring("Old Interior door".Length).Trim(' ', '(', ')');
                if (int.TryParse(suffix, out int number)) return $"Interior door {number}";
                if (string.IsNullOrWhiteSpace(suffix)) return "Interior door";
            }
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
            string compact = lower.Replace(" ", "").Replace("_", "").Replace("-", "");

            if (compact.Contains("handpumphandle") || compact.Contains("pumphandle")) return "Handpump handle";
            if (compact == "steelpotfire") return "Steel water pot";
            if (lower.Contains("bucket")) return lower.Contains("steel") ? "Steel bucket" :
                lower.Contains("plastic") ? "Plastic bucket" : "Bucket";
            if (lower.Contains("camphor") || lower.Contains("kapoor")) return "Camphor (Kapoor)";
            if (lower.Contains("salt") || lower.Contains("saltbowl")) return "Salt Bowl";
            if (lower.Contains("thali") || compact.Contains("prayerplate")) return "Prayer plate (Puja Thali)";
            if (lower.Contains("chalisa") || lower.Contains("hanuman") || compact.Contains("prayerbook"))
                return "Prayer book (Hanuman Chalisa)";
            if (compact.Contains("gangajal") || compact.Contains("holywater")) return "Holy Water (Gangajal)";
            if (lower.Contains("kalash") || lower.Contains("kalashpot")) return "Kalash Pot";
            if (lower.Contains("ganesh") || lower.Contains("murti")) return "Ganesh Idol";
            if (lower.Contains("nazar") || lower.Contains("najar") || lower.Contains("battu")) return "Nazar Battu";
            if (lower.Contains("smudge") || lower.Contains("dhoop")) return "Smudge Stick (Dhoop)";
            if (lower.Contains("umbrella") || lower.Contains("chatri")) return "Umbrella";
            if (lower.Contains("match") || lower.Contains("matchbox")) return "Matches";
            if (lower.Contains("key")) return SplitCamelCase(name);
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
                case GlobalConstants.RECIPE_ITEM.Thali: return "Prayer plate (Puja Thali)";
                case GlobalConstants.RECIPE_ITEM.Chalisa: return "Prayer book (Hanuman Chalisa)";
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
