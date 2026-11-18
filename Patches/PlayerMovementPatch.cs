using HarmonyLib;
using Il2Cpp;
using KamlaAccessibility.Core;
using MelonLoader;
using UnityEngine;

namespace KamlaAccessibility.Patches
{
    [HarmonyPatch(typeof(PlayerMovement), nameof(PlayerMovement.Update))]
    internal static class PlayerMovementPatch
    {
        private static bool _reportedFirstInput;

        private struct InputState
        {
            public PlayerInputManager Input;
            public Vector2 Move;
            public bool Sprint;

            public void Restore()
            {
                if (Input == null) return;
                Input.MoveInput = Move;
                Input.SprintTriggered = Sprint;
            }
        }

        private static void Prefix(PlayerMovement __instance, out InputState __state)
        {
            __state = default;
            var input = PlayerInputManager.Instance;
            var router = AccessibilityCoordinator.Instance?.Hotspots;
            if (input == null || input.MoveInput.sqrMagnitude > 0.04f || router == null ||
                !router.TryGetMovement(__instance, out var direction, out bool sprint))
            {
                return;
            }

            __state = new InputState { Input = input, Move = input.MoveInput, Sprint = input.SprintTriggered };
            input.MoveInput = direction;
            input.SprintTriggered = sprint;
            if (!_reportedFirstInput && direction.sqrMagnitude > 0f)
            {
                _reportedFirstInput = true;
                MelonLogger.Msg("[Navigation] Native player movement received auto-walk input.");
            }
        }

        private static void Postfix(InputState __state)
        {
            __state.Restore();
        }

        private static void Finalizer(System.Exception __exception, InputState __state)
        {
            if (__exception != null) __state.Restore();
        }
    }
}
