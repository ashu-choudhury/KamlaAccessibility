using System;
using HarmonyLib;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using KamlaAccessibility.Gameplay;

namespace KamlaAccessibility.Patches
{
    [HarmonyPatch(typeof(TMP_Text), nameof(TMP_Text.text), MethodType.Setter)]
    public static class TMPText_Subtitle_Patch
    {
        public static void Postfix(TMP_Text __instance, string value)
        {
            if (__instance == null) return;

            string name = __instance.gameObject.name.ToLowerInvariant();
            Transform p = __instance.transform.parent;
            string parentName = p != null ? p.gameObject.name.ToLowerInvariant() : string.Empty;

            if (name.Contains("subtitle") || parentName.Contains("subtitle") ||
                name.Contains("dialogue") || parentName.Contains("dialogue"))
            {
                SubtitleReader.OnDialogueTextChanged(__instance.GetInstanceID(), value);
            }
            else if (name.Contains("chant") || parentName.Contains("chant") ||
                name.Contains("objective") || parentName.Contains("objective") ||
                name.Contains("note") || parentName.Contains("note") ||
                name.Contains("notification") || parentName.Contains("notification"))
            {
                SubtitleReader.OnSubtitleTriggered(value, name);
            }
        }
    }

    [HarmonyPatch(typeof(Text), nameof(Text.text), MethodType.Setter)]
    public static class LegacyText_Subtitle_Patch
    {
        public static void Postfix(Text __instance, string value)
        {
            if (__instance == null) return;

            string name = __instance.gameObject.name.ToLowerInvariant();
            Transform p = __instance.transform.parent;
            string parentName = p != null ? p.gameObject.name.ToLowerInvariant() : string.Empty;

            if (name.Contains("subtitle") || parentName.Contains("subtitle") ||
                name.Contains("dialogue") || parentName.Contains("dialogue"))
            {
                SubtitleReader.OnDialogueTextChanged(__instance.GetInstanceID(), value);
            }
            else if (name.Contains("chant") || parentName.Contains("chant") ||
                name.Contains("objective") || parentName.Contains("objective") ||
                name.Contains("note") || parentName.Contains("note") ||
                name.Contains("notification") || parentName.Contains("notification"))
            {
                SubtitleReader.OnSubtitleTriggered(value, name);
            }
        }
    }
}
