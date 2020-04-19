// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

#define ADDITIONAL_CHECKS

using Spreads.Serialization;
using Spreads.Utils;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Spreads.Native;

namespace Spreads
{
    internal static class AdditionalCorrectnessChecks
    {
        /// <summary>
        /// A JIT-time constant that could be enabled via <see cref="Settings.DoAdditionalCorrectnessChecks"/>
        /// in Release mode (default is false). It is always true in Debug mode.
        /// </summary>
        /// <remarks>
        /// Unless <see cref="Settings.DoAdditionalCorrectnessChecks"/> is changed from default before this internal
        /// class is ever referenced from any code path it will have default
        /// value and this field will be JIT compile-time constant. If set to false, checks such as
        /// `if(AdditionalCorrectnessChecks.Enabled)` will be
        /// completely eliminated by JIT.
        /// </remarks>
#if DEBUG
        public const bool Enabled = true;
#else
#if ADDITIONAL_CHECKS
        public static readonly bool Enabled = Settings._doAdditionalCorrectnessChecks;
#else
        public const bool Enabled = false;
#endif
#endif
    }

    internal static class LeaksDetection
    {
#if DEBUG
        public static readonly bool Enabled = true;
#else
        public static readonly bool Enabled = Settings._doDetectBufferLeaks;
#endif
    }

    internal static class NativeAllocatorSettings
    {
        internal static readonly bool Initialized = Init();

        private static bool Init()
        {
            Mem.OptionSetEnabled(Mem.Option.EagerCommit, true);
            Mem.OptionSetEnabled(Mem.Option.LargeOsPages, true);
            Mem.OptionSetEnabled(Mem.Option.ResetDecommits, true);
            Mem.OptionSetEnabled(Mem.Option.PageReset, true);
            Mem.OptionSetEnabled(Mem.Option.SegmentReset, true);
            Mem.OptionSetEnabled(Mem.Option.AbandonedPageReset, true);
            Mem.OptionSetEnabled(Mem.Option.EagerRegionCommit, false); // TODO see mimalloc comment on this. Should be true on servers
            Mem.OptionSetEnabled(Mem.Option.ReserveHugeOsPages, true);
            return true;
        }
    }

    /// <summary>
    /// Global settings.
    /// </summary>
    public static class Settings
    {
        internal const int PAGE_SIZE = 4096;

        /// <summary>
        /// 128 bytes cache line already exists in some CPUs.
        /// </summary>
        /// <remarks>
        /// Also "the spatial prefetcher strives to keep pairs of cache lines in the L2 cache."
        /// https://stackoverflow.com/questions/29199779/false-sharing-and-128-byte-alignment-padding
        /// </remarks>
        internal const int SAFE_CACHE_LINE = 128;

        internal const int AVX512_ALIGNMENT = 64;

        internal const int LARGE_BUFFER_LIMIT = 128 * 1024;

        /// <summary>
        /// Multiply pool buffer count by this scale factor when
        /// buffer size is equal to <see cref="LARGE_BUFFER_LIMIT"/>,
        /// so that there are more most frequently used buffers pooled.
        /// </summary>
        internal const int LARGE_BUFFER_POOL_SCALE = 64;

        internal const int MIN_POOLED_BUFFER_LEN = 16;

        /// <summary>
        /// Get or set default compression method: BinaryLz4 (default) or BinaryZstd).
        /// </summary>
        public static SerializationFormat DefaultSerializationFormat { get; set; } = SerializationFormat.JsonGZip;

        // TODO when/if used often benchmark its effect and if significant then set default to false
        // ReSharper disable once NotAccessedField.Local
        internal static bool _doAdditionalCorrectnessChecks = false;

        internal static bool _doDetectBufferLeaks = false;

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

        /// <summary>
        /// When enabled buffers not returned to a pool will throw.
        /// Kills performance and produces a lot of garbage. Only
        /// for diagnostics.
        /// </summary>
        public static bool DoDetectBufferLeaks
        {
            get => LeaksDetection.Enabled;
            set
            {
                _doDetectBufferLeaks = value;

                // access it immediately: https://github.com/dotnet/coreclr/issues/2526
                if (LeaksDetection.Enabled)
                {
                    Trace.TraceInformation("Enabled buffer leaks detection.");
                }
            }
        }

        // See e.g. https://gregoryszorc.com/blog/2017/03/07/better-compression-with-zstandard/
        internal static int _lz4CompressionLevel = 5;

        internal static int _zstdCompressionLevel = 1;

        internal static int _zlibCompressionLevel = 3;

        // ReSharper disable once InconsistentNaming
        /// <summary>
        /// Default is 5.
        /// </summary>
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

        /// <summary>
        /// Default is 1. For many data types the default value
        /// increases IO throughput, i.e. time spent on both compression
        /// and writing smaller data is less than time spent on writing
        /// uncompressed data. Higher compression level only marginally
        /// improves compression ratio. Even Zstandard authors recommend
        /// using minimal compression level for most use cases.
        /// Please measure time and compression ratio with different
        /// compression levels before increasing the default level.
        /// </summary>
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

        /// <summary>
        /// Default is 3.
        /// </summary>
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
        /// If <see cref="BinarySerializer{T}.SizeOf(T,out Spreads.Buffers.RetainedMemory{byte})"/> does not return a temp buffer
        /// then force using a temp buffer for destination of <see cref="BinarySerializer{T}.Write"/>
        /// method. This is slower but useful to ensure correct implementation of
        /// a custom <see cref="BinarySerializer{T}"/>.
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

        /// <summary>
        /// This only affects known types for which interpolation search works correctly
        /// and is expected to be significantly faster. No reason to set it to false.
        /// </summary>
        internal const bool UseInterpolatedSearchForKnownTypes = true;

        public static int SharedSpinLockNotificationPort = 0;

        internal static Action? ZeroValueNotificationCallback = null;

        internal static int? PrivateMemoryPerCorePoolSize = 16 * 1024 / 128;
    }
}