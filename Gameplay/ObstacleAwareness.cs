using Il2Cpp;
using KamlaAccessibility.Speech;
using UnityEngine;

namespace KamlaAccessibility.Gameplay
{
    internal sealed class ObstacleAwareness
    {
        private Collider _lastCollider;
        private bool _warnedAboutGround;
        private float _nextProbe;
        private float _nextWarning;

        public GameObject Focus { get; private set; }

        public void Update(PlayerMovement player, Vector3 blockedRoute)
        {
            if (player.Controller == null || !player.Controller.enabled) { Reset(); return; }
            var input = PlayerInputManager.Instance;
            Vector2 move = input != null ? input.MoveInput : Vector2.zero;
            Vector3 direction = blockedRoute.sqrMagnitude > 0.01f
                ? NavigationHelper.Flat(blockedRoute).normalized
                : NavigationHelper.Flat(player.transform.TransformDirection(new Vector3(move.x, 0f, move.y))).normalized;

            if (direction.sqrMagnitude < 0.01f)
            {
                if (_lastCollider == null ||
                    Vector3.Distance(player.Controller.bounds.center, _lastCollider.bounds.ClosestPoint(player.Controller.bounds.center)) > 1.6f)
                    ResetFocus();
                return;
            }
            float now = Time.unscaledTime;
            if (blockedRoute.sqrMagnitude < 0.01f && now < _nextProbe) return;
            _nextProbe = now + 0.14f;

            var hit = NavigationHelper.FindBlockingCollider(player, direction, 0.85f);
            bool unsafeGround = hit == null && player.Controller.isGrounded &&
                !NavigationHelper.CanTakeStep(player, direction);
            if (hit == null && !unsafeGround) { ResetFocus(); return; }
            if ((hit == _lastCollider && unsafeGround == _warnedAboutGround) || now < _nextWarning) return;

            _lastCollider = hit;
            _warnedAboutGround = unsafeGround;
            _nextWarning = now + 0.65f;

            var interactable = hit != null ? hit.GetComponentInParent<Lookable>() : null;
            Focus = hit != null ? (interactable != null ? interactable.gameObject : hit.gameObject) : null;
            if (hit != null && hit.gameObject.name == "Sliding Wall")
            {
                foreach (var mechanism in Object.FindObjectsOfType<ItemDetectForWallSlide>())
                {
                    if (mechanism.slidingWall != null &&
                        hit.transform.IsChildOf(mechanism.slidingWall.transform) &&
                        PuzzleAccessibility.CanReach(player, mechanism.gameObject, 3f))
                    {
                        Focus = mechanism.gameObject;
                        break;
                    }
                }
            }

            string obstruction = unsafeGround ? "No clear footing ahead" : Describe(hit);
            Vector3 right = NavigationHelper.Flat(player.transform.right);
            bool leftOpen = NavigationHelper.CanTakeStep(player, -right);
            bool rightOpen = NavigationHelper.CanTakeStep(player, right);
            string detour = leftOpen && rightOpen ? " There is space to your left and right."
                : leftOpen ? " Try a step to your left."
                : rightOpen ? " Try a step to your right."
                : " Neither side has a clear step.";
            string action = interactable != null || (Focus != null && Focus.GetComponent<ItemDetectForWallSlide>() != null)
                ? " Press Enter to check nearby actions." : "";
            PrismSpeechProvider.Speak($"{obstruction}.{action}{detour}", true);
        }

        private static string Describe(Collider hit)
        {
            if (hit.GetComponentInParent<DoorLocked>() is { } locked && locked != null &&
                !locked.isUnlocked && !locked.IsDoorUnlocked)
                return $"Blocked by {HotspotManager.CleanDoorName(locked.gameObject)}, locked";
            if (hit.GetComponentInParent<MetalLockedDoor>() is { } metal && metal != null && !metal.unlocked)
                return "Blocked by a locked metal door";
            if (hit.GetComponentInParent<MainGateOuterArea>() != null) return "Blocked by the main gate";
            if (hit.GetComponentInParent<IntroEventDoor>() is { } introDoor && introDoor != null)
                return $"Blocked by {HotspotManager.CleanDoorName(introDoor.gameObject)}" +
                    (!introDoor.IsEventComplete && !introDoor.isOpened ? ", locked from behind" : "");
            if (hit.gameObject.name == "Sliding Wall") return "Blocked by the closed sliding wall";
            if (hit.GetComponentInParent<Door>() != null || hit.GetComponentInParent<DoorScriptCode>() != null)
                return $"Blocked by {InteractionDescriber.DescribeObject(hit.gameObject) ?? "a door"}";
            if (hit.GetComponentInParent<PushableBrick>() != null) return "Blocked by a puzzle brick";
            if (hit.GetComponentInParent<OpenableAlmirah>() != null) return "Blocked by a wardrobe";
            return "Obstacle in your path";
        }

        private void ResetFocus()
        {
            Focus = null;
            _lastCollider = null;
            _warnedAboutGround = false;
        }

        public void Reset()
        {
            ResetFocus();
            _nextProbe = 0f;
            _nextWarning = 0f;
        }
    }
}
