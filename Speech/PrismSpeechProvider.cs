using System;
using System.Runtime.InteropServices;
using System.Text;
using MelonLoader;

namespace KamlaAccessibility.Speech
{
    /// <summary>
    /// High-level Speech & Screen Reader service powered by the Prism library (prism.dll).
    /// Gracefully falls back to Windows SAPI COM automation if Prism fails to initialize.
    /// </summary>
    public static class PrismSpeechProvider
    {
        private static IntPtr _context = IntPtr.Zero;
        private static IntPtr _backend = IntPtr.Zero;
        private static string _backendName = "None";
        private static bool _initialized = false;
        private static bool _fallbackActive = false;
        private static dynamic _sapiVoice = null;

        private static string _lastSpoken = "";
        private static readonly object _lock = new object();

        public static bool IsInitialized => _initialized;
        public static string BackendName => _backendName;
        public static string LastSpokenText => _lastSpoken;

        public static void Initialize()
        {
            lock (_lock)
            {
                if (_initialized) return;

                MelonLogger.Msg("[PrismSpeech] Initializing Prism speech engine...");

                try
                {
                    _context = PrismNative.prism_init(IntPtr.Zero);
                    if (_context != IntPtr.Zero)
                    {
                        _backend = PrismNative.prism_registry_create_best(_context);
                        if (_backend != IntPtr.Zero)
                        {
                            IntPtr namePtr = PrismNative.prism_backend_name(_backend);
                            if (namePtr != IntPtr.Zero)
                            {
                                _backendName = Marshal.PtrToStringAnsi(namePtr) ?? "Unknown Prism Backend";
                            }
                            else
                            {
                                _backendName = "Default Prism Backend";
                            }

                            _initialized = true;
                            MelonLogger.Msg($"[PrismSpeech] Successfully initialized Prism backend: '{_backendName}'");
                            return;
                        }
                        else
                        {
                            MelonLogger.Warning("[PrismSpeech] prism_registry_create_best returned NULL. Falling back to SAPI.");
                            PrismNative.prism_shutdown(_context);
                            _context = IntPtr.Zero;
                        }
                    }
                    else
                    {
                        MelonLogger.Warning("[PrismSpeech] prism_init returned NULL. Falling back to SAPI.");
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[PrismSpeech] Exception initializing Prism native DLL: {ex.Message}. Falling back to SAPI.");
                }

                // Fallback to SAPI COM
                InitializeSapiFallback();
            }
        }

        private static void InitializeSapiFallback()
        {
            try
            {
                Type sapiType = Type.GetTypeFromProgID("SAPI.SpVoice");
                if (sapiType != null)
                {
                    _sapiVoice = Activator.CreateInstance(sapiType);
                    _backendName = "Windows SAPI Fallback";
                    _fallbackActive = true;
                    _initialized = true;
                    MelonLogger.Msg("[PrismSpeech] SAPI Fallback voice initialized successfully.");
                }
                else
                {
                    MelonLogger.Error("[PrismSpeech] SAPI.SpVoice not available on this system.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[PrismSpeech] Failed to initialize SAPI fallback: {ex.Message}");
            }
        }

        /// <summary>
        /// Speaks the given text using Prism (both speech and braille), or SAPI fallback.
        /// </summary>
        /// <param name="text">Text to speak</param>
        /// <param name="interrupt">If true, interrupts any current speech</param>
        public static void Speak(string text, bool interrupt = true)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            lock (_lock)
            {
                _lastSpoken = text;

                if (!_initialized)
                {
                    Initialize();
                }

                if (_backend != IntPtr.Zero)
                {
                    try
                    {
                        byte[] utf8Bytes = Encoding.UTF8.GetBytes(text + "\0");
                        int result = PrismNative.prism_backend_output(_backend, utf8Bytes, interrupt);
                        if (result != PrismNative.PRISM_OK)
                        {
                            // If output fails, try speak alone
                            PrismNative.prism_backend_speak(_backend, utf8Bytes, interrupt);
                        }
                        return;
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[PrismSpeech] Error in prism_backend_output: {ex.Message}");
                    }
                }

                // SAPI Fallback
                if (_fallbackActive && _sapiVoice != null)
                {
                    try
                    {
                        // SVSFlagsAsync = 1, SVSFPurgeBeforeSpeak = 2
                        int flags = 1;
                        if (interrupt) flags |= 2;
                        _sapiVoice.Speak(text, flags);
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[PrismSpeech] SAPI Speak error: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Sends text strictly to Braille display via Prism.
        /// </summary>
        public static void Braille(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || _backend == IntPtr.Zero) return;

            lock (_lock)
            {
                try
                {
                    byte[] utf8Bytes = Encoding.UTF8.GetBytes(text + "\0");
                    PrismNative.prism_backend_braille(_backend, utf8Bytes);
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[PrismSpeech] Braille error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Immediately stops any ongoing speech.
        /// </summary>
        public static void Stop()
        {
            lock (_lock)
            {
                if (_backend != IntPtr.Zero)
                {
                    try
                    {
                        PrismNative.prism_backend_stop(_backend);
                    }
                    catch { }
                }

                if (_fallbackActive && _sapiVoice != null)
                {
                    try
                    {
                        // SVSFPurgeBeforeSpeak = 2
                        _sapiVoice.Speak("", 2);
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Repeats the last spoken announcement.
        /// </summary>
        public static void RepeatLast()
        {
            if (!string.IsNullOrEmpty(_lastSpoken))
            {
                Speak(_lastSpoken, true);
            }
        }

        /// <summary>
        /// Shuts down the Prism context and frees resources.
        /// </summary>
        public static void Shutdown()
        {
            lock (_lock)
            {
                Stop();

                if (_backend != IntPtr.Zero)
                {
                    try
                    {
                        PrismNative.prism_backend_free(_backend);
                    }
                    catch { }
                    _backend = IntPtr.Zero;
                }

                if (_context != IntPtr.Zero)
                {
                    try
                    {
                        PrismNative.prism_shutdown(_context);
                    }
                    catch { }
                    _context = IntPtr.Zero;
                }

                _sapiVoice = null;
                _initialized = false;
                _fallbackActive = false;
            }
        }
    }
}
