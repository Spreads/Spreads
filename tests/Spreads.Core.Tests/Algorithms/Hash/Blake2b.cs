// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// Implementation is refactored and optimized from: Blake2Fast https://github.com/saucecontrol/Blake2Fast/
// Copyright (c) 2018 Clinton Ingram
// The MIT License

using System;
using System.Runtime.CompilerServices;
using Spreads.Buffers;
using Spreads.Core.Tests.Algorithms.Hash.BLAKE2b;

namespace Spreads.Core.Tests.Algorithms.Hash
{
    public static class Blake2b
    {    
        /// <summary>The default hash digest length in bytes.  For BLAKE2b, this value is 64.</summary>
        public const int DefaultDigestLength = Blake2bContext.HashBytes;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ComputeAndWriteHash(DirectBuffer input, Span<byte> output) => ComputeAndWriteHash(DefaultDigestLength, default, input, output);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ComputeAndWriteHash(int digestLength, DirectBuffer input, Span<byte> output) => ComputeAndWriteHash(digestLength, default, input, output);

        /// <summary>Perform an all-at-once BLAKE2b hash computation and write the hash digest to <paramref name="output" />.</summary>
        /// <remarks>If you have all the input available at once, this is the most efficient way to calculate the hash.</remarks>
        /// <param name="key">0 to 64 bytes of input for initializing a keyed hash.</param>
        /// <param name="input">The message bytes to hash.</param>
        /// <param name="output">Destination buffer into which the hash digest is written.  The buffer must have a capacity of at least <see cref="DefaultDigestLength"/>(64) /> bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ComputeAndWriteHash(ReadOnlySpan<byte> key, DirectBuffer input, Span<byte> output) => ComputeAndWriteHash(DefaultDigestLength, key, input, output);

        /// <summary>Perform an all-at-once BLAKE2b hash computation and write the hash digest to <paramref name="output" />.</summary>
        /// <remarks>If you have all the input available at once, this is the most efficient way to calculate the hash.</remarks>
        /// <param name="digestLength">The hash digest length in bytes.  Valid values are 1 to 64.</param>
        /// <param name="key">0 to 64 bytes of input for initializing a keyed hash.</param>
        /// <param name="input">The message bytes to hash.</param>
        /// <param name="output">Destination buffer into which the hash digest is written.  The buffer must have a capacity of at least <paramref name="digestLength" /> bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ComputeAndWriteHash(int digestLength, ReadOnlySpan<byte> key, DirectBuffer input, Span<byte> output)
        {
            if (output.Length < digestLength)
            { FailBadOutputLength(digestLength); }

            var ctx = default(Blake2bContext);
            ctx.InitUpdateHash(input, output, digestLength, key);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FailBadOutputLength(int digestLength)
        {
            throw new ArgumentException($"Output buffer must have a capacity of at least {digestLength} bytes.", "output");
        }

        public static Blake2bContext CreateContext() => CreateIncrementalHasher(DefaultDigestLength, default(ReadOnlySpan<byte>));

        public static Blake2bContext CreateIncrementalHasher(int digestLength) => CreateIncrementalHasher(digestLength, default(ReadOnlySpan<byte>));

        public static Blake2bContext CreateIncrementalHasher(ReadOnlySpan<byte> key) => CreateIncrementalHasher(DefaultDigestLength, key);

        /// <summary>Create and initialize an incremental BLAKE2b hash computation.</summary>
        /// <remarks>If you will receive the input in segments rather than all at once, this is the most efficient way to calculate the hash.</remarks>
        /// <param name="digestLength">The hash digest length in bytes.  Valid values are 1 to 64.</param>
        /// <param name="key">0 to 64 bytes of input for initializing a keyed hash.</param>
        /// <returns>An <see cref="Blake2bContext" /> for updating and finalizing the hash.</returns>
        public static Blake2bContext CreateIncrementalHasher(int digestLength, ReadOnlySpan<byte> key)
        {
            var ctx = default(Blake2bContext);
            ctx.Init(digestLength, key);
            return ctx;
        }
    }
}
