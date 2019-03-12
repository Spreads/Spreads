// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.DataTypes;
using Spreads.Native;

#if SPREADS

using Spreads.Serialization.Serializers;

#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static System.Runtime.CompilerServices.Unsafe;

#pragma warning disable HAA0101 // Array allocation for params parameter

namespace Spreads.Serialization
{
    internal delegate int FromPtrDelegate(IntPtr ptr, out object value);

    internal delegate int ToPtrDelegate(object value, IntPtr destination, MemoryStream ms = null, SerializationFormat compression = SerializationFormat.Binary);

    internal delegate int SizeOfDelegate(object value, out MemoryStream memoryStream, SerializationFormat compression = SerializationFormat.Binary);

    internal static class TypeHelper
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

        internal static readonly BinarySerializer<T> TypeSerializer = InitSerializer();

        /// <summary>
        /// Returns a positive number for primitive type, well known fixed size types
        /// and pinnable structs with <see cref="BinarySerializationAttribute"/> and
        /// <see cref="BinarySerializationAttribute.BlittableSize"/> set to actual size.
        /// For other types returns -1.
        /// </summary>
        // ReSharper disable once StaticMemberInGenericType
        public static readonly short FixedSize = InitFixedSizeSafe();

        // TODO this method must comply with xml doc
        // TODO ContainsReferences, it does what the name says. Auto layout and composites could have no references
        //      but have FixedSize <= 0 with out definitions. For skipping buffer cleaning we need !ContainsReferences

        // ReSharper disable once StaticMemberInGenericType
        public static readonly bool IsFixedSize = FixedSize > 0;

        // ReSharper disable once StaticMemberInGenericType
        public static readonly bool HasTypeSerializer = TypeSerializer != null;

        // ReSharper disable once StaticMemberInGenericType
        internal static readonly bool IsTypeSerializerInternal = TypeSerializer is InternalSerializer<T>;

        // ReSharper disable once StaticMemberInGenericType
        internal static readonly DataTypeHeader CustomHeader = InitCustomHeader();

        // internal static readonly int SizeOfDefault = IsFixedSize ? TypeSerializer?.SizeOf(default, null) ?? 0 : 0;

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

        private static short InitFixedSizeSafe()
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
                return size;
            }
            catch
            {
                return -1;
            }
        }

        private static BinarySerializer<T> InitSerializer()
        {
            var bsAttr = BinarySerializationAttribute.GetSerializationAttribute(typeof(T));

            BinarySerializer<T> serializer = null;

            if (bsAttr != null && bsAttr.SerializerType != null)
            {
                if (!typeof(BinarySerializer<T>).IsAssignableFrom(bsAttr.SerializerType))
                {
                    ThrowHelper.ThrowInvalidOperationException($"SerializerType `{bsAttr.SerializerType.FullName}` in BinarySerialization attribute does not implement IBinaryConverter<T> for the type `{typeof(T).FullName}`");
                }

                try
                {
                    serializer = (BinarySerializer<T>)Activator.CreateInstance(bsAttr.SerializerType);
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
            if (typeof(BinarySerializer<T>).IsAssignableFrom(typeof(T)))
            {
                if (serializer != null)
                {
                    ThrowHelper.ThrowInvalidOperationException($"IBinarySerializer `{serializer.GetType().FullName}` was already set via BinarySerialization attribute. The type `{typeof(T).FullName}` should not implement IBinaryConverter<T> interface or the attribute should not include SerializerType property.");
                }
                try
                {
                    serializer = (BinarySerializer<T>)(object)Activator.CreateInstance<T>();
                }
                catch
                {
                    ThrowHelper.ThrowInvalidOperationException($"Type T ({typeof(T).FullName}) implements IBinaryConverter<T> and must have a parameterless constructor.");
                }
            }

#if SPREADS
            // TODO synchronize with TypeEnumHelper's GetTypeEnum and CreateTypeInfo

            if (typeof(T) == typeof(DateTime))
            {
                serializer = (InternalSerializer<T>)(object)DateTimeSerializer.Instance;
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(FixedArray<>))
            {
                var elementType = typeof(T).GetGenericArguments()[0];
                var elementSize = TypeHelper.GetSize(elementType);
                if (elementSize > 0)
                { // only for blittable types
                    serializer = (InternalSerializer<T>)FixedArraySerializerFactory.Create(elementType);
                }
            }

            #region Tuple2

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                var gArgs = typeof(T).GetGenericArguments();
                var serializerTmp = (InternalSerializer<T>)KvpSerializerFactory.Create(gArgs[0], gArgs[1]);
                if (serializerTmp.FixedSize > 0)
                {
                    serializer = serializerTmp;
                }
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,>))
            {
                var gArgs = typeof(T).GetGenericArguments();

                var serializerTmp = (InternalSerializer<T>)ValueTuple2SerializerFactory.Create(gArgs[0], gArgs[1]);
                if (serializerTmp.FixedSize > 0)
                {
                    serializer = serializerTmp;
                    Task.Run(async () =>
                    {
                        await Task.Delay(1);
                        var sizeOf = BinarySerializer.SizeOf<T>(default, out var tempBuf, SerializationFormat.Binary);
                    });
                }
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(Tuple<,>))
            {
                var gArgs = typeof(T).GetGenericArguments();
                var serializerTmp = (InternalSerializer<T>)Tuple2SerializerFactory.Create(gArgs[0], gArgs[1]);
                if (serializerTmp.FixedSize > 0)
                {
                    serializer = serializerTmp;
                }
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetTypeInfo().IsValueType &&
                typeof(T).GetTypeInfo().GetInterfaces()
                    .Any(i => i.IsGenericType
                              && i.GetGenericTypeDefinition() == typeof(ITuple<,,>)
                              && i.GetGenericArguments().Last() == typeof(T)
                        )
                )
            {
                var iTy = typeof(T).GetTypeInfo().GetInterfaces()
                    .First(i => i.IsGenericType
                                && i.GetGenericTypeDefinition() == typeof(ITuple<,,>)
                                && i.GetGenericArguments().Last() == typeof(T)
                    );
                var gArgs = iTy.GetGenericArguments();
                var serializerTmp = (InternalSerializer<T>)InterfaceTuple2SerializerFactory.Create(gArgs[0], gArgs[1], typeof(T));
                if (serializerTmp.FixedSize > 0)
                {
                    serializer = serializerTmp;
                }
            }

            #endregion Tuple2

            #region Tuple3

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(TaggedKeyValue<,>))
            {
                var gArgs = typeof(T).GetGenericArguments();
                var serializerTmp = (InternalSerializer<T>)TaggedKeyValueByteSerializerFactory.Create(gArgs[0], gArgs[1]);
                if (serializerTmp.FixedSize > 0)
                {
                    serializer = serializerTmp;
                }
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,,>))
            {
                var gArgs = typeof(T).GetGenericArguments();

                var serializerTmp = (InternalSerializer<T>)ValueTuple3SerializerFactory.Create(gArgs[0], gArgs[1], gArgs[2]);
                if (serializerTmp.FixedSize > 0)
                {
                    serializer = serializerTmp;
                }
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(Tuple<,,>))
            {
                var gArgs = typeof(T).GetGenericArguments();
                var serializerTmp = (InternalSerializer<T>)Tuple3SerializerFactory.Create(gArgs[0], gArgs[1], gArgs[2]);
                if (serializerTmp.FixedSize > 0)
                {
                    serializer = serializerTmp;
                }
            }

            #endregion Tuple3

            #region Tuple4

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,,,>))
            {
                var gArgs = typeof(T).GetGenericArguments();

                var serializerTmp = (InternalSerializer<T>)ValueTuple4SerializerFactory.Create(gArgs[0], gArgs[1], gArgs[2], gArgs[3]);
                if (serializerTmp.FixedSize > 0)
                {
                    serializer = serializerTmp;
                }
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(Tuple<,,,>))
            {
                var gArgs = typeof(T).GetGenericArguments();
                var serializerTmp = (InternalSerializer<T>)Tuple4SerializerFactory.Create(gArgs[0], gArgs[1], gArgs[2], gArgs[3]);
                if (serializerTmp.FixedSize > 0)
                {
                    serializer = serializerTmp;
                }
            }

            #endregion Tuple4

            #region Tuple5

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,,,,>))
            {
                var gArgs = typeof(T).GetGenericArguments();

                var serializerTmp = (InternalSerializer<T>)ValueTuple5SerializerFactory.Create(gArgs[0], gArgs[1], gArgs[2], gArgs[3], gArgs[4]);
                if (serializerTmp.FixedSize > 0)
                {
                    serializer = serializerTmp;
                }
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(Tuple<,,,,>))
            {
                var gArgs = typeof(T).GetGenericArguments();
                var serializerTmp = (InternalSerializer<T>)Tuple5SerializerFactory.Create(gArgs[0], gArgs[1], gArgs[2], gArgs[3], gArgs[4]);
                if (serializerTmp.FixedSize > 0)
                {
                    serializer = serializerTmp;
                }
            }

            #endregion Tuple5

            if (typeof(T).IsArray)
            {
                var elementType = typeof(T).GetElementType();
                var elementSize = TypeHelper.GetSize(elementType);
                if (elementSize > 0)
                { // only for blittable types
                    serializer = (InternalSerializer<T>)ArraySerializerFactory.Create(elementType);
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
                    serializer = (InternalSerializer<T>)Collections.Internal.VectorStorageSerializerFactory.Create(elementType);
                }
            }

            // Do not add Json converter as fallback, it is not "binary", it implements the interface for
            // simpler implementation in BinarySerializer and fallback happens there
#endif

            return serializer;
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

            var bsAttr = BinarySerializationAttribute.GetSerializationAttribute(typeof(T));

            // by this line the type is not blittable

            BinarySerializer<T> serializerX = TypeSerializer;

            if (serializerX != null)
            {
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
                    if (typeof(BinarySerializer<T>).IsAssignableFrom(typeof(T)))
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

        private static DataTypeHeader InitCustomHeader()
        {
            // re-read, happens once per type
            var sa = BinarySerializationAttribute.GetSerializationAttribute(typeof(T));

            if (sa != null && sa.CustomHeader.TEOFS.TypeEnum != TypeEnum.None)
            {
                var value = (byte)sa.CustomHeader.TEOFS.TypeEnum;
                if (value < 100 || value >= 120)
                {
                    // Internal
                    Environment.FailFast("CustomHeader.TEOFS.TypeEnum must be in the range [100,119]");
                }
                return sa.CustomHeader;
            }

            return default;
        }
    }
}
