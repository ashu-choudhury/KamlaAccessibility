using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;

namespace KamlaAccessibility.Gameplay
{
    public static class HidingController
    {
        private const float NearbyDistance = 10f;
        private const float ClearDistance = 14f;
        private const float SameFloorHeight = 1.8f;

        internal static bool IsThreatNearby(PlayerMovement player, Enemy enemy, bool alreadyHandled)
        {
            if (enemy == null || !enemy.isActiveAndEnabled) return false;
            if (enemy.IsChasing) return true;

            Vector3 offset = enemy.transform.position - player.transform.position;
            float distance = alreadyHandled ? ClearDistance : NearbyDistance;
            if (Mathf.Abs(offset.y) > SameFloorHeight || NavigationHelper.Flat(offset).sqrMagnitude > distance * distance)
                return false;

            // Nearby Kamla should only start a new retreat if there is a clear line to her on this floor.
            if (alreadyHandled) return true;
            Vector3 eyes = player.mainCam != null ? player.mainCam.transform.position : player.transform.position;
            Vector3 target = enemy.transform.position + Vector3.up * 0.65f;
            return !Physics.Linecast(eyes, target, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                   hit.collider.transform.IsChildOf(enemy.transform);
        }

        internal static bool TryLeave(PlayerMovement player)
        {
            var cover = player.TpScriptRef;
            if (cover == null || !cover.isActiveAndEnabled || cover.player != player ||
                !player.IsHiding || player.IsGettingOut || player.teleportingBack ||
                player.IsKilling || player.isPlayingVid || cover.IsHideKilling) return false;

            bool interactionWasEnabled = player.CanInteract;
            if (!interactionWasEnabled)
            {
                // Recover a stale interaction lock only after reaching this cover's native hiding point.
                if (player.TeleportPoint == null || player.TeleportPoint != cover.TpPoint ||
                    Vector3.Distance(player.transform.position, cover.TpPoint.position) > 0.55f)
                    return false;
                player.CanInteract = true;
            }
            try
            {
                // The native exit clears look state. Only scope the interact input.
                PuzzleAccessibility.WithInteract(cover.isHidingTrigger);
                return player.IsGettingOut || !player.IsHiding;
            }
            finally
            {
                if (!interactionWasEnabled && player.IsHiding && !player.IsGettingOut)
                    player.CanInteract = false;
            }
        }

        public static bool TryFindRoute(PlayerMovement player, HashSet<int> excluded,
            out TPScript destination, out List<Vector3> waypoints)
        {
            destination = null;
            waypoints = null;
            var candidates = new List<TPScript>();
            Vector3 origin = player.transform.position;
            foreach (var spot in Object.FindObjectsOfType<TPScript>())
            {
                if (spot.isActiveAndEnabled && !spot.IsHideKilling && spot.player == player &&
                    !excluded.Contains(spot.GetInstanceID()) &&
                    spot.BPoint != null && spot.TpPoint != null && Vector3.Distance(origin, spot.BPoint.position) <= 25f)
                {
                    candidates.Add(spot);
                }
            }

            candidates.Sort((left, right) => (left.BPoint.position - origin).sqrMagnitude
                .CompareTo((right.BPoint.position - origin).sqrMagnitude));

            float bestLength = float.PositiveInfinity;
            foreach (var spot in candidates)
            {
                if (Vector3.Distance(origin, spot.BPoint.position) > bestLength + 1f) break;
                if (!NavigationHelper.TryFindPath(player, spot.BPoint.position, 0f, out var path)) continue;
                if (NavigationHelper.Flat(path[path.Count - 1] - spot.BPoint.position).sqrMagnitude > 0.6f * 0.6f)
                    continue;

                float length = 0f;
                Vector3 previous = origin;
                foreach (var waypoint in path)
                {
                    length += Vector3.Distance(previous, waypoint);
                    previous = waypoint;
                }
                if (length >= bestLength) continue;

                bestLength = length;
                destination = spot;
                waypoints = path;
            }
            return destination != null;
        }

        public static bool TryEnter(PlayerMovement player, TPScript spot)
        {
            if (player.IsHiding) return player.TpScriptRef == spot;
            if (spot == null || !spot.isActiveAndEnabled || spot.IsHideKilling || !player.CanInteract) return false;

            var input = PlayerInputManager.Instance;
            if (input == null) return false;
            if (spot.IsNear && (spot.isRayLooking || PuzzleAccessibility.CanReach(player, spot.gameObject, 2f)))
            {
                PuzzleAccessibility.WithInteract(spot.isHidingTrigger, spot);
                return player.IsHiding && player.TpScriptRef == spot;
            }

            var camera = player.mainCam;
            if (camera == null) return false;
            Vector3 direction = spot.transform.position - camera.transform.position;
            Vector3 horizontal = NavigationHelper.Flat(direction);
            if (horizontal.sqrMagnitude > 0.001f)
            {
                player.transform.rotation = Quaternion.LookRotation(horizontal);
                var look = camera.GetComponent<MouseLook>();
                if (look != null)
                {
                    look.xRotation = -Mathf.Atan2(direction.y, horizontal.magnitude) * Mathf.Rad2Deg;
                    camera.transform.localRotation = Quaternion.Euler(look.xRotation, 0f, 0f);
                }
            }
            return false;
        }

        public static string GetName(TPScript spot)
        {
            string name = spot.objectName;
            if (string.IsNullOrWhiteSpace(name)) name = spot.gameObject.name;
            if (name.IndexOf("bed", System.StringComparison.OrdinalIgnoreCase) >= 0) return "Under the bed";
            if (name.IndexOf("wardrobe", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("almirah", System.StringComparison.OrdinalIgnoreCase) >= 0) return "Wardrobe";
            if (name.IndexOf("cupboard", System.StringComparison.OrdinalIgnoreCase) >= 0) return "Cupboard";
            return string.IsNullOrWhiteSpace(spot.objectName) ? "Hiding spot" :
                HotspotManager.SplitCamelCase(spot.objectName.Replace("_", " ").Trim());
        }

        internal static void ClearObstructedExit(PlayerMovement player, TPScript spot)
        {
            var controller = player.Controller;
            if (spot == null || spot.BPoint == null || controller == null || !controller.enabled) return;

            Vector3 exit = spot.BPoint.position;
            Vector3 offset = exit - player.transform.position;
            Vector3 direction = NavigationHelper.Flat(offset);
            if (offset.sqrMagnitude > 1.5f * 1.5f || direction.sqrMagnitude < 0.1f * 0.1f) return;

            var obstruction = NavigationHelper.FindBlockingCollider(player, direction, direction.magnitude);
            if (obstruction == null || !obstruction.transform.IsChildOf(spot.transform)) return;

            // Some cupboard exit animations leave the controller behind their closing door.
            // Use the game's own exit point only when the player capsule is clear there.
            var bounds = controller.bounds;
            float radius = Mathf.Max(0.1f, Mathf.Min(bounds.extents.x, bounds.extents.z) - controller.skinWidth);
            Vector3 lower = bounds.center + offset;
            Vector3 upper = lower;
            lower.y = bounds.min.y + offset.y + radius + 0.06f;
            upper.y = Mathf.Max(lower.y, bounds.max.y + offset.y - radius);
            controller.enabled = false;
            try
            {
                if (!Physics.CheckCapsule(lower, upper, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    player.transform.position = exit;
            }
            finally
            {
                controller.enabled = true;
                Physics.SyncTransforms();
            }
        }
    }
}
