// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

#define ADDITIONAL_CHECKS

using System;
using System.Diagnostics;

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

        // TODO when/if used often benchmark its effect and if significant then set default to false
        // ReSharper disable once NotAccessedField.Local
        internal static bool _doAdditionalCorrectnessChecks;

        internal static bool _doDetectBufferLeaks;

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

        /// <summary>
        /// Set this property before accessing ChaosMonkey or types that use it.
        /// When false then this value is stored in static readonly field and is
        /// optimized away by JIT without any performance impact.
        /// </summary>
        public static bool EnableChaosMonkey { get; set; } = false;

        public static int AtomicCounterPoolBucketSize { get; set; } = 1024;

        public static bool LogMemoryPoolEvents { get; set; } = false;

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
