using System;
using Elfenlabs.Unsafe;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Elfenlabs.Collections
{
    public unsafe struct UnsafeReference<T> : IDisposable, IEquatable<UnsafeReference<T>>
        where T : unmanaged
    {
        T* ptr;

        Allocator allocator;

        public ref T Value => ref UnsafeUtility.AsRef<T>(ptr);

        public readonly bool IsCreated => ptr != null && allocator != Allocator.Invalid;

        public UnsafeReference(Allocator allocator)
        {
            this.allocator = allocator;
            this.ptr = (T*)UnsafeUtility.Malloc(
                UnsafeUtility.SizeOf<T>(),
                UnsafeUtility.AlignOf<T>(),
                allocator);
        }

        public UnsafeReference(T value, Allocator allocator)
        {
            this.allocator = allocator;
            this.ptr = (T*)UnsafeUtility.Malloc(
                UnsafeUtility.SizeOf<T>(),
                UnsafeUtility.AlignOf<T>(),
                allocator);
            *ptr = value;
        }

        public void Dispose()
        {
            if (ptr != null && allocator != Allocator.Invalid)
            {
                UnsafeUtility.Free(ptr, allocator);
                ptr = null;
            }
        }

        public bool Equals(UnsafeReference<T> other)
        {
            return ptr == other.ptr;
        }

        public override readonly int GetHashCode()
        {
            return (int)ptr;
        }
    }
}