// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.DataTypes;
using Spreads.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Spreads.Serialization.Serializers;
using static System.Runtime.CompilerServices.Unsafe;

#pragma warning disable HAA0101 // Array allocation for params parameter

namespace Spreads.Serialization
{
    internal delegate int FromPtrDelegate(IntPtr ptr, out object value);

    internal delegate int ToPtrDelegate(object value, IntPtr destination, MemoryStream ms = null, SerializationFormat compression = SerializationFormat.Binary);

    internal delegate int SizeOfDelegate(object value, out MemoryStream memoryStream, SerializationFormat compression = SerializationFormat.Binary);

    public static class TypeHelper
    {
        private static readonly Dictionary<Type, FromPtrDelegate> FromPtrDelegateCache = new Dictionary<Type, FromPtrDelegate>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static FromPtrDelegate GetFromPtrDelegate(Type ty)
        {
            FromPtrDelegate temp;
            if (FromPtrDelegateCache.TryGetValue(ty, out temp)) return temp;
            var mi = typeof(TypeHelper).GetMethod("ReadObject", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            // ReSharper disable once PossibleNullReferenceException

            var genericMi = mi.MakeGenericMethod(ty);

            temp = (FromPtrDelegate)genericMi.CreateDelegate(typeof(FromPtrDelegate));
            FromPtrDelegateCache[ty] = temp;
            return temp;
        }

        private static readonly Dictionary<Type, ToPtrDelegate> ToPtrDelegateCache = new Dictionary<Type, ToPtrDelegate>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ToPtrDelegate GetToPtrDelegate(Type ty)
        {
            ToPtrDelegate temp;
            if (ToPtrDelegateCache.TryGetValue(ty, out temp)) return temp;
            var mi = typeof(TypeHelper).GetMethod("WriteObject", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            // ReSharper disable once PossibleNullReferenceException
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
            // ReSharper disable once PossibleNullReferenceException
            var genericMi = mi.MakeGenericMethod(ty);
            temp = (SizeOfDelegate)genericMi.CreateDelegate(typeof(SizeOfDelegate));
            SizeOfDelegateCache[ty] = temp;
            return temp;
        }

        private static readonly Dictionary<Type, int> SizeDelegateCache = new Dictionary<Type, int>();

        // used by reflection below
        // ReSharper disable once UnusedMember.Local
        private static int Size<T>()
        {
            return TypeHelper<T>.FixedSize;
        }

        [Obsolete("This must go to TEH")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetSize(Type ty)
        {
            int temp;
            if (SizeDelegateCache.TryGetValue(ty, out temp)) return temp;
            var mi = typeof(TypeHelper).GetMethod("Size", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            // ReSharper disable once PossibleNullReferenceException
            var genericMi = mi.MakeGenericMethod(ty);
            temp = (int)genericMi.Invoke(null, new object[] { });
            SizeDelegateCache[ty] = temp;
            return temp;
        }
    }

    public static class TypeHelper<T>
    {
        // Do not use static ctor in any critical paths: https://github.com/Spreads/Spreads/issues/66
        // static TypeHelper() { }

        internal static IBinarySerializer<T> BinarySerializer;

        // ReSharper disable once StaticMemberInGenericType
        internal static bool IsInternalBinarySerializer; // TODO set to true for tuples and arrays

        /// <summary>
        /// Returns a positive number for primitive type, well known fixed size types
        /// and pinnable structs with <see cref="BinarySerializationAttribute"/> and
        /// <see cref="BinarySerializationAttribute.BlittableSize"/> set to actual size.
        /// For other types returns -1.
        /// </summary>
        // ReSharper disable once StaticMemberInGenericType
        public static readonly int FixedSize = InitFixedSizeSafe();

        // TODO this method must comply with xml doc
        // TODO ContainsReferences, it does what the name says. Auto layout and composites could have no references
        //      but have FixedSize <= 0 with out definitions. For skipping buffer cleaning we need !ContainsReferences

        // ReSharper disable once StaticMemberInGenericType
        public static readonly bool IsFixedSize = FixedSize > 0;

        // ReSharper disable once StaticMemberInGenericType
        public static readonly bool HasBinarySerializer = FixedSize <= 0 && BinarySerializer != null;

        // ReSharper disable once StaticMemberInGenericType
        public static readonly short PinnedSize = GetPinnedSize();

        /// <summary>
        /// True if T[] could be pinned in memory via GCHandle.
        /// </summary>
        // ReSharper disable once StaticMemberInGenericType
        public static readonly bool IsPinnable = PinnedSize > 0;

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
            ThrowHelper.ThrowArgumentException($"Type {typeof(T).Name} is not fixed size. Add Spreads.BinarySerialization attribute to explicitly opt-in to treat non-primitive user-defined structs as fixed-size.");
        }

        /// <summary>
        /// CLR definition, we cache it here since ty.IsValueType is a virtual call
        /// </summary>
        [Obsolete] // TODO review if we will need this
        internal static readonly bool IsValueType = typeof(T).GetTypeInfo().IsValueType;

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
                if (unsafeSize != size) { ThrowHelper.FailFast("Pinned and unsafe sizes differ!"); }
                return (short)size;
            }
            catch
            {
                return -1;
            }
        }

        private static int InitFixedSizeSafe()
        {
            // Probably this method is always called first when endianness matters.
            // Not that we expect that ever happen in reality...
            if (!BitConverter.IsLittleEndian)
            {
                Environment.FailFast("Spreads library supports only little-endian architectures.");
            }

            // TODO Do not wrap in try/catch attribute validation
            // We failed fast initially but now throw. Attribute errors should be testable
            // and crash application
            try
            {
                var size = InitFixedSize();
                if (size > 255)
                {
                    size = -1;
                }

                // NB do not support huge blittable type
                return size;
            }
            catch
            {
                return -1;
            }
        }

        private static int InitFixedSize()
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

            var bsAttr = BinarySerializationAttribute.GetSerializationAttribute(typeof(T));

            // by this line the type is not blittable

            IBinarySerializer<T> serializer = null;
            var isInternalSerializer = false;

            if (bsAttr != null && bsAttr.SerializerType != null)
            {
                if (!typeof(IBinarySerializer<T>).IsAssignableFrom(bsAttr.SerializerType))
                {
                    ThrowHelper.ThrowInvalidOperationException($"SerializerType `{bsAttr.SerializerType.FullName}` in BinarySerialization attribute does not implement IBinaryConverter<T> for the type `{typeof(T).FullName}`");
                }

                try
                {
                    serializer = (IBinarySerializer<T>)Activator.CreateInstance(bsAttr.SerializerType);
                }
                catch
                {
                    ThrowHelper.ThrowInvalidOperationException($"SerializerType `{bsAttr.SerializerType.FullName}` must have a parameterless constructor.");
                }
            }

            // NB we try to check interface as a last step, because some generic types
            // could implement IBinaryConverter<T> but still be blittable for certain types,
            // e.g. DateTime vs long in PersistentMap<K,V>.Entry
            //if (tmp is IBinaryConverter<T>) {
            if (typeof(IBinarySerializer<T>).IsAssignableFrom(typeof(T)))
            {
                if (serializer != null)
                {
                    ThrowHelper.ThrowInvalidOperationException($"IBinarySerializer `{serializer.GetType().FullName}` was already set via BinarySerialization attribute. The type `{typeof(T).FullName}` should not implement IBinaryConverter<T> interface or the attribute should not include SerializerType property.");
                }
                try
                {
                    serializer = (IBinarySerializer<T>)Activator.CreateInstance<T>();
                }
                catch
                {
                    ThrowHelper.ThrowInvalidOperationException($"Type T ({typeof(T).FullName}) implements IBinaryConverter<T> and must have a parameterless constructor.");
                }
                // ReSharper disable once PossibleNullReferenceException
                if (serializer.SerializerVersion <= 0)
                {
                    ThrowHelper.ThrowInvalidOperationException("User-defined IBinaryConverter<T> implementation for a type T should have a positive version.");
                }
            }

#if SPREADS
            // TODO synchronize with TypeEnumHelper's GetTypeEnum and CreateTypeInfo

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(FixedArray<>))
            {
                var elementType = typeof(T).GetGenericArguments()[0];
                var elementSize = TypeHelper.GetSize(elementType);
                if (elementSize > 0)
                { // only for blittable types
                    serializer = (IBinarySerializer<T>)FixedArraySerializerFactory.Create(elementType);
                    isInternalSerializer = true;
                }
            }

            #region Tuple2

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                var gArgs = typeof(T).GetGenericArguments();
                var serializerTmp = (IBinarySerializer<T>)KvpSerializerFactory.Create(gArgs[0], gArgs[1]);
                if (serializerTmp.FixedSize > 0)
                {
                    serializer = serializerTmp;
                    isInternalSerializer = true;
                }
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,>))
            {
                var gArgs = typeof(T).GetGenericArguments();

                var serializerTmp = (IBinarySerializer<T>)ValueTuple2SerializerFactory.Create(gArgs[0], gArgs[1]);
                if (serializerTmp.FixedSize > 0)
                {
                    serializer = serializerTmp;
                    isInternalSerializer = true;
                }
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(Tuple<,>))
            {
                var gArgs = typeof(T).GetGenericArguments();
                var serializerTmp = (ITupleSerializer<T>)Tuple2SerializerFactory.Create(gArgs[0], gArgs[1]);
                if (serializerTmp.IsBinary)
                {
                    serializer = serializerTmp;
                    isInternalSerializer = true;
                }
            }

            #endregion Tuple2

            #region Tuple3

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(TaggedKeyValue<,>))
            {
                var gArgs = typeof(T).GetGenericArguments();
                var serializerTmp = (IBinarySerializer<T>)TaggedKeyValueByteSerializerFactory.Create(gArgs[0], gArgs[1]);
                if (serializerTmp.FixedSize > 0)
                {
                    serializer = serializerTmp;
                    isInternalSerializer = true;
                }
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,,>))
            {
                var gArgs = typeof(T).GetGenericArguments();

                var serializerTmp = (IBinarySerializer<T>)ValueTuple3SerializerFactory.Create(gArgs[0], gArgs[1], gArgs[2]);
                if (serializerTmp.FixedSize > 0)
                {
                    serializer = serializerTmp;
                    isInternalSerializer = true;
                }
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(Tuple<,,>))
            {
                var gArgs = typeof(T).GetGenericArguments();
                var serializerTmp = (IBinarySerializer<T>)Tuple3SerializerFactory.Create(gArgs[0], gArgs[1], gArgs[2]);
                if (serializerTmp.FixedSize > 0)
                {
                    serializer = serializerTmp;
                    isInternalSerializer = true;
                }
            }

            #endregion Tuple3

            if (typeof(T).IsArray)
            {
                var elementType = typeof(T).GetElementType();
                var elementSize = TypeHelper.GetSize(elementType);
                if (elementSize > 0)
                { // only for blittable types
                    serializer = (IBinarySerializer<T>)ArraySerializerFactory.Create(elementType);
                    isInternalSerializer = true;
                }
            }

            if (typeof(T).IsGenericType
                && typeof(T).GetGenericTypeDefinition() == typeof(Collections.Internal.VectorStorage<>))
            {
                // TODO TEH by type
                var elementType = typeof(T).GenericTypeArguments[0];
                var elementSize = TypeHelper.GetSize(elementType);
                if (elementSize > 0)
                { // only for blittable types
                    serializer = (IBinarySerializer<T>)Collections.Internal.VectorStorageSerializerFactory.Create(elementType);
                    isInternalSerializer = true;
                }
            }

            // Do not add Json converter as fallback, it is not "binary", it implements the interface for
            // simpler implementation in BinarySerializer and fallback happens there
#endif
            if (serializer != null)
            {
                BinarySerializer = serializer;
                IsInternalBinarySerializer = isInternalSerializer;
                return -1;
            }

            var pinnedSize = GetPinnedSize();
            if (pinnedSize > 0)
            {
                if (typeof(T).GetTypeInfo().IsPrimitive)
                {
                    return pinnedSize;
                }

                // for a non-primitive type to be blittable, it must have an attribute

                var hasSizeAttribute = false;
                if (bsAttr != null && bsAttr.BlittableSize > 0)
                {
                    if (pinnedSize != bsAttr.BlittableSize)
                    {
                        ThrowHelper.ThrowInvalidOperationException(
                            $"Size of type {typeof(T).FullName} defined in BinarySerialization attribute {bsAttr.BlittableSize} differs from calculated size {pinnedSize}.");
                    }

                    if (bsAttr.SerializerType != null)
                    {
                        ThrowHelper.ThrowInvalidOperationException(
                            $"Cannot define BlittableSize and ConverterType at the same time in BinarySerialization attribute of type {typeof(T).FullName}.");
                    }
                    hasSizeAttribute = true;
                }
                else
                {
                    var sla = BinarySerializationAttribute.GetStructLayoutAttribute(typeof(T));
                    if (sla != null && sla.Size > 0 && sla.Value != LayoutKind.Auto && Settings.UseStructLayoutSizeAsBlittableSize)
                    {
                        if (pinnedSize != sla.Size)
                        {
                            ThrowHelper.ThrowInvalidOperationException(
                                $"Size of type {typeof(T).Name} defined in StructLayoutAttribute {sla.Size} differs from calculated size {pinnedSize} or layout is set to LayoutKind.Auto.");
                        }
                        hasSizeAttribute = true;
                    }
                }

                if (hasSizeAttribute) // TODO after IBS resolution
                {
                    if (typeof(IBinarySerializer<T>).IsAssignableFrom(typeof(T)))
                    {
                        // NB: this makes no sense, because blittable is version 0, if we have any change
                        // to struct layout later, we won't be able to work with version 0 anymore
                        // and will lose ability to work with old values.
                        ThrowHelper.ThrowInvalidOperationException($"Blittable types must not implement IBinaryConverter<T> interface.");
                    }
                    return pinnedSize;
                }

                if (bsAttr != null && bsAttr.PreferBlittable)
                {
                    // NB: here it is OK to have an interface, we just opt-in for blittable
                    // when we know it won't change, e.g. generic struct with fixed fields (KV<K,V>, DictEntry<K,V>, Message<T>, etc.)
                    // usually those types are internal
                    return pinnedSize;
                }
            }

            return -1;
        }

        // TODO Serializer versioning is not implemented

        internal static byte SerializerVersion
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => BinarySerializer?.SerializerVersion ?? 0;
        }

        internal static void RegisterSerializer(IBinarySerializer<T> serializer,
            bool overrideExisting = false)
        {
            if (serializer == null) { throw new ArgumentNullException(nameof(serializer)); }
            if (FixedSize > 0) { throw new InvalidOperationException("Cannot register a custom converter for pinnable types"); }

            // NB TypeHelper is internal, we could provide some hooks later e.g. for char or bool
            if (serializer.SerializerVersion == 0 || serializer.SerializerVersion > 3)
            {
                ThrowHelper.ThrowArgumentException("User-implemented serializer version must be in the range 1-3.");
            }

            if (HasBinarySerializer && !overrideExisting)
            {
                ThrowHelper.ThrowInvalidOperationException(
                    $"Type {typeof(T)} already implements IBinarySerializer<{typeof(T).Name}> interface. Use versioning to add a new converter (not supported yet)");
            }

            if (IsFixedSize) // TODO this may be possible, but don't care for now
            {
                Environment.FailFast($"Blittable types must not have IBinaryConverter<T>.");
            }
            BinarySerializer = serializer;
        }
    }
}