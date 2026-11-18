using MelonLoader;

namespace KamlaAccessibility.Core
{
    public static class ModConfig
    {
        private static MelonPreferences_Category _category;
        private static MelonPreferences_Entry<bool> _enableSpeech;
        private static MelonPreferences_Entry<bool> _enableBraille;
        private static MelonPreferences_Entry<bool> _enableSubtitles;
        private static MelonPreferences_Entry<bool> _enableAudioCues;
        private static MelonPreferences_Entry<float> _radarMaxRange;
        private static MelonPreferences_Entry<bool> _automaticHiding;
        private static MelonPreferences_Entry<bool> _kamlaBeacon;
        private static MelonPreferences_Entry<float> _beaconVolume;

        public static bool EnableSpeech => _enableSpeech?.Value ?? true;
        public static bool EnableBraille => _enableBraille?.Value ?? true;
        public static bool EnableSubtitles => _enableSubtitles?.Value ?? true;
        public static bool EnableAudioCues => _enableAudioCues?.Value ?? true;
        public static bool AutomaticHiding => _automaticHiding?.Value ?? true;
        public static bool EnableKamlaBeacon => _kamlaBeacon?.Value ?? true;
        public static float BeaconVolume => System.Math.Clamp(_beaconVolume?.Value ?? 0.4f, 0f, 1f);

        public static bool ToggleSubtitles()
        {
            _enableSubtitles.Value = !_enableSubtitles.Value;
            _category.SaveToFile();
            return _enableSubtitles.Value;
        }
        public static float RadarMaxRange => _radarMaxRange?.Value ?? 8.0f;

        public static void Initialize()
        {
            _category = MelonPreferences.CreateCategory("KamlaAccessibility", "Kamla Accessibility Mod Settings");
            _enableSpeech = _category.CreateEntry("EnableSpeech", true, "Enable Speech Output");
            _enableBraille = _category.CreateEntry("EnableBraille", true, "Enable Braille Output");
            _enableSubtitles = _category.CreateEntry("EnableSubtitles", true, "Automatically announce all in-game subtitles & chants");
            _enableAudioCues = _category.CreateEntry("EnableAudioCues", true, "Enable procedural UI audio cues and ticks");
            _automaticHiding = _category.CreateEntry("AutomaticHiding", true, "Automatically run to a reachable hiding spot when Kamla is chasing");
            _radarMaxRange = _category.CreateEntry("RadarMaxRange", 8.0f, "Max range in meters for nearby item radar");
            _kamlaBeacon = _category.CreateEntry("EnableKamlaBeacon", true, "Enable a separate directional cue when Kamla is nearby");
            _beaconVolume = _category.CreateEntry("BeaconVolume", 0.4f, "Volume of Kamla's directional warning, from 0 to 1");

            _category.SaveToFile();
        }
    }
}
