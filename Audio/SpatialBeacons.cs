using System;
using System.IO;
using UnityEngine;
using Il2Cpp;
using KamlaAccessibility.Core;
using KamlaAccessibility.Gameplay;
using Object = UnityEngine.Object;

namespace KamlaAccessibility.Audio
{
    internal sealed class SpatialBeacons
    {
        private AudioSource _threat;
        private AudioClip _threatClip;
        private float _nextThreat;

        public void Update(PlayerMovement player)
        {
            if (ModConfig.BeaconVolume <= 0f) { Stop(); return; }

            var listener = player.mainCam != null ? player.mainCam.transform : player.transform;
            var enemy = player.enemyRef;
            if (ModConfig.EnableKamlaBeacon && enemy != null && enemy.isActiveAndEnabled)
            {
                Pulse(listener, enemy.transform.position, 22f, enemy.IsChasing, ref _threat, ref _threatClip,
                    "Kamla", ref _nextThreat);
            }
            else
            {
                Silence(_threat, ref _nextThreat);
            }

        }

        private static void Pulse(Transform listener, Vector3 position, float maxDistance, bool chasing,
            ref AudioSource source, ref AudioClip clip, string name, ref float next)
        {
            Vector3 delta = position - listener.position;
            float distance = delta.magnitude;
            if (distance > maxDistance)
            {
                Silence(source, ref next);
                return;
            }

            if (source == null)
            {
                var emitter = new GameObject($"Accessibility beacon: {name}");
                source = emitter.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                next = 0f;
            }
            if (clip == null) clip = LoadClip(name);

            Vector3 direction = NavigationHelper.Flat(delta).normalized;
            source.panStereo = Mathf.Clamp(Vector3.Dot(NavigationHelper.Flat(listener.right).normalized, direction), -1f, 1f);
            float facing = Vector3.Dot(NavigationHelper.Flat(listener.forward).normalized, direction);
            source.pitch = facing < -0.25f ? 0.76f : 1f;
            float proximity = 1f - distance / maxDistance;
            source.volume = Mathf.Lerp(0.16f, 1f, proximity) * ModConfig.BeaconVolume;

            if (Time.unscaledTime < next) return;
            source.PlayOneShot(clip);
            next = Time.unscaledTime + Mathf.Lerp(chasing ? 0.43f : 0.62f, chasing ? 0.9f : 1.9f, distance / maxDistance);
        }

        private static AudioClip LoadClip(string name)
        {
            using var stream = typeof(SpatialBeacons).Assembly.GetManifestResourceStream($"KamlaAccessibility.Audio.BeaconSounds.{name}.pcm");
            using var reader = new BinaryReader(stream ?? throw new InvalidOperationException($"Missing beacon audio: {name}."));
            var samples = new float[stream.Length / 2];
            for (int i = 0; i < samples.Length; i++) samples[i] = reader.ReadInt16() / 32768f;
            var clip = AudioClip.Create(name, samples.Length, 1, 24000, false);
            if (!clip.SetData(samples, 0)) throw new InvalidOperationException($"Cannot load beacon audio: {name}.");
            return clip;
        }

        private static void Silence(AudioSource source, ref float next)
        {
            if (source != null && source.isPlaying) source.Stop();
            next = 0f;
        }

        public void Stop()
        {
            Silence(_threat, ref _nextThreat);
        }

        public void ResetScene()
        {
            Stop();
            if (_threat != null) Object.Destroy(_threat.gameObject);
            _threat = null;
        }
    }
}
