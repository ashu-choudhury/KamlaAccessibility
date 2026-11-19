using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace KamlaAccessibility.Gameplay
{
    public static class NavigationHelper
    {
        private const float WaypointSpacing = 0.45f;
        private static Il2CppStructArray<RaycastHit> _capsuleHits;

        public static bool TryFindPath(PlayerMovement player, Vector3 target, float stopDistance, out List<Vector3> waypoints)
        {
            waypoints = null;
            var controller = player.Controller;
            if (controller == null || !controller.enabled) return false;

            Vector3 feet = GetFeet(controller);
            Vector3 towardTarget = Flat(target - feet);
            Vector3 approach = target - towardTarget.normalized * Mathf.Min(stopDistance, towardTarget.magnitude);

            if (TryNavMeshPath(feet, approach, target, out waypoints)) return true;

            // The Outer Area can have walkable ground without a connected NavMesh.
            if (CanWalkStraight(player, feet, approach, out Vector3 end))
            {
                waypoints = new List<Vector3> { end };
                return true;
            }

            // Try going around the side of an obstacle with one intermediate waypoint.
            Vector3 side = Vector3.Cross(Vector3.up, towardTarget.normalized);
            Vector3 midpoint = (feet + approach) * 0.5f;
            foreach (float offset in new[] { 1.5f, -1.5f, 3f, -3f })
            {
                if (CanWalkStraight(player, feet, midpoint + side * offset, out Vector3 corner) &&
                    CanWalkStraight(player, corner, approach, out end))
                {
                    waypoints = new List<Vector3> { corner, end };
                    return true;
                }
            }

            return false;
        }

        public static bool TryFindPath(PlayerMovement player, HotspotItem item, out List<Vector3> waypoints,
            Vector3? failedApproach = null, bool preferPhysicalRoute = false)
        {
            waypoints = null;
            if (player.Controller == null || !player.Controller.enabled) return false;

            // Puzzle actions may have just moved a gate, handle, or container collider.
            Physics.SyncTransforms();
            Vector3 feet = GetFeet(player.Controller);
            Vector3 target = item.Position;
            var collider = item.InteractionCollider;
            var goals = new List<Vector3>();
            Vector3 bodyOffset = player.transform.position - feet;
            Vector3 eyeOffset = (player.mainCam != null ? player.mainCam.transform.position : player.transform.position) - feet;
            Func<Vector3, bool> canReadFrom = position =>
                (!failedApproach.HasValue || Flat(position - failedApproach.Value).sqrMagnitude > 0.35f * 0.35f) &&
                IsInteractionApproach(player, item, position + bodyOffset, position + eyeOffset);
            Vector3 towardPlayer = Flat(feet - target).normalized;
            if (towardPlayer.sqrMagnitude < 0.01f) towardPlayer = -player.transform.forward;
            var approachAngles = item.IsClue ? new[] { 0, -45, 45, -90, 90, -135, 135, 180 } :
                new[] { 0, -45, 45, -90, 90, 180 };
            foreach (int angle in approachAngles)
            {
                Vector3 side = Quaternion.AngleAxis(angle, Vector3.up) * towardPlayer;
                if (collider != null && !collider.isTrigger)
                {
                    var bounds = collider.bounds;
                    float radius = bounds.extents.magnitude + 1f;
                    Vector3 edge = bounds.ClosestPoint(target + side * radius);
                    goals.Add(edge + side * 0.42f);
                    goals.Add(edge + side * 0.65f);
                }
                else
                {
                    if (collider != null && collider.isTrigger) goals.Add(target + side * 0.25f);
                    goals.Add(target + side * 0.5f);
                    goals.Add(target + side * 0.85f);
                    goals.Add(target + side * 1.35f);
                    if (item.IsClue) goals.Add(target + side * 2.1f);
                }
            }
            if (collider == null || collider.isTrigger) goals.Add(target);
            bool haveli = item.GameObject.scene.name == "Haveli";
            float retryRange = haveli ? 18f : 6f;
            bool retryNearTarget = preferPhysicalRoute ||
                (failedApproach.HasValue && Flat(target - feet).sqrMagnitude < retryRange * retryRange);
            bool allowOrdinaryDoors = haveli;

            foreach (var goal in goals)
            {
                if (CanWalkStraight(player, feet, goal, out Vector3 end) && IsSameLevel(end, target) && canReadFrom(end))
                {
                    waypoints = new List<Vector3> { end };
                    return true;
                }
                if (!retryNearTarget && TryNavMeshPath(feet, goal, target, out waypoints) &&
                    canReadFrom(waypoints[waypoints.Count - 1])) return true;
            }

            if (!retryNearTarget && TryHaveliStairs(player, feet, target, goals, out waypoints, canReadFrom)) return true;

            // Narrow gaps and broad slopes can give different results on different sample grids.
            if (TryLocalDetour(player, feet, target, goals, 0.9f, 2200, true, out waypoints, allowOrdinaryDoors, canReadFrom) ||
                TryLocalDetour(player, feet, target, goals, 1.25f, 750, false, out waypoints, allowOrdinaryDoors, canReadFrom))
                return true;

            // A player standing close to a fixture may be between the regular grid's sample points.
            // A blocked forward step is a reason to try a different starting point.
            Vector3 sideStep = Vector3.Cross(Vector3.up, towardPlayer);
            foreach (var direction in new[] { -towardPlayer, sideStep, -sideStep, towardPlayer })
            {
                if (!CanWalkStraight(player, feet, feet + direction * 0.55f, out Vector3 seed) ||
                    !TryLocalDetour(player, seed, target, goals, 0.75f, 950, true, out waypoints,
                        allowOrdinaryDoors, canReadFrom)) continue;
                waypoints.Insert(0, seed);
                return true;
            }
            return haveli && TryReconnectToNavMesh(player, feet, target, goals, canReadFrom, out waypoints);
        }

        private static bool TryReconnectToNavMesh(PlayerMovement player, Vector3 feet, Vector3 target,
            List<Vector3> goals, Func<Vector3, bool> acceptEnd, out List<Vector3> route)
        {
            route = null;
            // Respawn and narrow furniture can leave the controller outside the baked walking area.
            // Only reconnect along a segment the current character capsule can actually walk.
            foreach (float distance in new[] { 0.8f, 1.5f, 2.4f, 3.8f, 5.6f, 8f })
            {
                for (int angle = 0; angle < 360; angle += 45)
                {
                    Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
                    Vector3 probe = feet + direction * distance;
                    if (!NavMesh.SamplePosition(probe, out var landing, 0.7f, NavMesh.AllAreas) ||
                        Flat(landing.position - feet).sqrMagnitude < 0.5f * 0.5f ||
                        Mathf.Abs(feet.y - landing.position.y) > 0.7f) continue;

                    List<Vector3> approach = null;
                    foreach (var goal in goals)
                    {
                        if (!TryNavMeshPath(landing.position, goal, target, out route) || !acceptEnd(route[route.Count - 1])) continue;
                        if (approach == null)
                        {
                            if (CanWalkStraight(player, feet, landing.position, out var direct) &&
                                Mathf.Abs(direct.y - landing.position.y) <= 0.35f)
                                approach = new List<Vector3> { direct };
                            else if (distance <= 2.4f ||
                                !TryLocalDetour(player, feet, landing.position, new List<Vector3> { landing.position },
                                    0.55f, 270, true, out approach, allowOrdinaryDoors: true,
                                    acceptEnd: end => Mathf.Abs(end.y - landing.position.y) <= 0.35f))
                                break;
                        }

                        Vector3 step = approach[approach.Count - 1];
                        int first = route.FindIndex(point => Flat(point - step).sqrMagnitude > 0.3f * 0.3f);
                        if (first >= 0)
                        {
                            Vector3 firstStep = step + Vector3.ClampMagnitude(Flat(route[first] - step), 0.7f);
                            if (!CanWalkStraight(player, step, firstStep, out _, allowOrdinaryDoors: true)) continue;
                        }
                        route.InsertRange(0, approach);
                        return true;
                    }
                }
            }
            route = null;
            return false;
        }

        internal static bool IsAtInteraction(PlayerMovement player, HotspotItem item)
        {
            var target = item.GameObject;
            if (target.GetComponent<ColliderForCryingEvent>() is { } encounter && encounter != null)
                return encounter.cryingKamlaMan != null && encounter.cryingKamlaMan.ActivateEvent;
            if (target.GetComponent<CourtYardMechanic>() is { } altar && altar != null)
                return altar.isNear && PuzzleAccessibility.CanReach(player, target, 3f);
            if (target.GetComponent<FootstepSmudgeStick>() is { } trail && trail != null)
                return trail.isActive && trail.isNear && PuzzleAccessibility.CanReach(player, target, 3f);
            Vector3 position = player.transform.position;
            Vector3 eyes = player.mainCam != null ? player.mainCam.transform.position : position;
            if (!IsInteractionApproach(player, item, position, eyes)) return false;
            if (target.GetComponent<OuterAreaDoor>() is { } entrance && entrance != null) return entrance.isNear;
            if (target.GetComponent<MainGateOuterArea>() is { } gate && gate != null)
                return gate.isNear;
            if (target.GetComponent<DoorScriptCode>() is { } door && door != null) return door.playerEnter;
            if (target.GetComponent<Book>() is { } book && book != null) return book.IsTouchingPlayer;
            if (target.GetComponent<Handpump>() is { } pump && pump != null) return pump.isNear;
            if (target.GetComponent<GlassShelf>() is { } cabinet && cabinet != null) return cabinet.isTouchingPlayer;
            if (target.GetComponent<GaneshMurtiManager>() is { } ganesh && ganesh != null) return ganesh.isNear;
            if (target.GetComponent<ItemDetectForWallSlide>() is { } wall && wall != null) return wall.isNear;
            if (target.GetComponent<MetalLockedDoor>() is { } metalDoor && metalDoor != null) return metalDoor.isTouchingPlayer;
            if (target.GetComponent<TPScript>() is { } cover && cover != null) return cover.IsNear;
            return true;
        }

        private static bool IsInteractionApproach(PlayerMovement player, HotspotItem item, Vector3 position, Vector3 eyes)
        {
            if (item.IsClue) return PuzzleAccessibility.CanReachFrom(player, item.GameObject, position, eyes, 2.75f);
            if (item.GameObject.GetComponent<ColliderForCryingEvent>() != null && item.InteractionCollider != null)
                return item.InteractionCollider.bounds.Contains(position);
            if (item.ApproachPoint != null && item.GameObject.GetComponent<FootstepSmudgeStick>() != null)
                return Flat(item.Position - position).sqrMagnitude <= 0.7f * 0.7f &&
                       Mathf.Abs(item.Position.y - position.y) < 2.1f;
            Vector3 point = item.InteractionCollider != null && !item.InteractionCollider.isTrigger
                ? item.InteractionCollider.bounds.ClosestPoint(position) : item.Position;
            float distance = Flat(point - position).magnitude;
            float limit = item.GameObject.GetComponent<ItemPicker>() != null || item.GameObject.GetComponent<Key>() != null
                ? 1.05f : 1.25f;
            return distance <= limit && PuzzleAccessibility.CanReachFrom(player, item.GameObject, position, eyes, 2.75f);
        }

        internal static Vector3 GetFeet(CharacterController controller)
        {
            var bounds = controller.bounds;
            return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        internal static bool TryFindCoverDetour(PlayerMovement player, Vector3 entrance, out List<Vector3> route)
        {
            Vector3 feet = GetFeet(player.Controller);
            if (CanWalkStraight(player, feet, entrance, out var end) && IsSameLevel(end, entrance))
            {
                route = new List<Vector3> { end };
                return true;
            }
            return TryLocalDetour(player, feet, entrance, new List<Vector3> { entrance }, 0.55f, 550,
                true, out route, allowOrdinaryDoors: true);
        }

        internal static bool TryBypassRouteObstruction(PlayerMovement player, Vector3 direction,
            List<Vector3> route, int nextWaypoint, out List<Vector3> bypass)
        {
            bypass = null;
            var obstruction = FindBlockingCollider(player, direction, 1.2f);
            if (obstruction == null) return false;

            Vector3 feet = GetFeet(player.Controller);
            for (int i = nextWaypoint; i < route.Count; i++)
            {
                Vector3 rejoin = route[i];
                Vector3 offset = Flat(rejoin - feet);
                if (Mathf.Abs(rejoin.y - feet.y) > 0.8f) break;
                bool rejoinOnSegment = i == nextWaypoint && offset.sqrMagnitude > 16f;
                if (rejoinOnSegment)
                {
                    rejoin = Vector3.Lerp(feet, rejoin, 3f / offset.magnitude);
                    offset = Flat(rejoin - feet);
                }
                if (offset.sqrMagnitude > 16f) break;
                if (offset.sqrMagnitude < 0.65f * 0.65f || Vector3.Dot(offset, direction) <= 0f) continue;
                if (!TryLocalDetour(player, feet, rejoin, new List<Vector3> { rejoin }, 0.38f, 360, true,
                        out bypass, acceptEnd: end => Mathf.Abs(end.y - rejoin.y) <= 0.45f)) continue;
                for (int j = rejoinOnSegment ? i : i + 1; j < route.Count; j++) bypass.Add(route[j]);
                MelonLoader.MelonLogger.Msg($"[Navigation] Found a local detour around {obstruction.name}.");
                return true;
            }
            return false;
        }

        private static bool IsSameLevel(Vector3 floor, Vector3 target) =>
            target.y - floor.y >= -0.65f && target.y - floor.y <= 2.75f;

        private static bool TryHaveliStairs(PlayerMovement player, Vector3 feet, Vector3 target,
            List<Vector3> goals, out List<Vector3> waypoints, Func<Vector3, bool> acceptEnd)
        {
            waypoints = null;
            if (!NavMesh.SamplePosition(target, out NavMeshHit targetFloor, 2.1f, NavMesh.AllAreas) ||
                Mathf.Abs(feet.y - targetFloor.position.y) < 1.25f)
                return false;

            foreach (var ramp in Object.FindObjectsOfType<MeshCollider>())
            {
                if (ramp.name != "Quad for Stairs" || !ramp.enabled) continue;
                var bounds = ramp.bounds;
                Vector3 axis = bounds.size.x >= bounds.size.z ? Vector3.right : Vector3.forward;
                float radius = Vector3.Dot(bounds.extents, axis);
                Vector3 above = new Vector3(bounds.center.x, bounds.max.y + 0.5f, bounds.center.z);
                if (!ramp.Raycast(new Ray(above - axis * (radius * 0.8f), Vector3.down), out var first, bounds.size.y + 1f) ||
                    !ramp.Raycast(new Ray(above + axis * (radius * 0.8f), Vector3.down), out var second, bounds.size.y + 1f) ||
                    !IsWalkableFloor(player, first) || !IsWalkableFloor(player, second))
                    continue;

                bool startAtFirst = Mathf.Abs(feet.y - first.point.y) < Mathf.Abs(feet.y - second.point.y);
                Vector3 endDirection = startAtFirst ? axis : -axis;
                Vector3 firstStep = startAtFirst ? first.point : second.point;
                Vector3 lastStep = startAtFirst ? second.point : first.point;
                Vector3 entrance = firstStep - endDirection * 0.82f;
                Vector3 exit = lastStep + endDirection * 0.82f;
                if (!TryReachStairLanding(player, feet, entrance, out var toStairs) ||
                    !NavMesh.SamplePosition(exit, out NavMeshHit exitFloor, 0.85f, NavMesh.AllAreas))
                    continue;

                foreach (var goal in goals)
                {
                    if (!TryNavMeshPath(exitFloor.position, goal, target, out var fromStairs) ||
                        (acceptEnd != null && !acceptEnd(fromStairs[fromStairs.Count - 1]))) continue;
                    // The mesh islands stop at the lip of the game's continuous stair ramp.
                    toStairs.Add(firstStep);
                    toStairs.Add(lastStep);
                    toStairs.AddRange(fromStairs);
                    waypoints = toStairs;
                    return true;
                }
            }
            return false;
        }

        private static bool TryReachStairLanding(PlayerMovement player, Vector3 feet, Vector3 landing,
            out List<Vector3> route)
        {
            if (!TryNavMeshPath(feet, landing, landing, out route, allowPartial: true)) return false;
            Vector3 last = route[route.Count - 1];
            if (Vector3.Distance(last, landing) <= 0.85f) return true;
            if (Vector3.Distance(last, landing) > 5f ||
                !TryLocalDetour(player, last, landing, new List<Vector3> { landing },
                    0.55f, 350, true, out var bridge, allowOrdinaryDoors: true))
                return false;
            route.AddRange(bridge);
            return true;
        }

        private sealed class RouteNode
        {
            public Vector3 Point;
            public RouteNode Previous;
            public float Distance;
        }

        private static bool TryLocalDetour(PlayerMovement player, Vector3 feet, Vector3 target,
            List<Vector3> goals, float spacing, int nodeLimit, bool followSlope, out List<Vector3> waypoints,
            bool allowOrdinaryDoors = false, Func<Vector3, bool> acceptEnd = null)
        {
            waypoints = null;
            var open = new PriorityQueue<RouteNode, float>();
            var visited = new HashSet<(int X, int Y, int Z)>();
            open.Enqueue(new RouteNode { Point = feet }, 0f);
            var directions = new[]
            {
                Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
                Vector3.forward + Vector3.right, Vector3.forward + Vector3.left,
                Vector3.back + Vector3.right, Vector3.back + Vector3.left
            };
            float directDistance = Flat(target - feet).magnitude;
            float maxDistance = Mathf.Min(150f, directDistance + 55f);
            int expanded = 0;
            while (expanded < nodeLimit && open.TryDequeue(out var current, out _))
            {
                Vector3 delta = current.Point - feet;
                if (!visited.Add((Mathf.RoundToInt(delta.x / spacing), Mathf.RoundToInt(delta.y / 0.8f),
                        Mathf.RoundToInt(delta.z / spacing)))) continue;
                expanded++;

                float remaining = Flat(current.Point - target).magnitude;
                if (remaining <= 4.5f)
                {
                    foreach (var goal in goals)
                    {
                        if (!CanWalkStraight(player, current.Point, goal, out Vector3 end, followSlope, allowOrdinaryDoors) ||
                            !IsSameLevel(end, target) || (acceptEnd != null && !acceptEnd(end))) continue;
                        waypoints = new List<Vector3> { end };
                        for (var node = current; node.Previous != null; node = node.Previous) waypoints.Add(node.Point);
                        waypoints.Reverse();
                        return true;
                    }
                }

                foreach (var direction in directions)
                {
                    Vector3 next = current.Point + direction * spacing;
                    float cost = current.Distance + spacing * direction.magnitude;
                    float estimate = Flat(next - target).magnitude;
                    if (cost + estimate > maxDistance ||
                        !CanWalkStraight(player, current.Point, next, out Vector3 end, followSlope, allowOrdinaryDoors)) continue;
                    open.Enqueue(new RouteNode { Point = end, Previous = current, Distance = cost }, cost + estimate);
                }
            }
            return false;
        }

        private static bool TryNavMeshPath(Vector3 feet, Vector3 approach, Vector3 target, out List<Vector3> waypoints,
            bool allowPartial = false)
        {
            waypoints = null;
            if (!NavMesh.SamplePosition(feet, out NavMeshHit start, 1.25f, NavMesh.AllAreas) ||
                Mathf.Abs(start.position.y - feet.y) > 0.7f ||
                !NavMesh.SamplePosition(approach, out NavMeshHit end, 2.1f, NavMesh.AllAreas) ||
                !IsSameLevel(end.position, target) ||
                Flat(end.position - target).sqrMagnitude > 2.2f * 2.2f)
            {
                return false;
            }

            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) ||
                (path.status != NavMeshPathStatus.PathComplete &&
                 !(allowPartial && path.status == NavMeshPathStatus.PathPartial)))
            {
                return false;
            }

            var corners = path.corners;
            if (corners == null || corners.Length == 0) return false;

            var route = new List<Vector3>(corners.Length);
            for (int i = 0; i < corners.Length; i++) route.Add(corners[i]);
            if (IsPathBlockedByLockedDoor(route, target)) return false;

            waypoints = route;
            return true;
        }

        private static bool CanWalkStraight(PlayerMovement player, Vector3 from, Vector3 toward, out Vector3 end,
            bool followSlope = true, bool allowOrdinaryDoors = false)
        {
            end = from;
            var controller = player.Controller;
            var bounds = controller.bounds;
            float radius = Mathf.Max(0.1f, Mathf.Min(bounds.extents.x, bounds.extents.z) - controller.skinWidth);
            float step = Mathf.Clamp(controller.stepOffset, 0.12f, 0.45f);
            int groundMask = player.GroundMask.value;
            float distance = Flat(toward - from).magnitude;
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / (followSlope ? WaypointSpacing : 0.65f)));
            float climb = followSlope ? Mathf.Max(step, distance / steps *
                Mathf.Tan(Mathf.Min(55f, controller.slopeLimit) * Mathf.Deg2Rad)) : step;

            for (int i = 1; i <= steps; i++)
            {
                Vector3 sample = Vector3.Lerp(from, toward, (float)i / steps);
                var above = new Vector3(sample.x, end.y + climb + (followSlope ? 0.08f : 0.12f), sample.z);
                if (!TryFindFloor(player, above, climb + 0.62f, groundMask, out RaycastHit floor))
                {
                    return false;
                }

                Vector3 delta = floor.point - end;
                if (delta.sqrMagnitude > 0.001f)
                {
                    float baseHeight = Mathf.Max(0f, followSlope ? delta.y : -delta.y);
                    float head = Mathf.Max(step + radius, bounds.size.y - radius);
                    var low = end + Vector3.up * (baseHeight + step + radius);
                    var high = end + Vector3.up * (baseHeight + head);
                    Vector3 horizontal = Flat(delta);
                    if (FindCapsuleObstruction(player, low, high, radius, horizontal, horizontal.magnitude, allowOrdinaryDoors) != null)
                        return false;
                }

                end = floor.point;
            }
            return true;
        }

        private static bool TryFindFloor(PlayerMovement player, Vector3 origin, float depth, int groundMask,
            out RaycastHit floor)
        {
            if (groundMask != 0 && Physics.Raycast(origin, Vector3.down, out floor, depth, groundMask,
                    QueryTriggerInteraction.Ignore) && IsWalkableFloor(player, floor)) return true;
            return Physics.Raycast(origin, Vector3.down, out floor, depth, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore) && IsWalkableFloor(player, floor);
        }

        // The Outer Area's terrain supports the character even when the global layer matrix excludes it.
        private static bool IsWalkableFloor(PlayerMovement player, RaycastHit floor) =>
            floor.collider != null && !floor.collider.transform.IsChildOf(player.transform) &&
            Vector3.Angle(floor.normal, Vector3.up) <= player.Controller.slopeLimit;

        private static bool IsPathBlockedByLockedDoor(List<Vector3> waypoints, Vector3 target)
        {
            foreach (var door in Object.FindObjectsOfType<DoorLocked>())
            {
                if (!door.isUnlocked && !door.IsDoorUnlocked && DoesPathCrossDoor(waypoints, target, door.transform.position))
                    return true;
            }
            foreach (var door in Object.FindObjectsOfType<MetalLockedDoor>())
            {
                if (!door.unlocked && DoesPathCrossDoor(waypoints, target, door.transform.position))
                    return true;
            }
            foreach (var door in Object.FindObjectsOfType<IntroEventDoor>())
            {
                if (!door.IsEventComplete && !door.isOpened && DoesPathCrossDoor(waypoints, target, door.transform.position))
                    return true;
            }
            return false;
        }

        private static bool DoesPathCrossDoor(List<Vector3> waypoints, Vector3 target, Vector3 door)
        {
            if (Vector3.Distance(door, target) <= 1.5f) return false;

            for (int i = 1; i < waypoints.Count; i++)
            {
                Vector3 start = waypoints[i - 1];
                Vector3 delta = waypoints[i] - start;
                Vector3 horizontal = Flat(delta);
                if (horizontal.sqrMagnitude < 0.0001f) continue;

                float along = Vector3.Dot(Flat(door - start), horizontal) / horizontal.sqrMagnitude;
                if (along < 0f || along > 1f || (along == 1f && i == waypoints.Count - 1)) continue;
                Vector3 closest = start + delta * along;
                if (Mathf.Abs(door.y - closest.y) <= 1.6f && Flat(door - closest).sqrMagnitude < 0.9f * 0.9f)
                    return true;
            }
            return false;
        }

        public static bool TryOpenBlockingDoor(PlayerMovement player, Vector3 direction)
        {
            var nearest = FindBlockingCollider(player, direction, 1.6f);
            var gate = nearest != null ? nearest.GetComponentInParent<MainGateOuterArea>() : null;
            if (gate != null && gate.isNear && !gate.isOpen)
            {
                PuzzleAccessibility.WithInteract(gate.gateOpen);
                return gate.isOpen;
            }
            var scriptDoor = nearest != null ? nearest.GetComponentInParent<DoorScriptCode>() : null;
            if (scriptDoor == null)
                scriptDoor = FindDoorInStep(player, direction, nearest);
            if (scriptDoor != null && scriptDoor.isActiveAndEnabled && !scriptDoor.open && !scriptDoor.isDoorOpening)
            {
                return TryToggleDoor(scriptDoor);
            }
            if (nearest == null) return false;
            var door = nearest.GetComponentInParent<Door>();
            if (door != null && door.isActiveAndEnabled && !door.isOpen && !door.isOpening)
            {
                return TryToggleDoor(door);
            }
            return false;
        }

        internal static bool TryToggleDoor(DoorScriptCode door)
        {
            bool wasOpen = door.open;
            PuzzleAccessibility.WithInteract(door.playerDoorOpen);
            return door.open != wasOpen;
        }

        internal static bool TryToggleDoor(Door door)
        {
            bool wasOpen = door.isOpen;
            PuzzleAccessibility.WithInteract(door.playerDoorOpen);
            return door.isOpen != wasOpen || door.isOpening;
        }

        private static DoorScriptCode FindDoorInStep(PlayerMovement player, Vector3 direction, Collider obstruction)
        {
            var feet = GetFeet(player.Controller);
            DoorScriptCode closest = null;
            float closestDistance = 1.8f;
            foreach (var door in Object.FindObjectsOfType<DoorScriptCode>())
            {
                if (!door.isActiveAndEnabled || door.open || door.isDoorOpening) continue;
                if (obstruction != null && (IsPartOfDoor(obstruction, door.leftDoor) || IsPartOfDoor(obstruction, door.rightDoor)))
                    return door;

                Vector3 offset = door.transform.position - feet;
                float distance = Flat(offset).magnitude;
                if (!door.playerEnter || distance >= closestDistance || Mathf.Abs(offset.y) > 2f ||
                    Vector3.Dot(Flat(offset), direction) < -0.35f) continue;
                closest = door;
                closestDistance = distance;
            }
            return closest;
        }

        private static bool IsPartOfDoor(Collider obstruction, GameObject door) => door != null &&
            obstruction.transform.IsChildOf(door.transform);

        public static Collider FindBlockingCollider(PlayerMovement player, Vector3 direction, float distance)
        {
            var controller = player.Controller;
            if (controller == null || !controller.enabled || direction.sqrMagnitude < 0.001f) return null;

            var bounds = controller.bounds;
            float radius = Mathf.Clamp(Mathf.Min(bounds.extents.x, bounds.extents.z) - controller.skinWidth, 0.1f, 0.4f);
            Vector3 low = bounds.center;
            low.y = bounds.min.y + Mathf.Max(0.15f, controller.stepOffset) + radius;
            Vector3 high = bounds.center;
            high.y = Mathf.Max(low.y, bounds.max.y - radius);
            return FindCapsuleObstruction(player, low, high, radius, direction, distance);
        }

        private static Collider FindCapsuleObstruction(PlayerMovement player, Vector3 low, Vector3 high,
            float radius, Vector3 direction, float distance, bool allowOrdinaryDoors = false)
        {
            if (distance <= 0.001f) return null;
            // CapsuleCastAll throws in the installed game. This overload calls the available native sweep.
            _capsuleHits ??= new Il2CppStructArray<RaycastHit>(32);
            int count = Physics.CapsuleCastNonAlloc(low, high, radius, direction.normalized, _capsuleHits,
                distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == _capsuleHits.Length && _capsuleHits.Length < 128)
            {
                _capsuleHits = new Il2CppStructArray<RaycastHit>(128);
                count = Physics.CapsuleCastNonAlloc(low, high, radius, direction.normalized, _capsuleHits,
                    distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            }
            Collider nearest = null;
            float closest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = _capsuleHits[i];
                var collider = hit.collider;
                if (collider == null || collider.transform.IsChildOf(player.transform) || hit.distance >= closest)
                {
                    continue;
                }
                if (allowOrdinaryDoors && IsClosedOrdinaryDoor(collider)) continue;
                closest = hit.distance;
                nearest = collider;
            }
            return nearest;
        }

        private static bool IsClosedOrdinaryDoor(Collider collider)
        {
            var scriptDoor = collider.GetComponentInParent<DoorScriptCode>();
            if (scriptDoor != null) return scriptDoor.isActiveAndEnabled && !scriptDoor.open && !scriptDoor.isDoorOpening;
            var door = collider.GetComponentInParent<Door>();
            return door != null && door.isActiveAndEnabled && !door.isOpen && !door.isOpening;
        }

        public static bool CanTakeStep(PlayerMovement player, Vector3 direction)
        {
            var bounds = player.Controller.bounds;
            var from = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            return CanWalkStraight(player, from, from + Flat(direction).normalized * 0.75f, out _);
        }

        public static Vector3 Flat(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
