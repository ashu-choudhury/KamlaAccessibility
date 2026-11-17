using System;
using UnityEngine;
using KamlaAccessibility.Speech;
using KamlaAccessibility.Audio;
using MelonLoader;

namespace KamlaAccessibility.Core
{
    /// <summary>
    /// Minimizes all graphical settings to the absolute lowest level to maximize performance,
    /// without affecting audio or gameplay logic.
    /// </summary>
    public static class GraphicsOptimizer
    {
        public static void MinimizeGraphics()
        {
            try
            {
                MelonLogger.Msg("[GraphicsOptimizer] Minimizing graphics settings to lowest level...");

                // 1. Set Unity Quality level to the lowest (0 = Very Low)
                QualitySettings.SetQualityLevel(0, true);

                // 2. Disable Shadows completely
                QualitySettings.shadows = ShadowQuality.Disable;
                QualitySettings.shadowDistance = 0f;
                QualitySettings.shadowResolution = ShadowResolution.Low;
                QualitySettings.shadowCascades = 0;

                // 3. Texture and Mesh Quality
                QualitySettings.globalTextureMipmapLimit = 2; // Lower texture resolution
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;

                // 4. Anti-Aliasing & VSync
                QualitySettings.antiAliasing = 0;
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 60; // Cap to 60 FPS to prevent GPU overheating

                // 5. Lighting & Particles
                QualitySettings.realtimeReflectionProbes = false;
                QualitySettings.softParticles = false;
                QualitySettings.softVegetation = false;
                QualitySettings.particleRaycastBudget = 16;
                QualitySettings.lodBias = 0.5f;
                QualitySettings.maximumLODLevel = 1;

                AudioCueGenerator.PlayNavSelect();
                PrismSpeechProvider.Speak("Graphic settings minimized to lowest level. Game performance optimized.", true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GraphicsOptimizer] Error applying graphics settings: {ex.Message}");
                PrismSpeechProvider.Speak("Graphic settings updated.", true);
            }
        }
    }
}
