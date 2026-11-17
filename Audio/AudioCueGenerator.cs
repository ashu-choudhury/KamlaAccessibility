using System;
using UnityEngine;

namespace KamlaAccessibility.Audio
{
    /// <summary>
    /// Generates procedural audio cues (UI ticks, radar beeps, boundary alerts)
    /// directly using Unity's audio engine without needing external sound assets.
    /// </summary>
    public static class AudioCueGenerator
    {
        private static AudioSource _audioSource;

        public static void Initialize(GameObject parent)
        {
            if (_audioSource == null && parent != null)
            {
                _audioSource = parent.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 0f; // 2D sound for UI
                _audioSource.volume = 0.6f;
            }
        }

        /// <summary>
        /// Plays a short clean sine tone at a given frequency and duration.
        /// </summary>
        public static void PlayTone(float frequency = 440f, float duration = 0.08f, float volume = 0.5f)
        {
            if (_audioSource == null) return;

            try
            {
                int sampleRate = 44100;
                int samplesCount = (int)(sampleRate * duration);
                float[] samples = new float[samplesCount];

                for (int i = 0; i < samplesCount; i++)
                {
                    float t = (float)i / sampleRate;
                    // Envelope: fast attack, linear decay to avoid audio click
                    float envelope = 1f - ((float)i / samplesCount);
                    samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * volume;
                }

                AudioClip clip = AudioClip.Create("CueTone", samplesCount, 1, sampleRate, false);
                clip.SetData(samples, 0);
                _audioSource.PlayOneShot(clip);
            }
            catch { }
        }

        public static void PlayNavTick() => PlayTone(600f, 0.04f, 0.4f);
        public static void PlayNavSelect() => PlayTone(880f, 0.08f, 0.6f);
        public static void PlayBoundaryHit() => PlayTone(220f, 0.07f, 0.5f);
        public static void PlayRadarBeep(float pitchFactor = 1.0f) => PlayTone(500f * pitchFactor, 0.06f, 0.5f);
        public static void PlayObjectivePing() => PlayTone(1046.5f, 0.15f, 0.7f); // High C
    }
}
