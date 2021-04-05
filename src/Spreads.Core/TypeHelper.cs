// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Spreads.Collections;
using Spreads.Collections.Concurrent;
using Spreads.DataTypes;

using Spreads.Utils;
using static System.Runtime.CompilerServices.Unsafe;
// ReSharper disable HeapView.ObjectAllocation.Possible

#pragma warning disable HAA0101 // Array allocation for params parameter

namespace Spreads
{
    public static class TypeHelper
    {
        internal static readonly AppendOnlyStorage<RuntimeTypeInfo> RuntimeTypeInfoStorage = new();
        internal static readonly ConcurrentDictionary<Type, int> RuntimeTypeInfoIndexLookup = new();

        public static readonly nint StringOffset = CalculateStringOffset();
        public static readonly int StringOffsetInt = (int)StringOffset;

        public static readonly nint ArrayOffset = TypeHelper<object>.ArrayOffset;
        public static readonly int ArrayOffsetInt = (int)ArrayOffset;

        private static nint CalculateStringOffset()
        {
            string sampleString = "a";
            unsafe
            {
                fixed (char* pSampleString = sampleString)
                {
                    return ByteOffset(ref As<Box<char>>(sampleString)!.Value, ref AsRef<char>(pSampleString));
                }
            }
        }

        // ReSharper disable once UnusedMember.Local Use by reflection
        private static RuntimeTypeId GetRuntimeTypeIdReflection<T>()
        {
            return TypeHelper<T>.GetRuntimeVecInfo().RuntimeTypeId;
        }

        [SuppressMessage("ReSharper", "InconsistentlySynchronizedField")]
        public static ref readonly RuntimeTypeInfo GetRuntimeTypeInfo(Type ty)
        {
            if (RuntimeTypeInfoIndexLookup.TryGetValue(ty, out int idx))
                return ref RuntimeTypeInfoStorage[idx];

            lock (RuntimeTypeInfoStorage)
            {
                if (RuntimeTypeInfoIndexLookup.TryGetValue(ty, out idx))
                {
                    return ref RuntimeTypeInfoStorage[idx];
                }

                RuntimeTypeId GetRuntimeTypeInfoViaReflection()
                {
                    MethodInfo mi = typeof(TypeHelper).GetMethod("GetRuntimeTypeIdReflection", BindingFlags.Static | BindingFlags.NonPublic)!;
                    ThrowHelper.Assert(mi != null);
                    MethodInfo genericMi = mi.MakeGenericMethod(ty);
                    var runtimeTypeId = (RuntimeTypeId) genericMi.Invoke(null, new object[] { })!;
                    return runtimeTypeId;
                }

                return ref GetRuntimeTypeInfo(GetRuntimeTypeInfoViaReflection());

            }
        }

        public static ref readonly RuntimeTypeInfo GetRuntimeTypeInfo(RuntimeTypeId typeId)
        {
            return ref RuntimeTypeInfoStorage[typeId.TypeId - 1];
        }

        [Obsolete("This must go to TEH")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static short GetFixedSize(Type ty)
        {
            return GetRuntimeTypeInfo(ty).FixedSize;
        }
    }

    [SuppressMessage("ReSharper", "StaticMemberInGenericType")]
    public static class TypeHelper<T>
    {
        private static readonly ILogger _logger = Logger.Factory.CreateLogger(typeof(TypeHelper<T>));

        /// <summary>
        /// Returns a positive number for primitive type, well known fixed size types
        /// and pinnable structs with <see cref="BuiltInDataTypeAttribute"/> and
        /// <see cref="BuiltInDataTypeAttribute.BlittableSize"/> set to actual size.
        /// For other types returns -1.
        /// </summary>
        public static readonly short FixedSize = InitFixedSizeSafe();

        public static readonly bool IsFixedSize = FixedSize > 0;

        public static readonly short PinnedSize = GetPinnedSize();

        /// <summary>
        /// True if T[] could be pinned in memory via GCHandle.
        /// </summary>
        public static readonly bool IsPinnable = PinnedSize > 0;

        public static readonly nint ArrayOffset = CalculateArrayOffset();
        public static readonly int ArrayOffsetInt = (int)ArrayOffset;

        public static readonly nint ArrayOffsetMinus1Item = CalculateArrayOffset() - SizeOf<T>();
        public static readonly int ArrayOffsetIntMinus1Item = (int)ArrayOffsetMinus1Item;

        public static readonly RuntimeTypeInfo RuntimeTypeInfo = GetRuntimeVecInfo();

        public static readonly RuntimeTypeId RuntimeTypeId = RuntimeTypeInfo.RuntimeTypeId;

        public static readonly bool IsReferenceOrContainsReferences = IsReferenceOrContainsReferencesImpl();

        private static nint CalculateArrayOffset()
        {
            var oneArray = new T[1];
            IntPtr offset = ByteOffset(ref As<Box<T>>(oneArray)!.Value, ref oneArray[0]);
            if(_logger.IsEnabled(LogLevel.Trace))
                _logger.LogTrace($"ArrayOffset for type {typeof(T).FullName} is {(int)offset}");
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe RuntimeTypeInfo GetRuntimeVecInfo()
        {
            if (TypeHelper.RuntimeTypeInfoIndexLookup.TryGetValue(typeof(T), out int idx))
                return TypeHelper.RuntimeTypeInfoStorage[idx];

            lock (TypeHelper.RuntimeTypeInfoStorage)
            {
                if (TypeHelper.RuntimeTypeInfoIndexLookup.TryGetValue(typeof(T), out idx))
                    return TypeHelper.RuntimeTypeInfoStorage[idx];

                var typeInfo = new RuntimeTypeInfo (
                    typeof(T),
                    // One based so that the default value is invalid
                    (RuntimeTypeId)(TypeHelper.RuntimeTypeInfoStorage.Count + 1),
                    checked((short)SizeOf<T>()),
                    FixedSize,
                    IsReferenceOrContainsReferences,
                    &Vec.DangerousGetObject<T>
                );

                idx = TypeHelper.RuntimeTypeInfoStorage.Add(typeInfo);
                TypeHelper.RuntimeTypeInfoIndexLookup[typeof(T)] = idx;

                return typeInfo;
            }
        }

        internal static bool IsReferenceOrContainsReferencesImpl()
        {
#if HAS_ISREF
            return RuntimeHelpers.IsReferenceOrContainsReferences<T>();
#else
            return IsReferenceOrContainsReferencesManual(typeof(T));
#endif
        }

        internal static bool IsReferenceOrContainsReferencesManual(Type type)
        {
            if (type.GetTypeInfo().IsPrimitive) // This is hopefully the common case. All types that return true for this are value types w/out embedded references.
                return false;

            if (!type.GetTypeInfo().IsValueType)
                return true;

            // If type is a Nullable<> of something, unwrap it first.
            Type? underlyingNullable = Nullable.GetUnderlyingType(type);
            if (underlyingNullable != null)
                type = underlyingNullable;

            if (type.GetTypeInfo().IsEnum)
                return false;

            foreach (FieldInfo field in type.GetTypeInfo().DeclaredFields)
            {
                if (field.IsStatic)
                    continue;
                if (IsReferenceOrContainsReferencesManual(field.FieldType))
                    return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EnsureFixedSize()
        {
            if (FixedSize > 0)
            {
                return FixedSize;
            }

            ThrowTypeIsNotFixedSize();
            return -1;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowTypeIsNotFixedSize()
        {
            ThrowHelper.ThrowArgumentException(
                $"Type {typeof(T).Name} is not fixed size. Add Spreads.BinarySerialization attribute to explicitly opt-in to treat non-primitive user-defined structs as fixed-size.");
        }

        /// <summary>
        /// CLR definition, we cache it here since ty.IsValueType could be a virtual call on older platforms.
        /// </summary>
        internal static readonly bool IsValueType = typeof(T).IsValueType;

        /// <summary>
        /// Implements <see cref="IDelta{T}"/>
        /// </summary>
        internal static readonly bool IsIDelta = typeof(IDelta<T>).GetTypeInfo().IsAssignableFrom(typeof(T));

        /// <summary>
        /// Distance between two elements of a pinned array.
        /// </summary>
        [DebuggerStepThrough]
        private static short GetPinnedSize()
        {
            try
            {
                var array = new T[2];
                var pinnedArrayHandle = GCHandle.Alloc(array, GCHandleType.Pinned);
                var size = (int)
                    (Marshal.UnsafeAddrOfPinnedArrayElement(array, 1).ToInt64() -
                     Marshal.UnsafeAddrOfPinnedArrayElement(array, 0).ToInt64());
                pinnedArrayHandle.Free();
                // Type helper works only with types that could be pinned in arrays
                // Here we just cross-check, happens only in static constructor
                var unsafeSize = SizeOf<T>();
                if (unsafeSize != size)
                    Environment.FailFast("Pinned and unsafe sizes differ.");
                return (short)size;
            }
            catch
            {
                return -1;
            }
        }

        private static short InitFixedSizeSafe()
        {
            if (!BitConverter.IsLittleEndian)
                Environment.FailFast("Spreads library only supports a little-endian architecture.");

            try
            {
                short size = InitFixedSize();
                return size;
            }
            catch
            {
                return -1;
            }
        }

        private static short InitFixedSize()
        {
            // Auto layout for this is PITA
            if (typeof(T) == typeof(DateTime))
            {
                return 8;
            }

            // Decimal is pinnable but not primitive, GetPinnedSize fails on it
            if (typeof(T) == typeof(decimal))
            {
                return 16;
            }

            var builtInDataTypeAttribute = BuiltInDataTypeAttribute.GetSerializationAttribute(typeof(T));

            // by this line the type is not blittable

            short pinnedSize = GetPinnedSize();
            if (pinnedSize > 0)
            {
                if (typeof(T).GetTypeInfo().IsPrimitive)
                    return pinnedSize;

                // for a non-primitive type to be blittable, it must have an attribute

                if (builtInDataTypeAttribute != null && builtInDataTypeAttribute.BlittableSize > 0)
                {
                    if (pinnedSize != builtInDataTypeAttribute.BlittableSize)
                    {
                        ThrowHelper.ThrowInvalidOperationException(
                            $"Size of type {typeof(T).FullName} defined in BinarySerialization attribute {builtInDataTypeAttribute.BlittableSize} differs from calculated size {pinnedSize}.");
                    }

                    return pinnedSize;
                }

                if (builtInDataTypeAttribute != null && builtInDataTypeAttribute.PreferBlittable)
                    return pinnedSize;
            }

            return -1;
        }


    }
}
