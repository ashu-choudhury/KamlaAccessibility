using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Il2Cpp;

namespace KamlaAccessibility.Gameplay
{
    /// <summary>
    /// Provides NavMesh-based reachability validation and pathfinding waypoint calculation.
    /// Ensures only physically walkable hotspots are displayed and navigated.
    /// </summary>
    public static class NavigationHelper
    {
        private const float SampleRadius = 3.5f;
        private const float PlayerSampleRadius = 2.5f;

        /// <summary>
        /// Validates whether a target position can be physically reached by walking from playerPos.
        /// </summary>
        public static bool IsReachable(Vector3 playerPos, Vector3 targetPos, out List<Vector3> waypoints)
        {
            waypoints = new List<Vector3>();

            // 1. Sample player position on NavMesh
            if (!NavMesh.SamplePosition(playerPos, out NavMeshHit playerHit, PlayerSampleRadius, NavMesh.AllAreas))
            {
                // Try fallback with slightly larger radius
                if (!NavMesh.SamplePosition(playerPos, out playerHit, 5.0f, NavMesh.AllAreas))
                {
                    return false;
                }
            }

            // 2. Sample target position on NavMesh (accounts for items on tables, shelves, altars)
            if (!NavMesh.SamplePosition(targetPos, out NavMeshHit targetHit, SampleRadius, NavMesh.AllAreas))
            {
                return false;
            }

            // 3. Calculate path
            var path = new NavMeshPath();
            bool pathCalculated = NavMesh.CalculatePath(playerHit.position, targetHit.position, NavMesh.AllAreas, path);
            if (!pathCalculated || path.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }

            // Extract corners
            var corners = path.corners;
            if (corners == null || corners.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < corners.Length; i++)
            {
                waypoints.Add(corners[i]);
            }

            // 4. Verify path is not blocked by a locked door beyond reach
            if (IsPathBlockedByLockedDoor(waypoints, targetPos))
            {
                waypoints.Clear();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Quick overload for reachability check without needing waypoints.
        /// </summary>
        public static bool IsReachable(Vector3 playerPos, Vector3 targetPos)
        {
            return IsReachable(playerPos, targetPos, out _);
        }

        /// <summary>
        /// Checks if the calculated path passes through an active locked door before reaching the target.
        /// If the target is the locked door itself, it is reachable from the player's side.
        /// </summary>
        private static bool IsPathBlockedByLockedDoor(List<Vector3> waypoints, Vector3 targetPos)
        {
            try
            {
                // Check DoorLocked
                var lockedDoors = UnityEngine.Object.FindObjectsOfType<DoorLocked>();
                foreach (var ld in lockedDoors)
                {
                    if (ld != null && ld.gameObject.activeInHierarchy)
                    {
                        bool isUnlocked = ld.isUnlocked || ld.IsDoorUnlocked;
                        if (!isUnlocked)
                        {
                            Vector3 doorPos = ld.transform.position;
                            if (DoesPathCrossDoor(waypoints, targetPos, doorPos))
                            {
                                return true;
                            }
                        }
                    }
                }

                // Check MetalLockedDoor
                var metalLockedDoors = UnityEngine.Object.FindObjectsOfType<MetalLockedDoor>();
                foreach (var mld in metalLockedDoors)
                {
                    if (mld != null && mld.gameObject.activeInHierarchy)
                    {
                        Vector3 doorPos = mld.transform.position;
                        if (DoesPathCrossDoor(waypoints, targetPos, doorPos))
                        {
                            return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        private static bool DoesPathCrossDoor(List<Vector3> waypoints, Vector3 targetPos, Vector3 doorPos)
        {
            // If the target itself is the door or right next to it (< 1.5m), it's reachable
            float distToTarget = Vector3.Distance(doorPos, targetPos);
            if (distToTarget <= 1.5f) return false;

            // Check if any waypoint or segment along the path passes through the door threshold
            float doorThresholdDist = 1.2f;
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                Vector3 p1 = waypoints[i];
                Vector3 p2 = waypoints[i + 1];

                // Check distance from door position to segment p1-p2
                float d = DistancePointToSegment(doorPos, p1, p2);
                if (d <= doorThresholdDist)
                {
                    return true;
                }
            }

            return false;
        }

        private static float DistancePointToSegment(Vector3 point, Vector3 segA, Vector3 segB)
        {
            point.y = 0;
            segA.y = 0;
            segB.y = 0;

            Vector3 seg = segB - segA;
            float segLenSqr = seg.sqrMagnitude;
            if (segLenSqr < 0.0001f)
            {
                return Vector3.Distance(point, segA);
            }

            float t = Mathf.Clamp01(Vector3.Dot(point - segA, seg) / segLenSqr);
            Vector3 proj = segA + t * seg;
            return Vector3.Distance(point, proj);
        }

        /// <summary>
        /// Checks for any closed unlocked door near the player or along the path and opens it.
        /// </summary>
        public static void CheckAndOpenDoorsNearPlayer(Vector3 playerPos, float radius = 2.2f)
        {
            try
            {
                var scriptDoors = UnityEngine.Object.FindObjectsOfType<DoorScriptCode>();
                foreach (var dsc in scriptDoors)
                {
                    if (dsc != null && dsc.gameObject.activeInHierarchy && !dsc.open)
                    {
                        if (Vector3.Distance(playerPos, dsc.transform.position) <= radius)
                        {
                            dsc.playerDoorOpen();
                        }
                    }
                }

                var doors = UnityEngine.Object.FindObjectsOfType<Door>();
                foreach (var door in doors)
                {
                    if (door != null && door.gameObject.activeInHierarchy && !door.isOpen)
                    {
                        if (Vector3.Distance(playerPos, door.transform.position) <= radius)
                        {
                            door.playerDoorOpen();
                        }
                    }
                }
            }
            catch { }
        }
    }
}
