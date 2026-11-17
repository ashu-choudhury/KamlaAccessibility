using System;
using MelonLoader;
using Il2CppInterop.Runtime.Injection;
using KamlaAccessibility.Core;
using KamlaAccessibility.Speech;
using KamlaAccessibility.Audio;

[assembly: MelonInfo(typeof(KamlaAccessibility.Core.KamlaAccessibilityMod), "Kamla Accessibility Mod", "1.0.0", "Ashu")]
[assembly: MelonGame("Mad-Mantra Games", "Kamla")]

namespace KamlaAccessibility.Core
{
    public class KamlaAccessibilityMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("==================================================");
            MelonLogger.Msg("  Kamla Accessibility Mod v1.0.0 (Prism Powered)  ");
            MelonLogger.Msg("==================================================");

            try
            {
                // 1. Initialize Configuration
                ModConfig.Initialize();

                // 2. Register IL2CPP Components
                ClassInjector.RegisterTypeInIl2Cpp<AccessibilityManager>();
                ClassInjector.RegisterTypeInIl2Cpp<AudioBeacon>();
                MelonLogger.Msg("Registered IL2CPP accessibility components.");

                // 3. Initialize Prism Speech Engine
                PrismSpeechProvider.Initialize();

                PrismSpeechProvider.Speak("Kamla Accessibility Mod loaded. Welcome to Kamla.", true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to initialize Kamla Accessibility Mod: {ex}");
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            try
            {
                AccessibilityManager.EnsureInstance();

                string friendlyName = sceneName;
                if (sceneName.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    sceneName.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    sceneName.IndexOf("Start", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    friendlyName = "Main Menu";
                }
                else if (sceneName.IndexOf("Game", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         sceneName.IndexOf("Haveli", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         sceneName.IndexOf("Level", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    friendlyName = "The Haveli";
                }

                PrismSpeechProvider.Speak($"{friendlyName} loaded.", true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in OnSceneWasLoaded: {ex.Message}");
            }
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
