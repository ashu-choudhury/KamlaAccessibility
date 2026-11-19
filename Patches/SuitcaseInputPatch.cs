using HarmonyLib;
using Il2Cpp;
using KamlaAccessibility.Core;

namespace KamlaAccessibility.Patches
{
    [HarmonyPatch(typeof(Suitcase), nameof(Suitcase.checkResults))]
    internal static class SuitcaseInputPatch
    {
        private static bool Prefix(Suitcase __instance) =>
            AccessibilityCoordinator.Instance?.Puzzles.IsDialing(__instance) != true;
    }
}
