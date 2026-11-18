using HarmonyLib;
using Il2Cpp;
using KamlaAccessibility.Gameplay;

namespace KamlaAccessibility.Patches
{
    [HarmonyPatch(typeof(UIManager), nameof(UIManager.TextActivate))]
    internal static class GamePromptPatch
    {
        private static void Postfix(string __0) => SubtitleReader.OnSubtitleTriggered(__0, "Prompt");
    }

    [HarmonyPatch(typeof(UIManager), nameof(UIManager.TextDeactivate))]
    internal static class ClearGamePromptPatch
    {
        private static void Postfix() => SubtitleReader.ClearPrompt();
    }

    [HarmonyPatch(typeof(UIManager), nameof(UIManager.UIPanelActivate))]
    internal static class InspectionPanelPatch
    {
        private static void Postfix(string __1, string __2)
        {
            if (!string.IsNullOrWhiteSpace(__1) || !string.IsNullOrWhiteSpace(__2))
                SubtitleReader.OnSubtitleTriggered($"{__1}. {__2}", "Inspection");
        }
    }
}
