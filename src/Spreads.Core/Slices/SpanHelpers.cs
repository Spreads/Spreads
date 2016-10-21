// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Spreads.Slices
{
    /// <summary>
    /// Internal helper methods used for implementing Span.
    /// </summary>
    static class SpanHelpers
    {
        /// <summary>
        /// The offset, in bytes, to the first character in a string.
        /// </summary>
        internal static readonly int OffsetToStringData =
            System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData;
    }

    /// <summary>
    /// Internal helper methods used for implementing Span.
    /// </summary>
    static class SpanHelpers<T>
    {
        /// <summary>
        /// The offset, in bytes, to the first element of an array of type T.
        /// </summary>
        internal static readonly int OffsetToArrayData =
            PtrUtils.ElemOffset<T>(new T[1]);
    }
}

