using System;
using UnityEngine;
using KamlaAccessibility.Core;

namespace KamlaAccessibility.Audio
{
    /// <summary>
    /// Provides 3D directional audio beacons to guide blind players toward objectives and the central altar.
    /// </summary>
    public class AudioBeacon : MonoBehaviour
    {
        private static AudioBeacon _instance;
        private AudioSource _audioSource;
        private Transform _targetTransform;
        private float _pingTimer = 0f;
        private const float PingInterval = 3.5f;

        public AudioBeacon(IntPtr ptr) : base(ptr) { }

        public static void EnsureBeacon(GameObject host)
        {
            if (_instance == null && host != null)
            {
                _instance = host.AddComponent<AudioBeacon>();
            }
        }

        private void Start()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1.0f; // 3D sound
            _audioSource.rolloffMode = AudioRolloffMode.Linear;
            _audioSource.minDistance = 1.5f;
            _audioSource.maxDistance = 35.0f;
            _audioSource.dopplerLevel = 0f;
            _audioSource.volume = 0.7f;
        }

        private void Update()
        {
            if (!ModConfig.EnableAudioBeacon) return;

            _pingTimer += Time.deltaTime;
            if (_pingTimer >= PingInterval)
            {
                _pingTimer = 0f;
                PingTarget();
            }
        }

        public void SetTarget(Transform target)
        {
            _targetTransform = target;
        }

        private void PingTarget()
        {
            if (_targetTransform == null)
            {
                // Auto-locate Courtyard Altar / Havan / Murti if not explicitly set
                var havan = GameObject.Find("PlaceCircleCourtyard") ?? GameObject.Find("HavanParticleSystem") ?? GameObject.Find("CourtyardMechanic");
                if (havan != null)
                {
                    _targetTransform = havan.transform;
                }
            }

            if (_targetTransform == null || _audioSource == null) return;

            transform.position = _targetTransform.position;
            AudioCueGenerator.PlayObjectivePing();
        }
    }
}
