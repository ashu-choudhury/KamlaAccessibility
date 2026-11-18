using HarmonyLib;
using Il2Cpp;
using KamlaAccessibility.Core;

namespace KamlaAccessibility.Patches
{
    internal struct HeldPuzzleInput
    {
        private PlayerInputManager _input;
        private float _previous;
        private Lookable _focus;
        private bool _wasLooking;

        public static HeldPuzzleInput Begin(Lookable focus = null)
        {
            var input = PlayerInputManager.Instance;
            if (input == null) return default;
            var state = new HeldPuzzleInput
            {
                _input = input,
                _previous = input.PlaceItemTriggered,
                _focus = focus,
                _wasLooking = focus != null && focus.isRayLooking
            };
            input.PlaceItemTriggered = 1f;
            if (focus != null) focus.isRayLooking = true;
            return state;
        }

        public void Restore()
        {
            if (_focus != null) _focus.isRayLooking = _wasLooking;
            if (_input != null) _input.PlaceItemTriggered = _previous;
        }
    }

    [HarmonyPatch(typeof(ItemInCourtyard), nameof(ItemInCourtyard.Update))]
    internal static class RitualInputPatch
    {
        private static void Prefix(ItemInCourtyard __instance, out HeldPuzzleInput __state)
        {
            __state = AccessibilityCoordinator.Instance?.Puzzles.WantsRitualHold(__instance) == true
                ? HeldPuzzleInput.Begin(__instance.placeCircle) : default;
        }

        private static void Postfix(HeldPuzzleInput __state) => __state.Restore();

        private static void Finalizer(System.Exception __exception, HeldPuzzleInput __state)
        {
            if (__exception != null) __state.Restore();
        }
    }

    [HarmonyPatch(typeof(Handpump), nameof(Handpump.Update))]
    internal static class HandpumpInputPatch
    {
        private static void Prefix(Handpump __instance, out HeldPuzzleInput __state)
        {
            __state = AccessibilityCoordinator.Instance?.Puzzles.WantsPumpHold(__instance) == true
                ? HeldPuzzleInput.Begin(__instance) : default;
        }

        private static void Postfix(HeldPuzzleInput __state) => __state.Restore();

        private static void Finalizer(System.Exception __exception, HeldPuzzleInput __state)
        {
            if (__exception != null) __state.Restore();
        }
    }
}
