// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Spreads.Collections;

namespace Spreads
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly unsafe struct RuntimeTypeInfo
    {
        public readonly Type Type;
        public readonly RuntimeTypeId RuntimeTypeId;
        /// <summary>
        /// Size of a type in an array. See <see cref="Unsafe.SizeOf{T}"/>
        /// </summary>
        public readonly short ElemSize;
        /// <summary>
        /// Positive when a type has fixed binary size.
        /// </summary>
        public readonly short FixedSize;
        public readonly bool IsReferenceOrContainsReferences;
        internal readonly delegate*<in Vec, int, object> DangerousGetObjectDelegate;

        internal RuntimeTypeInfo(Type type,
            RuntimeTypeId runtimeTypeId,
            short elemSize,
            short fixedSize,
            bool isReferenceOrContainsReferences,
            delegate*<in Vec, int, object> dangerousGetObjectDelegate)
        {
            Type = type;
            RuntimeTypeId = runtimeTypeId;
            ElemSize = elemSize;
            FixedSize = fixedSize;
            IsReferenceOrContainsReferences = isReferenceOrContainsReferences;
            DangerousGetObjectDelegate = dangerousGetObjectDelegate;
        }
    }
}
