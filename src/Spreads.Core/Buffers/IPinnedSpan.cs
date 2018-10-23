// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;

namespace Spreads.Buffers
{
    /// <summary>
    /// Helper interface to simplify work with pinned/native memory without relying only on Span.
    /// </summary>
    /// <typeparam name="T">Unmanaged struct yet without C# 7.3 `unmanaged` constraint.</typeparam>
    public unsafe interface IPinnedSpan<T> : IDisposable //where T : struct
    {
        /// <summary>
        /// Pointer to the first element.
        /// </summary>
        void* Data { get; }

        /// <summary>
        /// Number of items (not bytes when T != byte)
        /// </summary>
        int Length { get; }

        ref T this[int index] { get; }

        bool IsEmpty { get; }

        // These two below could have default interface impl via Pointer + Length + Unsafe.SizeOf<T>.

        Span<T> Span { get; }

        DirectBuffer DirectBuffer { get; }
    }
}
