// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.DataTypes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Spreads.Native;
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

    // NB Optimization fail: static RO are not JIT consts for generics. Although for tiered compilation this could work at some point.

    public static unsafe class TypeHelper<T>
    {
        // Just in case, do not use static ctor in any critical paths: https://github.com/Spreads/Spreads/issues/66
        // static TypeHelper() { }

        internal static IBinaryConverter<T> BinaryConverter;

        public static bool HasBinaryConverter
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => BinaryConverter != null;
        }

        /// <summary>
        /// Returns a positive size of a pinnable type T, -1 if the type T is not pinnable or has
        /// a registered <see cref="IBinaryConverter{T}"/> converter.
        /// We assume the type T is pinnable if `GCHandle.Alloc(T[2], GCHandleType.Pinned) = true`.
        /// This is more relaxed than Marshal.SizeOf, but still doesn't cover cases such as
        /// an array of KVP[DateTime,double], which has contiguous layout in memory.
        /// </summary>
        // ReSharper disable once StaticMemberInGenericType
        public static readonly int FixedSize = InitChecked();


        // NOTE: PinnedSize simply tries to pin via GCHandle.
        // FixedSize could be positive for non-pinnable structs with auto layout (e.g. DateTime)
        // but it is opt-in and requires an attribute to treat a custom blittable type as fixed.

        /// <summary>
        /// True if an array T[] could be pinned in memory.
        /// </summary>
        // ReSharper disable once StaticMemberInGenericType
        public static readonly bool IsPinnable = PinnedSize() > 0 || FixedSize > 0;

        public static readonly int ElemOffset = Native.UnsafeEx.ElemOffset(new T[1]);

        public static readonly int ElemSize = SizeOf<T>();

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
            throw new ApplicationException($"Type {typeof(T).Name} is not fixed size. Either add Size parameter to StructLayout attribute or use Spreads.Serialization attribute to explicitly opt-in to treat non-primitive user-defined structs as fixed-size.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FailTypeIsNotFixedSize()
        {
            ThrowHelper.FailFast($"Type {typeof(T).Name} is not fixed size. Either add Size parameter to StructLayout attribute or use Spreads.Serialization attribute to explicitly opt-in to treat non-primitive user-defined structs as fixed-size.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FailHeaderNotSimpleBinary()
        {
            ThrowHelper.FailFast("Header is not simple binary.");
        }

        /// <summary>
        /// CLR definition, we cache it here since ty.IsValueType is a virtual call
        /// </summary>
        public static readonly bool IsValueType = typeof(T).GetTypeInfo().IsValueType;

        /// <summary>
        /// Implements <see cref="IDelta{T}"/>
        /// </summary>
        public static readonly bool IsIDelta = typeof(IDelta<T>).GetTypeInfo().IsAssignableFrom(typeof(T));

        

        // ReSharper disable once StaticMemberInGenericType
        public static readonly bool IsFixedSize = FixedSize > 0;

        private static TypeEnum InitTypeEnum()
        {
            // re-read, happens once per type
            var sa = BinarySerializationAttribute.GetSerializationAttribute(typeof(T));

            if (VariantHelper<T>.TypeEnum == 0)
            {
                if (sa != null && sa.TypeEnum != TypeEnum.None)
                {
                    if ((int)sa.TypeEnum > Variant.KnownSmallTypesLimit && (FixedSize > 0 && FixedSize <= 25))
                    {
                        // Internal
                        Environment.FailFast("(int) sa.TypeEnum > Variant.KnownSmallTypesLimit && (FixedSize > 0 && FixedSize <= 25)");
                    }
                    return sa.TypeEnum;
                }
                if (FixedSize > 0 && FixedSize <= 255)
                {
                    return TypeEnum.FixedBinary;
                }
            }

            if (sa != null && sa.TypeEnum != TypeEnum.None)
            {
                // Internal
                Environment.FailFast("Do not provide TypeEnum for pre-defined types in Spreads.Core assembly.");
            }
            return VariantHelper<T>.TypeEnum;
        }

        private static byte InitTypeSize()
        {
            return (FixedSize > 0 && FixedSize <= 255)
                ? (byte)FixedSize
                : VariantHelper<T>.GetElementTypeSizeForHeader();
        }

        internal static readonly DataTypeHeader DefaultBinaryHeader = new DataTypeHeader
        {
            VersionAndFlags =
            {
                ConverterVersion = 0,
                IsBinary = true,
                CompressionMethod = CompressionMethod.None
            },
            TypeEnum = InitTypeEnum(),
            TypeSize = InitTypeSize(),
            ElementTypeEnum = VariantHelper<T>.ElementTypeEnum // TODO if TypeEnum == None | FixedBinary, check KnownTypeId
        };

        internal static readonly DataTypeHeader DefaultBinaryHeaderWithTs = new DataTypeHeader
        {
            VersionAndFlags =
            {
                ConverterVersion = 0,
                IsBinary = true,
                CompressionMethod = CompressionMethod.None,
                IsTimestamped = true
            },
            TypeEnum = DefaultBinaryHeader.TypeEnum,
            TypeSize = DefaultBinaryHeader.TypeSize,
            ElementTypeEnum = DefaultBinaryHeader.ElementTypeEnum
        };

        private static int InitChecked()
        {
            try
            {
                var size = Init();
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

        private static int PinnedSize()
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
                return size;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Method is only called from the static constructor of TypeHelper.
        /// </summary>
        /// <returns></returns>
        private static int Init()
        {
            if (typeof(T) == typeof(DateTime))
            {
                return 8;
            }
            // NB decimal is pinnable but not primitive, the check below fails on it
            if (typeof(T) == typeof(decimal))
            {
                return 16;
            }

            var pinnedSize = PinnedSize();

            var sa = BinarySerializationAttribute.GetSerializationAttribute(typeof(T));

            if (pinnedSize > 0)
            {
                if (typeof(T).GetTypeInfo().IsPrimitive)
                {
                    return pinnedSize;
                }

                // for a non-primitive type to be blittable, it must have an attribute

                var hasSizeAttribute = false;
                if (sa != null && sa.BlittableSize > 0)
                {
                    if (pinnedSize != sa.BlittableSize)
                    {
                        Environment.FailFast(
                            $"Size of type {typeof(T).FullName} defined in SerializationAttribute {sa.BlittableSize} differs from calculated size {pinnedSize}.");
                    }

                    if (sa.ConverterType != null)
                    {
                        Environment.FailFast(
                            $"Cannot define BlittableSize and ConverterType at the same time in Serialization attribute of type {typeof(T).FullName}.");
                    }
                    hasSizeAttribute = true;
                }
                else
                {
                    var sla = BinarySerializationAttribute.GetStructLayoutAttribute(typeof(T));
                    if (sla != null && sla.Size > 0)
                    {
                        if (pinnedSize != sla.Size || sla.Value == LayoutKind.Auto)
                        {
                            Environment.FailFast(
                                $"Size of type {typeof(T).Name} defined in StructLayoutAttribute {sla.Size} differs from calculated size {pinnedSize} or layout is set to LayoutKind.Auto.");
                        }
                        hasSizeAttribute = true;
                    }
                }
                if (hasSizeAttribute)
                {
                    if (typeof(IBinaryConverter<T>).IsAssignableFrom(typeof(T)))
                    {
                        // NB: this makes no sense, because blittable is version 0, if we have any change
                        // to struct layout later, we won't be able to work with version 0 anymore
                        // and will lose ability to work with old values.
                        Environment.FailFast($"Blittable types must not implement IBinaryConverter<T> interface.");
                    }
                    return pinnedSize;
                }
                if (sa != null && sa.PreferBlittable)
                {
                    // NB: here it is OK to have an interface, we just opt-in for blittable
                    // when we know it won't change, e.g. generic struct with fixed fields (KV<K,V>, DictEntry<K,V>, Message<T>, etc.)
                    // usually those types are internal
                    return pinnedSize;
                }
            }

            // by this line the type is not blittable

            IBinaryConverter<T> converter = null;

            if (sa != null && sa.ConverterType != null)
            {
                if (!typeof(IBinaryConverter<T>).IsAssignableFrom(sa.ConverterType))
                {
                    Environment.FailFast($"ConverterType `{sa.ConverterType.FullName}` in Serialization attribute does not implement IBinaryConverter<T> for the type `{typeof(T).FullName}`");
                }

                try
                {
                    converter = (IBinaryConverter<T>)Activator.CreateInstance(sa.ConverterType);
                }
                catch
                {
                    Environment.FailFast($"ConverterType `{sa.ConverterType.FullName}` must have a parameterless constructor.");
                }
            }

            // NB we try to check interface as a last step, because some generic types
            // could implement IBinaryConverter<T> but still be blittable for certain types,
            // e.g. DateTime vs long in PersistentMap<K,V>.Entry
            //if (tmp is IBinaryConverter<T>) {
            if (typeof(IBinaryConverter<T>).IsAssignableFrom(typeof(T)))
            {
                if (converter != null)
                {
                    Environment.FailFast($"Converter `{converter.GetType().FullName}` is already set via Serialization attribute. The type `{typeof(T).FullName}` should not implement IBinaryConverter<T> interface or the attribute should not include ConverterType property.");
                }
                try
                {
                    converter = (IBinaryConverter<T>)Activator.CreateInstance<T>();
                }
                catch
                {
                    //Trace.TraceWarning($"Type {typeof(T).FullName} is marked as IBinaryConverter and so it must have a parameterless constructor");
                    Environment.FailFast($"Type T ({typeof(T).FullName}) is marked as IBinaryConverter<T> and therefore must have a parameterless constructor.");
                }
                if (converter.ConverterVersion <= 0)
                {
                    Environment.FailFast("User IBinaryConverter<T> implementation for a type T should have a positive version.");
                }
            }

            // NB: string as UTF8 Json is OK
            // /else if (typeof(T) == typeof(string))
            // /{
            // /    BinaryConverter = (IBinaryConverter<T>)(new StringBinaryConverter());
            // /}

            // TODO
            if (typeof(T).IsArray)
            {
                var elementType = typeof(T).GetElementType();
                var elementSize = TypeHelper.GetSize(elementType);
                if (elementSize > 0)
                { // only for blittable types
                    converter = (IBinaryConverter<T>)ArrayConverterFactory.Create(elementType);
                }
            }

            // Do not add Json converter as fallback, it is not "binary", it implements the interface for
            // simpler implementation in BinarySerializer and fallback happens there

            BinaryConverter = converter;

            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteWithHeader(in T value, DirectBuffer destination, Timestamp timestamp = default)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            {
                if (FixedSize <= 0)
                {
                    FailTypeIsNotFixedSize();
                    return -1;
                }
            }

            var hasTs = (long)timestamp != default;
            // var tsSize = *(int*)(&hasTs) << 3; // could have branchless, but need thread-save dirty destination. pointers on stack are slow for some reason

            int pos;
            if (hasTs)
            {
                pos = 12;
                if (AdditionalCorrectnessChecks.Enabled)
                {
                    destination.Assert(0, pos + FixedSize);
                }
                WriteUnaligned(destination.Data, DefaultBinaryHeaderWithTs);
                WriteUnaligned(destination.Data + DataTypeHeader.Size, timestamp);
            }
            else
            {
                pos = 4;
                if (AdditionalCorrectnessChecks.Enabled)
                {
                    destination.Assert(0, pos + FixedSize);
                }
                WriteUnaligned(destination.Data, DefaultBinaryHeader);
            }

            WriteUnaligned(destination.Data + pos, value);

            var len = pos + FixedSize;
            return len;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadWithHeader(byte* ptr, out T value, out Timestamp timestamp)
        {
            var header = ReadUnaligned<DataTypeHeader>(ptr);

            var versionAndFlags = header.VersionAndFlags;

            if (AdditionalCorrectnessChecks.Enabled)
            {
                if (FixedSize <= 0)
                {
                    FailTypeIsNotFixedSize();
                }

                if (versionAndFlags.CompressionMethod != CompressionMethod.None
                    || !versionAndFlags.IsBinary || versionAndFlags.ConverterVersion != 0)
                {
                    FailHeaderNotSimpleBinary();
                }
            }

            var tsSize = 0;
            if (versionAndFlags.IsTimestamped)
            {
                tsSize = Timestamp.Size;
                timestamp = ReadUnaligned<Timestamp>(ptr + DataTypeHeader.Size);
            }
            else
            {
                timestamp = default;
            }
            value = ReadUnaligned<T>(ptr + DataTypeHeader.Size + tsSize);
            return DataTypeHeader.Size + tsSize + FixedSize;
        }

        public static byte ConverterVersion
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => BinaryConverter?.ConverterVersion ?? 0;
        }

        public static void RegisterConverter(IBinaryConverter<T> converter,
            bool overrideExisting = false)
        {
            if (converter == null) { throw new ArgumentNullException(nameof(converter)); }
            if (FixedSize > 0) { throw new InvalidOperationException("Cannot register a custom converter for pinnable types"); }

            // NB TypeHelper is internal, we could provide some hooks later e.g. for char or bool
            if (converter.ConverterVersion == 0 || converter.ConverterVersion > 15)
            {
                ThrowHelper.ThrowArgumentException("User-implemented converter version must be in the range 1-15.");
            }

            if (HasBinaryConverter && !overrideExisting)
            {
                ThrowHelper.ThrowInvalidOperationException(
                    $"Type {typeof(T)} already implements IBinaryConverter<{typeof(T)}> interface. Use versioning to add a new converter (not supported yet)");
            }

            if (IsFixedSize) // TODO this may be possible, but don't care for now
            {
                Environment.FailFast($"Blittable types must not have IBinaryConverter<T>.");
            }
            BinaryConverter = converter;
        }
    }
}