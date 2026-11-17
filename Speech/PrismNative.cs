using System;
using System.Runtime.InteropServices;

namespace KamlaAccessibility.Speech
{
    /// <summary>
    /// P/Invoke bindings for the C API exported by prism.dll (https://github.com/ethindp/prism).
    /// </summary>
    internal static class PrismNative
    {
        private const string DllName = "prism";

        public const int PRISM_OK = 0;
        public const int PRISM_ERROR_NOT_FOUND = 1;
        public const int PRISM_ERROR_UNSUPPORTED = 2;
        public const int PRISM_ERROR_INITIALIZATION = 3;
        public const int PRISM_ERROR_IO = 4;
        public const int PRISM_ERROR_NOT_INITIALIZED = 14;
        public const int PRISM_ERROR_ALREADY_INITIALIZED = 15;

        public const long BACKEND_SUPPORTS_SPEAK = 1L << 0;
        public const long BACKEND_SUPPORTS_BRAILLE = 1L << 4;
        public const long BACKEND_SUPPORTS_OUTPUT = 1L << 5;

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr prism_init(IntPtr config);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void prism_shutdown(IntPtr context);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr prism_registry_create_best(IntPtr context);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void prism_backend_free(IntPtr backend);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr prism_backend_name(IntPtr backend);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern long prism_backend_get_features(IntPtr backend);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int prism_backend_speak(IntPtr backend, byte[] textUtf8, [MarshalAs(UnmanagedType.I1)] bool interrupt);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int prism_backend_braille(IntPtr backend, byte[] textUtf8);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int prism_backend_output(IntPtr backend, byte[] textUtf8, [MarshalAs(UnmanagedType.I1)] bool interrupt);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int prism_backend_stop(IntPtr backend);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr prism_error_string(int code);
    }
}
