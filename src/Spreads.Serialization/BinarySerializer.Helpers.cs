// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Spreads.Serialization.Serializers;

namespace Spreads.Serialization
{
    internal delegate int FromPtrDelegate(IntPtr ptr, out object value);

    internal delegate int ToPtrDelegate(object value, IntPtr destination, MemoryStream? ms = null, SerializationFormat compression = SerializationFormat.Binary);

    internal delegate int SizeOfDelegate(object value, out MemoryStream? memoryStream, SerializationFormat compression = SerializationFormat.Binary);


    public static partial class BinarySerializer
    {
        private static readonly Dictionary<Type, FromPtrDelegate> _fromPtrDelegateCache = new();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static FromPtrDelegate GetFromPtrDelegate(Type ty)
        {
            FromPtrDelegate temp;
            if (_fromPtrDelegateCache.TryGetValue(ty, out temp)) return temp;
            var mi = typeof(TypeHelper).GetMethod("ReadObject", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            ThrowHelper.Assert(mi != null);
            var genericMi = mi.MakeGenericMethod(ty);

            temp = (FromPtrDelegate)genericMi.CreateDelegate(typeof(FromPtrDelegate));
            _fromPtrDelegateCache[ty] = temp;
            return temp;
        }

        private static readonly Dictionary<Type, ToPtrDelegate> ToPtrDelegateCache = new Dictionary<Type, ToPtrDelegate>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ToPtrDelegate GetToPtrDelegate(Type ty)
        {
            ToPtrDelegate temp;
            if (ToPtrDelegateCache.TryGetValue(ty, out temp)) return temp;
            var mi = typeof(TypeHelper).GetMethod("WriteObject", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            ThrowHelper.AssertFailFast(mi != null);
            var genericMi = mi.MakeGenericMethod(ty);
            temp = (ToPtrDelegate)genericMi.CreateDelegate(typeof(ToPtrDelegate));
            ToPtrDelegateCache[ty] = temp;
            return temp;
        }

        private static readonly Dictionary<Type, SizeOfDelegate> SizeOfDelegateCache = new Dictionary<Type, SizeOfDelegate>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static SizeOfDelegate GetSizeOfDelegate(Type ty)
        {
            SizeOfDelegate temp;
            if (SizeOfDelegateCache.TryGetValue(ty, out temp)) return temp;
            var mi = typeof(TypeHelper).GetMethod("SizeOfObject", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            ThrowHelper.AssertFailFast(mi != null);
            var genericMi = mi.MakeGenericMethod(ty);
            temp = (SizeOfDelegate)genericMi.CreateDelegate(typeof(SizeOfDelegate));
            SizeOfDelegateCache[ty] = temp;
            return temp;
        }
    }
}
