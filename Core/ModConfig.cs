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
        private static MelonPreferences_Entry<bool> _enableAudioBeacon;

        public static bool EnableSpeech => _enableSpeech?.Value ?? true;
        public static bool EnableBraille => _enableBraille?.Value ?? true;
        public static bool EnableSubtitles => _enableSubtitles?.Value ?? true;
        public static bool EnableAudioCues => _enableAudioCues?.Value ?? true;
        public static float RadarMaxRange => _radarMaxRange?.Value ?? 8.0f;
        public static bool EnableAudioBeacon
        {
            get => _enableAudioBeacon?.Value ?? true;
            set
            {
                if (_enableAudioBeacon != null)
                {
                    _enableAudioBeacon.Value = value;
                    _category?.SaveToFile();
                }
            }
        }

        public static void Initialize()
        {
            _category = MelonPreferences.CreateCategory("KamlaAccessibility", "Kamla Accessibility Mod Settings");
            _enableSpeech = _category.CreateEntry("EnableSpeech", true, "Enable Speech Output");
            _enableBraille = _category.CreateEntry("EnableBraille", true, "Enable Braille Output");
            _enableSubtitles = _category.CreateEntry("EnableSubtitles", true, "Automatically announce all in-game subtitles & chants");
            _enableAudioCues = _category.CreateEntry("EnableAudioCues", true, "Enable procedural UI audio cues and ticks");
            _radarMaxRange = _category.CreateEntry("RadarMaxRange", 8.0f, "Max range in meters for nearby item radar");
            _enableAudioBeacon = _category.CreateEntry("EnableAudioBeacon", true, "Enable directional 3D audio beacon for objectives");

            _category.SaveToFile();
        }
    }
}
