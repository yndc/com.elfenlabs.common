using System;
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
    public struct NativeBuffer<T> where T : unmanaged
    {
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
                var atomicSafetyHandle = AtomicSafetyHandle.Create();
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref arr, atomicSafetyHandle);
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