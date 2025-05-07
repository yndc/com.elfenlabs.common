using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace Elfenlabs.Collections
{
    /// <summary>
    /// Array that can be passed to and from C++ plugins.
    /// Best practice to manage the lifetime of the buffer in the C# land.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct NativeBuffer<T> : IEnumerable<T>, IDisposable
        where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        private IntPtr ptr;
        private Allocator allocator;
        private int size;

        /// <summary>
        /// Create a NativeBuffer from a NativeArray.
        /// </summary>
        /// <param name="array"></param>
        /// <returns></returns>
        public static NativeBuffer<T> Alias(NativeArray<T> array)
        {
            unsafe
            {
                var buffer = new NativeBuffer<T>
                {
                    size = array.Length * UnsafeUtility.SizeOf<T>(),
                    allocator = Allocator.Invalid,
                    ptr = (IntPtr)array.GetUnsafeReadOnlyPtr()
                };
                return buffer;
            }
        }

        /// <summary>
        /// Create a NativeBuffer from a NativeArray.
        /// </summary>
        /// <param name="array"></param>
        /// <returns></returns>
        public static NativeBuffer<T> Alias(DynamicBuffer<T> array)
        {
            unsafe
            {
                var buffer = new NativeBuffer<T>
                {
                    size = array.Length * UnsafeUtility.SizeOf<T>(),
                    allocator = Allocator.Invalid,
                    ptr = (IntPtr)array.GetUnsafeReadOnlyPtr()
                };
                return buffer;
            }
        }

        public static NativeBuffer<byte> FromString(string str, Allocator allocator)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(str);
            return NativeBuffer<byte>.FromBytes(bytes, allocator);
        }

        public static NativeBuffer<byte> Alias(FixedString128Bytes str)
        {
            unsafe
            {
                return new NativeBuffer<byte>
                {
                    size = str.Length,
                    allocator = Allocator.Invalid,
                    ptr = (IntPtr)str.GetUnsafePtr()
                };
            }
        }

        public NativeArray<T> AsNativeArray()
        {
            unsafe
            {
                var arr = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(ptr.ToPointer(), Count(), allocator);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                var atomicSafetyHandle = AtomicSafetyHandle.Create();
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref arr, atomicSafetyHandle);
#endif
                return arr;
            }
        }

        public static NativeBuffer<byte> FromBytes(byte[] bytes, Allocator allocator)
        {
            var buffer = new NativeBuffer<byte>(bytes.Length, allocator);
            Marshal.Copy(bytes, 0, buffer.ptr, bytes.Length);
            return buffer;
        }

        public NativeBuffer(IntPtr ptr, Allocator allocator, int size)
        {
            this.ptr = ptr;
            this.allocator = allocator;
            this.size = size;
        }

        public NativeBuffer(int length, Allocator allocator)
        {
            size = length * UnsafeUtility.SizeOf<T>();
            this.allocator = allocator;
            unsafe
            {
                ptr = (IntPtr)UnsafeUtility.Malloc(size, 4, allocator);
            }
        }

        public readonly int SizeBytes()
        {
            return size;
        }

        public readonly int Count()
        {
            return size / ItemSize();
        }

        public readonly int ItemSize()
        {
            unsafe
            {
                return sizeof(T);
            }
        }

        public readonly T this[int index]
        {
            get
            {
                unsafe
                {
                    return Marshal.PtrToStructure<T>(IntPtr.Add(ptr, index * sizeof(T)));
                }
            }
            set
            {
                unsafe
                {
                    Marshal.StructureToPtr(value, IntPtr.Add(ptr, index * sizeof(T)), false);
                }
            }
        }

        public unsafe T* GetUnsafePtr()
        {
            unsafe
            {
                return (T*)ptr;
            }
        }

        public void Dispose()
        {
            if (allocator == Allocator.Invalid)
            {
                return;
            }
            unsafe
            {
                UnsafeUtility.Free((void*)ptr, allocator);
            }
            allocator = Allocator.Invalid;
        }

        public unsafe JobHandle Dispose(JobHandle inputDeps)
        {
            if (allocator == Allocator.Invalid)
            {
                return inputDeps;
            }
            var job = new BufferDisposalJob
            {
                Ptr = ptr,
                Allocator = allocator
            };
            return job.Schedule(inputDeps);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new Enumerator(ref this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public static implicit operator NativeSlice<T>(NativeBuffer<T> buffer)
        {
            unsafe
            {
                var slice = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<T>(buffer.GetUnsafePtr(), UnsafeUtility.SizeOf<T>(), buffer.Count());
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeSliceUnsafeUtility.SetAtomicSafetyHandle(ref slice, AtomicSafetyHandle.Create());
#endif
                return slice;
            }
        }

        public struct Enumerator : IEnumerator<T>, IEnumerator, IDisposable
        {
            private NativeBuffer<T> buffer;

            private int index;

            private T value;

            public T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return value;
                }
            }

            object IEnumerator.Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return Current;
                }
            }

            public Enumerator(ref NativeBuffer<T> buffer)
            {
                this.buffer = buffer;
                index = -1;
                value = default;
            }

            public void Dispose()
            {
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe bool MoveNext()
            {
                index++;
                if (index < buffer.Count())
                {
                    value = UnsafeUtility.ReadArrayElement<T>((void*)buffer.ptr, index);
                    return true;
                }

                value = default;
                return false;
            }

            public void Reset()
            {
                index = -1;
            }
        }

        struct BufferDisposalJob : IJob
        {
            [NativeDisableUnsafePtrRestriction]
            public IntPtr Ptr;
            public Allocator Allocator;

            public readonly void Execute()
            {
                unsafe
                {
                    UnsafeUtility.Free((void*)Ptr, Allocator);
                }
            }
        }
    }
}