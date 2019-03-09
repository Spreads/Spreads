﻿using Spreads.Extensions;
using Spreads.Serialization;
using Spreads.Utils;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads
{
    internal class AdditionalCorrectnessChecks
    {
        // Unless _doAdditionalCorrectnessChecks is changed from default before this internal
        // class is ever referenced from any code path it will have default
        // value and this field will be JIT compile-time constant. If set to false checks such as
        // if(AdditionalCorrectnessChecks.Enabled) will be
        // completely eliminated by JIT.

#if DEBUG
        public static readonly bool Enabled = true;
#else
        public static readonly bool Enabled = Settings._doAdditionalCorrectnessChecks;
#endif
    }

    /// <summary>
    /// Global settings.
    /// </summary>
    public static class Settings
    {
        // ReSharper disable once InconsistentNaming
        internal const int LARGE_BUFFER_LIMIT = 64 * 1024;

        // ReSharper disable once InconsistentNaming
        internal const int MIN_POOLED_BUFFER_LEN = 16;

        // TODO find best values
        /// <summary>
        ///
        /// </summary>
        public const int SliceMemoryAlignment = 8;

        // TODO Review: we do not need that big for every worker thread (default thread pool + Spreads' one = a lot)
        // It's used for temp serialization when we need pinned memory without costs of Rent/Pin/Unpin/Return
        // If we make it small then buffers are not in LOH and will fragment heap.
        // Possible option is one large buffer with static incrementing counter and ThreadStatic index
        // Could be stored as IntPtr+length and could be off-heap, but old APIs often need byte[].
        // Another option - Retain() like we do for small buffers, but with interlocked add for claim
        // and CAS for release. If pos = xadd(len) to get our end position, do work and then CAS(pos-len, pos)
        // if we are unable to decrement position via CAS then just forget about it and rotate the buffer
        // when no one is using it.

        // NB at least 85k for LOH
        internal static readonly int ThreadStaticPinnedBufferSize = BitUtil.FindNextPositivePowerOfTwo(LARGE_BUFFER_LIMIT);

        /// <summary>
        /// Call Trace.TraceWarning when a finalizer of IDisposable objects is called.
        /// </summary>
        public static bool TraceFinalizationOfIDisposables { get; set; }

        ///// <summary>
        ///// Throw <see cref="OutOfOrderKeyException{TKey}"/> if Zip input values arrive out of order.
        ///// </summary>
        //public static bool ZipThrowOnOunOfOrderInputs { get; set; }

        /// <summary>
        /// Get or set default compression method: BinaryLz4 (default) or BinaryZstd).
        /// </summary>
        public static SerializationFormat DefaultSerializationFormat { get; set; } = SerializationFormat.JsonGZip;

        // TODO when/if used often benchmark its effect and if significant then set default to false
        // ReSharper disable once NotAccessedField.Local
        internal static bool _doAdditionalCorrectnessChecks = true;

        /// <summary>
        /// Enable/disable additional correctness checks that could affect performance or exit the process with FailFast.
        /// This could only be set at application startup before accessing other Spreads types.
        /// By default this value is set to true and Spreads usually fails fast on any condition
        /// that could compromise calculation correctness or data integrity. If an application
        /// that uses Spreads runs correctly "long-enough" this setting could be set to false to measure
        /// the effect of the checks on performance. If the effect is not significant it is safer
        /// to keep this option as true if correctness of Spreads is more important than nanoseconds
        /// and fail fast is more acceptable scenario than incorrect calculations.
        /// </summary>
        /// <remarks>
        /// In Spreads, this setting is used for cases that does not reduce performance
        /// a lot, e.g. null or range checks in hot loops that take fixed number of
        /// CPU cycles, break inlining or similar fixed-time impact.
        /// It should not be used e.g. to allocate finalizable objects to detect leaks
        /// in buffer management and other cases that could generate a lot of garbage and
        /// induce GC latency. For the later cases `#if DEBUG` is used.
        /// </remarks>
        public static bool DoAdditionalCorrectnessChecks
        {
            get => AdditionalCorrectnessChecks.Enabled;
            set
            {
                _doAdditionalCorrectnessChecks = value;

                // access it immediately: https://github.com/dotnet/coreclr/issues/2526
                if (!AdditionalCorrectnessChecks.Enabled)
                {
                    Trace.TraceInformation("Disabled AdditionalCorrectnessChecks");
                }
            }
        }

        // ReSharper disable once InconsistentNaming
        internal static int SCMDefaultChunkLength = 4096;

        // See e.g. https://gregoryszorc.com/blog/2017/03/07/better-compression-with-zstandard/
        internal static int _lz4CompressionLevel = 5;

        internal static int _zstdCompressionLevel = 1;

        internal static int _zlibCompressionLevel = 3;

        // ReSharper disable once InconsistentNaming
        public static int LZ4CompressionLevel
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _lz4CompressionLevel;
            set
            {
                if (value < 0 || value >= 10)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException();
                }
                _lz4CompressionLevel = value;
                BinarySerializer.UpdateLevels();
            }
        }

        public static int ZstdCompressionLevel
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _zstdCompressionLevel;
            set
            {
                if (value < 0 || value >= 10)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException();
                }
                _zstdCompressionLevel = value;
                BinarySerializer.UpdateLevels();
            }
        }

        public static int ZlibCompressionLevel
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _zlibCompressionLevel;
            set
            {
                if (value < 0 || value >= 10)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException();
                }
                _zlibCompressionLevel = value;
                BinarySerializer.UpdateLevels();
            }
        }

#if NETCOREAPP2_1
        internal static int _brotliCompressionLevel = 3;
        public static int BrotliCompressionLevel
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _brotliCompressionLevel;
            set
            {
                if (value < 0 || value >= 10)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException();
                }

                if (value > 4)
                {
                    Trace.TraceWarning("Setting BrotliCompressionLevel > 4 could be very slow without much gain in compression ratio. Check performance on real data.");
                }
                _brotliCompressionLevel = value;
            }
        }
#endif

        /// <summary>
        /// Set this property before accessing ChaosMonkey or types that use it.
        /// When false then this value is stored in static readonly field and is
        /// optimized away by JIT without any performance impact.
        /// </summary>
        public static bool EnableChaosMonkey { get; set; } = false;

        // Good to know that this works and could use later when calli becomes an intrinsic,
        // but cannot use default calli for native calls: https://github.com/dotnet/coreclr/issues/19997
        // With `unmanaged cdecl` performance is equal to DllImport. CoreClr/CoreFx use the same DllImport
        // as user code so there is nothing better that that for native interop.
        internal static readonly bool PreferCalli = false;

        internal static int _compressionStartFrom = 860;

        /// <summary>
        /// Minimum serialized payload size to apply compression.
        /// The value is inclusive.
        /// </summary>
        public static int CompressionStartFrom
        {
            // TODO (review) for small values with all numeric (esp. non-double) fields this method call could have visible impact, maybe do the same thing as with AdditionalCorrectness
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _compressionStartFrom;
            set
            {
                if (value < 500)
                {
                    value = 500;
                }
                _compressionStartFrom = value;
            }
        }

        public static int AtomicCounterPoolBucketSize { get; set; } = 1024;

        public static bool LogMemoryPoolEvents { get; set; } = false;

        private static Opt<bool> _safeBinarySerializerWrite = Opt<bool>.Missing;

        /// <summary>
        /// If <see cref="IBinarySerializer{T}.SizeOf"/> does not return a temp buffer
        /// then force using a temp buffer for destination of <see cref="IBinarySerializer{T}.Write"/>
        /// method. This is slower but useful to ensure correct implementation of
        /// a custom <see cref="IBinarySerializer{T}"/>.
        ///
        /// <para>By default is set to <see cref="DoAdditionalCorrectnessChecks"/></para>
        ///
        /// </summary>
        public static bool DefensiveBinarySerializerWrite
        {
            get => _safeBinarySerializerWrite.PresentOrDefault(AdditionalCorrectnessChecks.Enabled);
            set => _safeBinarySerializerWrite = Opt.Present(value);
        }

        /// <summary>
        /// Set to true to use <see cref="StructLayoutAttribute.Size"/> as <see cref="BinarySerializationAttribute.BlittableSize"/>.
        /// </summary>
        public static bool UseStructLayoutSizeAsBlittableSize = false;

        internal const int SlabLength = 128 * 1024;

        /// <summary>
        /// This only affects known types for which interpolation search works correctly
        /// and is expected to be significantly faster. No reason to set it to false.
        /// </summary>
        internal const bool UseInterpolatedSearchForKnownTypes = true;

        public static int SharedSpinLockNotificationPort = 0;

        internal static Action ZeroValueNotificationCallback = null;
        internal static Func<IQueryHandler> QueryHandlerFactory = null;
    }
}
