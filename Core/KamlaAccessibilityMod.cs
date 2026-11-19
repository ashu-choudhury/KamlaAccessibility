using System;
using MelonLoader;
using KamlaAccessibility.Core;
using KamlaAccessibility.Speech;

[assembly: MelonInfo(typeof(KamlaAccessibilityMod), "Kamla Accessibility Mod", KamlaAccessibilityMod.Version, "Ashu")]
[assembly: MelonGame("Mad-Mantra Games", "Kamla")]

namespace KamlaAccessibility.Core
{
    public class KamlaAccessibilityMod : MelonMod
    {
        internal const string Version = "2.0.3";

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("==================================================");
            MelonLogger.Msg($"  Kamla Accessibility Mod v{Version} (Prism Powered)  ");
            MelonLogger.Msg("==================================================");

            try
            {
                // 1. Initialize Configuration
                ModConfig.Initialize();

                // 2. Initialize Prism Speech Engine
                PrismSpeechProvider.Initialize();

                // 3. Initialize Coordinator
                AccessibilityCoordinator.Initialize();

                PrismSpeechProvider.Speak("Kamla Accessibility Mod loaded. Welcome to Kamla.", true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to initialize Kamla Accessibility Mod: {ex}");
            }
        }

        public override void OnUpdate()
        {
            AccessibilityCoordinator.Instance?.Update();
        }

        public override void OnApplicationQuit()
        {
            try
            {
                PrismSpeechProvider.Shutdown();
            }
            catch { }
        }
    }
}
