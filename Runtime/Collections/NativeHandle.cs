using System;
using System.Runtime.InteropServices;

namespace Elfenlabs.Collections
{
    /// <summary>
    /// Simple wrapper for a native handle reference with a tag type
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [System.Runtime.InteropServices.StructLayout(LayoutKind.Sequential)] // Ensure predictable layout
    public readonly struct NativeHandle<T> : IEquatable<NativeHandle<T>> where T : struct
    {
        public readonly IntPtr Value;

        // Internal constructor to control creation
        internal NativeHandle(IntPtr value)
        {
            Value = value;
        }

        public bool IsCreated => Value != IntPtr.Zero;

        // Null handle representation
        public static readonly NativeHandle<T> Null = new(IntPtr.Zero);

        // --- Conversion Operators ---

        // Allow explicit conversion TO IntPtr (for P/Invoke calls)
        public static explicit operator IntPtr(NativeHandle<T> handle)
        {
            return handle.Value;
        }

        // Allow explicit conversion FROM IntPtr (when receiving from P/Invoke)
        public static explicit operator NativeHandle<T>(IntPtr ptr)
        {
            return new NativeHandle<T>(ptr);
        }

        // --- Equality ---

        public bool Equals(NativeHandle<T> other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            // Use 'is' pattern matching for concise type check and cast
            return obj is NativeHandle<T> other && Equals(other);
        }

        public override int GetHashCode()
        {
            // Delegate directly to IntPtr's GetHashCode
            return Value.GetHashCode();
        }

        public static bool operator ==(NativeHandle<T> left, NativeHandle<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NativeHandle<T> left, NativeHandle<T> right)
        {
            return !(left == right);
        }

        public override string ToString()
        {
            return $"NativeHandke<{typeof(T).Name}>({Value})";
        }
    }
}