using System.Runtime.InteropServices;

namespace Spreads {
    /// <summary>
    /// Call Marshal.SizeOf only once per type and store it is a static Size property.
    /// Returns -1 if Marshal.SizeOf throws.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class TypeSizeHelper<T> where T : struct {
        static TypeSizeHelper() {
            try {
                Size = Marshal.SizeOf(typeof(T));
            } catch {
                Size = -1;
            }
        }

        public static int Size { get; }
    }
}