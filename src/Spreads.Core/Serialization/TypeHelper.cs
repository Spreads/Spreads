// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Serialization
{
    // TODO remove special treatment of decimal, it is blittable/pinnable

    internal delegate int FromPtrDelegate(IntPtr ptr, out object value);

    internal delegate int ToPtrDelegate(object value, ref Buffer<byte> destination, uint offset = 0u, MemoryStream ms = null, CompressionMethod compression = CompressionMethod.DefaultOrNone);

    internal delegate int SizeOfDelegate(object value, out MemoryStream memoryStream, CompressionMethod compression = CompressionMethod.DefaultOrNone);

    internal class TypeParams
    {
        public int Size;

        /// <summary>
        /// CLR definition, we cache it here since ty.IsValueType is a virtual call
        /// </summary>
        public bool IsValueType;

        /// <summary>
        /// Either CLR-primitive or a pinnale struct marked with SerializationAttribute(BlittableSize > 0)
        /// </summary>
        public bool IsBlittable;

        public bool IsFixedSize;
        public bool IsDateTime;
    }

    internal class TypeHelper
    {
        private static int ReadObject<T>(IntPtr ptr, out object value)
        {
            var len = TypeHelper<T>.Read(ptr, out var temp);
            value = temp;
            return len;
        }

        private static int WriteObject<T>(object value, ref Buffer<byte> destination, uint offset = 0u, MemoryStream ms = null, CompressionMethod compression = CompressionMethod.DefaultOrNone)
        {
            var temp = value == null ? default(T) : (T)value;
            return TypeHelper<T>.Write(temp, ref destination, offset, ms, compression);
        }

        private static int SizeOfObject<T>(object value, out MemoryStream memoryStream, CompressionMethod compression = CompressionMethod.DefaultOrNone)
        {
            var temp = value == null ? default(T) : (T)value;
            return TypeHelper<T>.SizeOf(temp, out memoryStream, compression);
        }

        private static int Size<T>()
        {
            return TypeHelper<T>.Size;
        }

        private static readonly Dictionary<Type, FromPtrDelegate> FromPtrDelegateCache = new Dictionary<Type, FromPtrDelegate>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static FromPtrDelegate GetFromPtrDelegate(Type ty)
        {
            FromPtrDelegate temp;
            if (FromPtrDelegateCache.TryGetValue(ty, out temp)) return temp;
            var mi = typeof(TypeHelper).GetMethod("ReadObject", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var genericMi = mi.MakeGenericMethod(ty);
#if NET451
            temp = (FromPtrDelegate)Delegate.CreateDelegate(typeof(FromPtrDelegate), genericMi);
#else
            temp = (FromPtrDelegate)genericMi.CreateDelegate(typeof(FromPtrDelegate));
#endif
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
            var genericMi = mi.MakeGenericMethod(ty);
#if NET451
            temp = (ToPtrDelegate)Delegate.CreateDelegate(typeof(ToPtrDelegate), genericMi);
#else
            temp = (ToPtrDelegate)genericMi.CreateDelegate(typeof(ToPtrDelegate));
#endif
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
            var genericMi = mi.MakeGenericMethod(ty);
#if NET451
            temp = (SizeOfDelegate)Delegate.CreateDelegate(typeof(SizeOfDelegate), genericMi);
#else
            temp = (SizeOfDelegate)genericMi.CreateDelegate(typeof(SizeOfDelegate));
#endif
            SizeOfDelegateCache[ty] = temp;
            return temp;
        }

        private static readonly Dictionary<Type, int> SizeDelegateCache = new Dictionary<Type, int>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetSize(Type ty)
        {
            int temp;
            if (SizeDelegateCache.TryGetValue(ty, out temp)) return temp;
            var mi = typeof(TypeHelper).GetMethod("Size", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var genericMi = mi.MakeGenericMethod(ty);
            temp = (int)genericMi.Invoke(null, new object[] { });
            SizeDelegateCache[ty] = temp;
            return temp;
        }
    }

    internal sealed unsafe class TypeHelper<T> : TypeHelper
    {
        // ReSharper disable StaticMemberInGenericType
        private static bool _hasBinaryConverter;

        /// <summary>
        /// Returns a positive size of a blittable type T, -1 if the type T is not blittable and has
        /// no registered converter, 0 if there is a registered converter for variable-length type.
        /// We assume the type T is blittable if `GCHandle.Alloc(T[2], GCHandleType.Pinned) = true`.
        /// This is more relaxed than Marshal.SizeOf, but still doesn't cover cases such as
        /// an array of KVP[DateTime,double], which has contiguous layout in memory.
        /// </summary>
        public static readonly int Size = InitChecked();

        /// <summary>
        /// True if an array T[] could be pinned in memory.
        /// </summary>
        public static readonly bool IsPinnable = Size > 0 && typeof(T) != typeof(DateTime);

        private static IBinaryConverter<T> _converterInstance;
        private static TypeParams _typeParams;
        // ReSharper restore StaticMemberInGenericType

        // Just in case, do not use static ctor in any critical paths: https://github.com/Spreads/Spreads/issues/66
        // static TypeHelper() { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                var unsafeSize = Unsafe.SizeOf<T>();
                if (unsafeSize != size) Environment.FailFast("Pinned and unsafe sizes differ!");
                return size;
            }
            catch
            {
                return -1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int InitChecked()
        {
            try
            {
                return Init();
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Init()
        {
            _typeParams = new TypeParams();
            if (typeof(T) == typeof(DateTime))
            {
                _typeParams.IsBlittable = false;
                _typeParams.IsFixedSize = true;
                _typeParams.IsDateTime = true;
                _typeParams.Size = 8;
                return 8;
            }
            //if (typeof(T) == typeof(decimal))
            //{
            //    _typeParams.IsBlittable = true;
            //    _typeParams.IsFixedSize = true;
            //    _typeParams.IsDateTime = true;
            //    _typeParams.Size = 16;
            //    return 16;
            //}

            _typeParams.IsValueType = typeof(T).GetTypeInfo().IsValueType;
            var pinnedSize = PinnedSize();

            if (pinnedSize > 0)
            {
                if (typeof(T).GetTypeInfo().IsPrimitive && (typeof(T) != typeof(bool) && typeof(T) != typeof(char)))
                {
                    _typeParams.IsBlittable = true;
                    _typeParams.IsFixedSize = true;
                    _typeParams.Size = pinnedSize;
                    return pinnedSize;
                }

                // for a non-primitive type to be blittable, it must have an attribute
                var sa = SerializationAttribute.GetSerializationAttribute(typeof(T));
                var hasSizeAttribute = false;
                if (sa != null && sa.BlittableSize > 0)
                {
                    if (pinnedSize != sa.BlittableSize)
                    {
                        Environment.FailFast($"Size of type {typeof(T).Name} defined in SerializationAttribute {sa.BlittableSize} differs from calculated size {pinnedSize}.");
                    }
                    hasSizeAttribute = true;
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
                    _typeParams.IsBlittable = true;
                    _typeParams.IsFixedSize = true;
                    _typeParams.Size = pinnedSize;
                    return pinnedSize;
                }
                if (sa != null && sa.PreferBlittable)
                {
                    // NB: here it is OK to have an interface, we just opt-in for blittable
                    // when we know it won't change, e.g. generic struct with fixed fields (KV<K,V>, DictEntry<K,V>, Message<T>, etc.)
                    // usually those types are internal
                    _typeParams.IsBlittable = true;
                    _typeParams.IsFixedSize = true;
                    _typeParams.Size = pinnedSize;
                    return pinnedSize;
                }
            }

            // by this line the type is not blittable
            _typeParams.IsBlittable = false;

            // NB we try to check interface as a last step, because some generic types
            // could implement IBinaryConverter<T> but still be blittable for certain types,
            // e.g. DateTime vs long in PersistentMap<K,V>.Entry
            //if (tmp is IBinaryConverter<T>) {
            if (typeof(IBinaryConverter<T>).IsAssignableFrom(typeof(T)))
            {
                IBinaryConverter<T> converter;
                try
                {
                    converter = (IBinaryConverter<T>)Activator.CreateInstance<T>();
                }
                catch
                {
                    //Trace.TraceWarning($"Type {typeof(T).FullName} is marked as IBinaryConverter and so it must have a parameterless constructor");
                    throw new ApplicationException($"Type T ({typeof(T).FullName}) is marked as IBinaryConverter<T> and so it must have a parameterless constructor.");
                }
                if (converter.Version <= 0)
                {
                    throw new InvalidOperationException("IBinaryConverter<T> implementation for a type T should have a positive version.");
                }
                _converterInstance = converter;
                _hasBinaryConverter = true;
                return _converterInstance.IsFixedSize ? _converterInstance.Size : 0;
            }
            //byte[] should work as any other primitive array
            if (typeof(T) == typeof(byte[]))
            {
                _converterInstance = (IBinaryConverter<T>)(new ByteArrayBinaryConverter());
                _hasBinaryConverter = true;
                return 0;
            }
            if (typeof(T) == typeof(DateTime[]))
            {
                _converterInstance = (IBinaryConverter<T>)(new DateTimeArrayBinaryConverter());
                _hasBinaryConverter = true;
                return 0;
            }
            if (typeof(T) == typeof(string))
            {
                _converterInstance = (IBinaryConverter<T>)(new StringBinaryConverter());
                _hasBinaryConverter = true;
                return 0;
            }
            if (typeof(T).IsArray)
            {
                var elementType = typeof(T).GetElementType();
                var elementSize = GetSize(elementType);
                if (elementSize > 0)
                { // only for blittable types
                    var converter = (IBinaryConverter<T>)BlittableArrayConverterFactory.Create(elementType);
                    if (converter == null) return -1;
                    _converterInstance = converter;
                    _hasBinaryConverter = true;
                    Trace.Assert(!_converterInstance.IsFixedSize);
                    Trace.Assert(_converterInstance.Size == 0);
                    return 0;
                }
            }
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read(IntPtr ptr, out T value)
        {
            if (_hasBinaryConverter)
            {
                Debug.Assert(Size == 0);
                return _converterInstance.Read(ptr, out value);
            }
            if (Size < 0)
            {
                throw new InvalidOperationException("TypeHelper<T> doesn't support variable-size types");
            }
            Debug.Assert(Size > 0);
            //Debug.Assert(ptr.ToInt64() % Size == 0, "Unaligned unsafe read");
            value = Unsafe.Read<T>((void*)ptr);
            return Size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write(T value, ref Buffer<byte> destination, uint offset = 0u, MemoryStream ms = null, CompressionMethod compression = CompressionMethod.DefaultOrNone)
        {
            if (_hasBinaryConverter)
            {
                Debug.Assert(Size == 0);
                return _converterInstance.Write(value, ref destination, offset, ms, compression);
            }
            if (Size < 0)
            {
                throw new InvalidOperationException("TypeHelper<T> doesn't support variable-size types");
            }
            Debug.Assert(Size > 0);
            if (destination.Length < offset + Size)
            {
                return (int)BinaryConverterErrorCode.NotEnoughCapacity;
            }
            var handle = destination.Pin();

            var ptr = (IntPtr)handle.PinnedPointer + (int)offset;

            Unsafe.Write<T>((void*)ptr, value);

            handle.Free();

            return Size;
        }

        /// <summary>
        /// Returns binary size of the value
        /// </summary>
        /// <param name="value"></param>
        /// <param name="memoryStream"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int SizeOf(T value, out MemoryStream memoryStream, CompressionMethod compression)
        { //= CompressionMethod.DefaultOrNone
            if (_hasBinaryConverter)
            {
                Debug.Assert(Size == 0);
                return _converterInstance.SizeOf(value, out memoryStream, compression);
            }
            memoryStream = null;
            if (Size < 0)
            {
                return -1;
            }
            Debug.Assert(Size > 0);
            return Size;
        }

        public static byte Version => _hasBinaryConverter ? _converterInstance.Version : (byte)0;

        internal static void RegisterConverter(IBinaryConverter<T> converter, bool overrideExisting = false)
        {
            if (converter == null) throw new ArgumentNullException(nameof(converter));
            if (Size > 0) throw new InvalidOperationException("Cannot register a custom converter for fixed-size types");

            // NB TypeHelper is internal, we could provide some hooks later e.g. for char or bool
            if (converter.Version == 0)
            {
                Trace.TraceWarning("Adding a converter with version zero");
            }

            if (_hasBinaryConverter && !overrideExisting)
                throw new InvalidOperationException(
                    $"Type {typeof(T)} already implements IBinaryConverter<{typeof(T)}> interface. Use versioning to add a new converter (not supported yet)");

            if (_typeParams.IsBlittable)
            {
                Environment.FailFast($"Blittable types must not have IBinaryConverter<T>.");
            }
            _hasBinaryConverter = true;
            _converterInstance = converter;
        }

        public static T ConvertFrom<TSource>(TSource s)
        {
            return ConverterCache<TSource>.Converter(s);
        }

        private static class ConverterCache<TSource>
        {
            internal static readonly Func<TSource, T> Converter = Get();

            private static Func<TSource, T> Get()
            {
                var p = Expression.Parameter(typeof(TSource));
                var c = Expression.ConvertChecked(p, typeof(T));
                return Expression.Lambda<Func<TSource, T>>(c, p).Compile();
            }
        }
    }
}