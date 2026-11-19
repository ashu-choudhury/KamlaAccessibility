using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using KamlaAccessibility.Core;
using KamlaAccessibility.Speech;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using Il2CppTMPro;
using Object = UnityEngine.Object;

namespace KamlaAccessibility.Gameplay
{
    internal sealed class PuzzleAccessibility
    {
        private Suitcase _suitcase;
        private int _wheel = -1;
        private int _number = -1;
        private string _typedDigits = "";
        private string _submittedDigits;
        private ItemInCourtyard _ritual;
        private Handpump _pump;
        private bool _holdingRitual;
        private int _ritualProgress = -1;
        private FootstepSmudgeStick _trail;
        private int _lastTrailBand = -1;
        private float _nextTrailCheck;
        private GameObject _lastRecipe;
        private PauseMenuAndRecipeManager _recipeManager;

        public bool IsModalActive => _ritual != null || _pump != null || (_suitcase != null && _suitcase.IsInSuitcaseMode);

        public bool UpdateSuitcase(PlayerMovement player)
        {
            if (_suitcase == null || !_suitcase.IsInSuitcaseMode)
            {
                AnnounceSuitcaseExit();
                _suitcase = player.suitcaseRef;
            }

            if (_suitcase == null || !_suitcase.IsInSuitcaseMode) return false;
            var wheels = _suitcase.wheels;
            if (wheels == null || wheels.Length == 0) return true;
            int selected = Mathf.Clamp(_suitcase.selectedWheel, 0, wheels.Length - 1);
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _typedDigits = "";
                _submittedDigits = null;
                WithInteract(_suitcase.suitcaseMode, _suitcase, back: true);
                return true;
            }
            if (_submittedDigits != null)
            {
                DialSubmittedDigits(wheels);
                return true;
            }
            for (int digit = 0; digit <= 9; digit++)
            {
                if (!Input.GetKeyDown(KeyCode.Alpha0 + digit) && !Input.GetKeyDown(KeyCode.Keypad0 + digit)) continue;
                TypeSuitcaseDigit(digit, wheels.Length);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Backspace) && _typedDigits.Length > 0)
            {
                _typedDigits = _typedDigits.Substring(0, _typedDigits.Length - 1);
                Say(_typedDigits.Length == 0 ? "Combination cleared." : $"Digits, {string.Join(", ", _typedDigits.ToCharArray())}.");
                return true;
            }
            if (_typedDigits.Length > 0)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    SubmitSuitcaseDigits(wheels.Length);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow)) selected = (selected + wheels.Length - 1) % wheels.Length;
            if (Input.GetKeyDown(KeyCode.RightArrow)) selected = (selected + 1) % wheels.Length;
            _suitcase.selectedWheel = selected;
            var wheel = wheels[selected] != null ? wheels[selected].GetComponent<RotateWheel>() : null;
            if (wheel == null) return true;

            if (Input.GetKeyDown(KeyCode.UpArrow)) wheel.keyPressUp();
            if (Input.GetKeyDown(KeyCode.DownArrow)) wheel.keyPressDown();
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ConfirmSuitcaseWheel(wheel, selected, wheels);
                return true;
            }
            bool first = _wheel < 0;
            if (_wheel != selected || _number != wheel.numberShown)
            {
                _wheel = selected;
                _number = wheel.numberShown;
                string help = first ? "Suitcase. Type the combination using number keys or the number pad, then press Enter. Backspace corrects a digit. Left and Right choose a wheel. Up and Down turn it. Escape leaves. " : "";
                Say($"{help}Wheel {selected + 1} of {wheels.Length}, {_number}.");
            }
            return true;
        }

        private void TypeSuitcaseDigit(int digit, int wheelCount)
        {
            if (_typedDigits.Length >= wheelCount) { Say("Combination ready. Press Enter or Backspace."); return; }
            _typedDigits += (char)('0' + digit);
            Say($"Digit {_typedDigits.Length}, {digit}." + (_typedDigits.Length == wheelCount ? " Press Enter to dial." : ""));
        }

        private void SubmitSuitcaseDigits(int wheelCount)
        {
            if (_typedDigits.Length != wheelCount) { Say($"Enter all {wheelCount} digits."); return; }
            _submittedDigits = _typedDigits;
            _typedDigits = "";
            Say($"Dialing {string.Join(", ", _submittedDigits.ToCharArray())}.");
        }

        internal bool IsDialing(Suitcase suitcase) => _submittedDigits != null && _suitcase == suitcase;

        private void DialSubmittedDigits(Il2CppReferenceArray<GameObject> wheels)
        {
            for (int i = 0; i < wheels.Length; i++)
            {
                var wheel = wheels[i] != null ? wheels[i].GetComponent<RotateWheel>() : null;
                if (wheel == null) { _submittedDigits = null; Say("A suitcase dial is unavailable."); return; }
                if (!wheel.coroutineAllowed) return;
                int steps = (_submittedDigits[i] - '0' - wheel.numberShown + 10) % 10;
                if (steps == 0) continue;
                _suitcase.selectedWheel = i;
                _wheel = i;
                if (steps <= 5) wheel.keyPressUp();
                else wheel.keyPressDown();
                return;
            }
            _submittedDigits = null;
            _suitcase.checkResults();
            if (!_suitcase.unlocked) Say("The suitcase is still locked. Enter another combination.");
        }

        public void AnnounceSuitcaseExit()
        {
            if (_wheel < 0 || (_suitcase != null && _suitcase.IsInSuitcaseMode)) return;
            if (_suitcase != null && _suitcase.unlocked)
            {
                var wheels = _suitcase.wheels;
                var dial = wheels != null && _wheel < wheels.Length && wheels[_wheel] != null
                    ? wheels[_wheel].GetComponent<RotateWheel>() : null;
                Say(dial != null ? $"Wheel {_wheel + 1}, {dial.numberShown}. Suitcase unlocked." : "Suitcase unlocked.");
            }
            else Say("Left the suitcase.");
            _wheel = -1;
            _number = -1;
            _typedDigits = "";
            _submittedDigits = null;
        }

        private void ConfirmSuitcaseWheel(RotateWheel wheel, int selected, Il2CppReferenceArray<GameObject> wheels)
        {
            if (!wheel.coroutineAllowed)
            {
                Say("Wait for the wheel to stop turning.");
                return;
            }

            int digit = wheel.numberShown;
            _suitcase.checkResults();
            if (_suitcase.unlocked)
            {
                Say($"Wheel {selected + 1}, {digit}. Suitcase unlocked.");
                _wheel = -1;
                _number = -1;
                return;
            }

            if (selected + 1 >= wheels.Length)
            {
                Say($"Wheel {selected + 1}, {digit}. The suitcase is still locked.");
                return;
            }

            int next = selected + 1;
            _suitcase.selectedWheel = next;
            var nextWheel = wheels[next] != null ? wheels[next].GetComponent<RotateWheel>() : null;
            _wheel = next;
            _number = nextWheel != null ? nextWheel.numberShown : -1;
            Say($"Wheel {selected + 1}, {digit}. Wheel {next + 1}{(_number >= 0 ? $", {_number}" : "")}.");
        }

        public bool UpdateRitual(PlayerMovement player)
        {
            if (_pump != null)
            {
                if (_pump.hasFilledWater)
                {
                    Say("Water pot filled. Press Enter at the handpump to pick it up.");
                    StopRitual();
                    return true;
                }
                if (!CanReach(player, _pump.gameObject, 3f) || !_pump.isNear || Input.GetKeyDown(KeyCode.Escape))
                {
                    StopRitual();
                    return false;
                }
                ReportHoldProgress(_pump.currentTime, _pump.timeForFilling, "Pump");
                return true;
            }
            if (_ritual == null) return false;
            if (IsRitualPlaced(_ritual))
            {
                Say($"{HotspotManager.FormatRecipeItem(_ritual.MyItem)} placed.");
                StopRitual();
                return true;
            }
            if (!CanReach(player, _ritual.gameObject, 3f) || !_ritual.isActivated)
            {
                StopRitual();
                return false;
            }

            if (Input.GetKeyDown(KeyCode.Escape)) { StopRitual(); return true; }

            ReportHoldProgress(_ritual.time, _ritual.timeForHold, "Ritual");
            return true;
        }

        private void ReportHoldProgress(float elapsed, float duration, string label)
        {
            bool held = IsEnterHeld();
            if (_holdingRitual && !held)
            {
                Say("Released. Hold Enter to try again, or press Escape to leave.");
                _ritualProgress = -1;
            }
            _holdingRitual = held;
            if (held && duration > 0f)
            {
                int band = Mathf.Clamp(Mathf.FloorToInt(4f * elapsed / duration), 0, 4);
                if (band != _ritualProgress && band > 0 && band < 4) Say($"{label} {band * 25} percent.");
                _ritualProgress = band;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsEnterHeld()
        {
            return Application.isFocused && Time.timeScale > 0f &&
                (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter));
        }

        public bool WantsRitualHold(ItemInCourtyard item) => item == _ritual && IsEnterHeld();
        public bool WantsPumpHold(Handpump pump) => pump == _pump && pump.isNear && IsEnterHeld();
        internal static bool IsRitualPlaced(ItemInCourtyard item) => item.isCompleted || item.isItemPlaced;

        public void UpdateRecipe()
        {
            var ui = UIManager.Instance;
            if (_recipeManager == null && ui != null && ui.pauseMenu != null)
                _recipeManager = ui.pauseMenu.GetComponentInChildren<PauseMenuAndRecipeManager>(true);
            var recipe = _recipeManager != null ? _recipeManager.recipeBookCanvas : null;
            if (recipe == null || !recipe.activeInHierarchy) { _lastRecipe = null; return; }
            if (_lastRecipe == recipe) return;
            _lastRecipe = recipe;

            var entries = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var text in recipe.GetComponentsInChildren<Text>())
                AddRecipeText(text.text, seen, entries);
            foreach (var text in recipe.GetComponentsInChildren<TMP_Text>())
                AddRecipeText(text.text, seen, entries);
            if (entries.Count > 0) PrismSpeechProvider.Speak($"Recipe book. {string.Join(". ", entries)}", false);
        }

        private static void AddRecipeText(string text, HashSet<string> seen, List<string> entries)
        {
            string line = SubtitleReader.CleanText(text);
            if (!string.IsNullOrWhiteSpace(line) && seen.Add(line)) entries.Add(line);
        }

        public void UpdateTrail()
        {
            if (Time.unscaledTime < _nextTrailCheck) return;
            _nextTrailCheck = Time.unscaledTime + 0.45f;
            if (_trail == null) _trail = Object.FindObjectOfType<FootstepSmudgeStick>();
            if (_trail == null || !_trail.isActive || !_trail.isInCollisionArea ||
                _trail.percentSlider == null || !_trail.percentSlider.isActiveAndEnabled)
            {
                _lastTrailBand = -1;
                return;
            }

            int band = Mathf.RoundToInt(_trail.percentSlider.fillAmount * 10f);
            if (band == _lastTrailBand) return;
            _lastTrailBand = band;
            Say($"Footstep clue strength, {band * 10} percent.");
        }

        public void AddOptions(PlayerMovement player, List<InteractionOption> options)
        {
            AddNearby<TPScript>(player, options, spot => $"Hide, {HidingController.GetName(spot)}", spot =>
            {
                if (HidingController.TryEnter(player, spot)) Say($"Hiding. {HidingController.GetName(spot)}.");
                else Say("Move closer to the hiding spot before trying again.");
            });
            AddNearby<Suitcase>(player, options, "Inspect suitcase dials", item =>
            {
                if (item.unlocked) { Say("The suitcase is unlocked."); return; }
                WithInteract(item.suitcaseMode, item);
                if (item.IsInSuitcaseMode) _suitcase = item;
                else Say("The suitcase cannot be used here.");
            });
            AddNearby<Book>(player, options, "Inspect book", book =>
            {
                if (book.HasInteractedWithBook) { Say("The key book is already open."); return; }
                WithInteract(book.Update, book);
                Say("Opening the key book. Check nearby actions for the key.");
            });
            AddNearby<ItemDetectForWallSlide>(player, options, "Use sliding wall mechanism", mechanism =>
            {
                if (mechanism.isSlidingComplete) { Say("The sliding wall is already open."); return; }
                if (!mechanism.isNear) { Say("Move closer to the sliding wall mechanism."); return; }
                WithInteract(mechanism.itemDetect);
                if (mechanism.isSlidingComplete) Say("The sliding wall is opening.");
                else
                {
                    var item = mechanism.keyObject != null ? mechanism.keyObject.GetComponent<ItemPicker>() : null;
                    string name = item != null ? HotspotManager.CleanItemName(item) :
                        HotspotManager.FormatKnownItemName(mechanism.keyObject?.name);
                    Say(name != null ? $"The sliding wall needs {name}." : "Bring the matching item to the sliding wall mechanism.");
                }
            });
            AddNearby<StatueRotate>(player, options, statue =>
                $"Rotate statue {statue.statueNumber + 1}, orientation {statue.rotateCode + 1}", statue =>
            {
                if (statue.statueMan == null || !statue.statueMan.IsNear || statue.isRotating || statue.statueMan.Unlocked)
                {
                    Say("The statue cannot be turned now.");
                    return;
                }
                int previous = statue.rotateCode;
                WithInteract(statue.rotateStatue, statue);
                Say(statue.rotateCode != previous
                    ? $"Statue {statue.statueNumber + 1}, orientation {statue.rotateCode + 1}."
                    : $"Turning statue {statue.statueNumber + 1}.");
            });
            AddNearby<PushableBrick>(player, options, brick =>
                $"Push {DescribeBrick(brick)}", brick =>
            {
                if (brick.isPushed || brick.brickMan == null || !brick.brickMan.pushEnabled)
                {
                    Say("This brick cannot be pushed now.");
                    return;
                }
                WithInteract(brick.addForce, brick);
                Say(brick.isPushed ? $"{DescribeBrick(brick)} pushed." : "The brick did not move.");
            });
            AddNearby<GaneshMurtiManager>(player, options, "Place Ganesh idol", altar =>
            {
                if (!altar.isNear) { Say("Move closer to the Ganesh altar."); return; }
                if (altar.isPlaced) { Say("The Ganesh idol is already placed."); return; }
                WithInteract(altar.ganeshMurtiMechanism);
                Say(altar.isPlaced ? "Ganesh idol placed." : "The Ganesh idol could not be placed.");
            });
            AddNearby<ItemInCourtyard>(player, options, item =>
                $"Place {HotspotManager.FormatRecipeItem(item.MyItem)} at the ritual circle", item =>
            {
                if (IsRitualPlaced(item)) { Say("That item is already placed."); return; }
                if (!item.isActivated) { Say("Bring the matching item to its circle and move closer."); return; }
                _ritual = item;
                _holdingRitual = false;
                _ritualProgress = -1;
                Say("Hold Enter to place the item. Escape cancels.");
            });
            AddNearby<FootstepSmudgeStick>(player, options, "Check the footstep clue", trail =>
            {
                if (!trail.isActive) { Say("The footstep trail is inactive."); return; }
                if (trail.isNear) WithInteract(trail.Update);
                if (trail.percentSlider != null && trail.percentSlider.isActiveAndEnabled)
                    Say($"Footstep clue strength, {Mathf.RoundToInt(trail.percentSlider.fillAmount * 100f)} percent.");
            });
            AddNearby<FootstepKey>(player, options, "Collect the footstep key", key => WithInteract(key.Update, key));
            AddNearby<Handpump>(player, options, "Use handpump", pump =>
            {
                if (pump.hasFilledWater)
                {
                    var pot = pump.bucketPicked != null ? pump.bucketPicked.GetComponent<ItemPicker>() : null;
                    if (pot != null && pot.transform.IsChildOf(player.transform))
                    {
                        Say("You are carrying the filled water pot. Take it to the fire.");
                        return;
                    }
                    bool pickedUp = InteractionManager.TryPickUp(player, pot);
                    Say(pickedUp ? "Picked up the filled water pot. Take it to the fire." :
                        "The water pot is filled. Move closer to the pot beside the handpump to pick it up.");
                    return;
                }
                if (!pump.isNear) { Say("Move closer to the handpump."); return; }
                if (!pump.hasAttachedHandle || !pump.hasPlacedBucket)
                    WithInteract(pump.Update, pump);

                if (pump.hasAttachedHandle && pump.hasPlacedBucket && !pump.hasFilledWater)
                {
                    _pump = pump;
                    _holdingRitual = false;
                    _ritualProgress = -1;
                    Say("Water pot in place. Hold Enter to fill it. Escape cancels.");
                    return;
                }

                if (!pump.hasAttachedHandle) Say("The pump needs its handle.");
                else if (!pump.hasPlacedBucket) Say("The handle is attached. The pump needs the steel water pot.");
            });
            AddNearby<FireBox>(player, options, "Try to put out the fire", fire =>
            {
                if (!fire.isNear && !CanReach(player, fire.gameObject, 0.85f))
                {
                    Say("Move closer to the fire.");
                    return;
                }
                if (fire.hasExtinguishedFire) { Say("The fire is out."); return; }
                if (fire.handpumpRef != null && !fire.handpumpRef.hasFilledWater)
                {
                    Say($"The fire needs water. At the handpump: {DescribePump(fire.handpumpRef)}.");
                    return;
                }
                int previousPours = fire.count;
                WithInteract(fire.extinguishFire, fire);
                if (fire.hasExtinguishedFire) Say("The fire is out.");
                else if (fire.count > previousPours)
                    Say("First pour done. Refill the water pot at the handpump, then return to the fire.");
                else Say("The fire is still burning. Check that you are carrying the filled bucket.");
            });
            AddNearby<GlassShelf>(player, options, shelf => shelf.unlocked ? "Check the glass shelf" : "Try the glass shelf lock",
                shelf =>
                {
                    if (!shelf.isTouchingPlayer) { Say("Move closer to the glass shelf."); return; }
                    WithInteract(shelf.unlocked ? shelf.Update : shelf.unlockMech, shelf);
                    Say(shelf.unlocked ? "The glass shelf is unlocked." : "The glass shelf is locked.");
                });
            AddNearby<MetalLockedDoor>(player, options, "Try the metal door lock", door =>
            {
                if (!door.isTouchingPlayer) { Say("Move closer to the metal door."); return; }
                WithInteract(door.unlockMech, door);
                Say(door.unlocked ? "The metal door is unlocked." : "The metal door is locked.");
            });
            AddNearby<CourtYardMechanic>(player, options, "Begin the final ritual", court =>
            {
                if (!court.isNear || !court.isFinale)
                {
                    Say("Finish the ritual preparations and return here.");
                    return;
                }
                WithInteract(court.Update);
            });
            AddNearby<MainGateOuterArea>(player, options, gate => gate.isOpen ? "Walk through the main gate" : "Open the main gate", gate =>
            {
                if (!gate.isNear)
                {
                    Say("Move closer to the main gate.");
                    return;
                }
                if (!gate.isOpen) WithInteract(gate.gateOpen);
                if (gate.isOpen)
                {
                    AccessibilityCoordinator.Instance?.Hotspots.WalkThroughGate(player, gate);
                    Say("Walking through the main gate.");
                }
                else Say("The main gate did not open.");
            });
            AddNearby<OuterAreaDoor>(player, options, "Enter the Haveli", door =>
            {
                if (!door.isNear || door.isUsed) { Say("The entrance is unavailable from here."); return; }
                WithInteract(door.Update);
            });
            AddClues(player, options);
        }

        private static void AddClues(PlayerMovement player, List<InteractionOption> options)
        {
            foreach (var suitcase in Object.FindObjectsOfType<Suitcase>())
                AddDecals(player, options, suitcase.numberDecals, suitcase.correctCombination, "Suitcase clue", false);

            foreach (var puzzle in Object.FindObjectsOfType<StatuePuzzleManager>())
                AddDecals(player, options, puzzle.numberDecals, puzzle.correctSequence, "Statue clue", true);
        }

        private static void AddDecals(PlayerMovement player, List<InteractionOption> options,
            Il2CppReferenceArray<GameObject> decals, Il2CppStructArray<int> numbers, string title, bool orientation)
        {
            if (decals == null || numbers == null) return;
            for (int i = 0; i < Math.Min(decals.Length, numbers.Length); i++)
            {
                var decal = decals[i];
                if (decal == null || !CanReach(player, decal, 2.75f)) continue;
                if (!IsClueRevealed(decal)) continue;

                string name = $"{title} {i + 1}";
                string reading = $"{name}, {(orientation ? "orientation " : "")}{numbers[i] + (orientation ? 1 : 0)}.";
                options.Add(new InteractionOption(name, () => Say(reading), decal));
            }
        }

        public static bool IsClueRevealed(GameObject decal)
        {
            if (decal == null || !decal.activeInHierarchy) return false;
            var projector = decal.GetComponent<DecalProjector>();
            if (projector != null) return projector.isActiveAndEnabled && projector.fadeFactor > 0f;
            var renderer = decal.GetComponentInChildren<Renderer>();
            return renderer != null && renderer.enabled;
        }

        internal static bool CanReach(PlayerMovement player, GameObject target, float range)
        {
            Vector3 position = player.transform.position;
            return CanReachFrom(player, target, position,
                player.mainCam != null ? player.mainCam.transform.position : position, range);
        }

        internal static bool CanReachFrom(PlayerMovement player, GameObject target, Vector3 position, Vector3 eyes, float range)
        {
            if (target == null || !target.activeInHierarchy) return false;
            var collider = HotspotItem.FindInteractionCollider(target);
            Vector3 center = collider != null ? collider.bounds.center : target.transform.position;
            Vector3 near = collider != null ? collider.bounds.ClosestPoint(eyes) : center;
            if (Vector3.Distance(position, near) > range) return false;
            var projector = target.GetComponent<DecalProjector>();
            if (projector != null) return CanSeeProjectedClue(projector, eyes);
            if (!Physics.Linecast(eyes, near, out var hit, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
                return true;
            return hit.collider.transform.IsChildOf(target.transform) ||
                target.transform.IsChildOf(hit.collider.transform) ||
                hit.collider.transform.IsChildOf(player.transform);
        }

        private static bool CanSeeProjectedClue(DecalProjector projector, Vector3 eyes)
        {
            if (!projector.isActiveAndEnabled || projector.fadeFactor <= 0f) return false;
            Vector3 center = projector.transform.TransformPoint(projector.pivot);
            Vector3 ray = center - eyes;
            if (!Physics.Raycast(eyes, ray.normalized, out var hit, ray.magnitude + projector.size.z,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            Vector3 point = projector.transform.InverseTransformPoint(hit.point) - projector.pivot;
            Vector3 extents = projector.size * 0.5f + Vector3.one * 0.06f;
            return Mathf.Abs(point.x) <= extents.x && Mathf.Abs(point.y) <= extents.y && Mathf.Abs(point.z) <= extents.z;
        }

        private static void AddNearby<T>(PlayerMovement player, List<InteractionOption> options,
            string title, Action<T> action) where T : Behaviour
        {
            AddNearby<T>(player, options, _ => title, action);
        }

        private static void AddNearby<T>(PlayerMovement player, List<InteractionOption> options,
            Func<T, string> title, Action<T> action) where T : Behaviour
        {
            foreach (var item in Object.FindObjectsOfType<T>())
            {
                if (item.isActiveAndEnabled && CanReach(player, item.gameObject, 3f))
                    options.Add(new InteractionOption(title(item), () =>
                    {
                        if (player.CanInteract && CanReach(player, item.gameObject, 3f)) action(item);
                        else Say("That action is no longer available.");
                    }, item.gameObject));
            }
        }

        internal static void WithInteract(Action action, Lookable target = null, bool back = false)
        {
            var input = PlayerInputManager.Instance;
            if (input == null) return;
            bool interact = input.InteractTriggered;
            bool pause = input.PauseTriggered;
            bool looking = target != null && target.isRayLooking;
            try
            {
                input.InteractTriggered = !back;
                input.PauseTriggered = back;
                if (target != null) target.isRayLooking = true;
                action();
            }
            finally
            {
                input.InteractTriggered = interact;
                input.PauseTriggered = pause;
                if (target != null) target.isRayLooking = looking;
            }
        }

        public static string DescribeBrick(PushableBrick brick)
        {
            return brick.ForceDirection.ToString() switch
            {
                "FORWARD_BLUE" => "blue brick, pushes forward",
                "UP_GREEN" => "green brick, pushes up",
                "RIGHT_RED" => "red brick, pushes right",
                _ => "puzzle brick"
            };
        }

        public static string DescribePump(Handpump pump)
        {
            if (!pump.hasAttachedHandle) return "Needs the handpump handle";
            if (!pump.hasPlacedBucket) return "Needs a bucket";
            if (!pump.hasFilledWater) return "Ready to fill the bucket";
            return "Bucket filled";
        }

        private static void Say(string message) => PrismSpeechProvider.Speak(message, true);

        public void StopRitual()
        {
            _ritual = null;
            _pump = null;
            _holdingRitual = false;
        }

        public void Reset()
        {
            StopRitual();
            _suitcase = null;
            _typedDigits = "";
            _submittedDigits = null;
            _wheel = -1;
            _number = -1;
            _trail = null;
            _lastTrailBand = -1;
            _nextTrailCheck = 0f;
            _lastRecipe = null;
            _recipeManager = null;
        }
    }
}
