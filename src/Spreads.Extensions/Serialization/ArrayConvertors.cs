using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Spreads.Serialization {
    internal static class ArrayConvertorFactory {
        public static IBinaryConverter<TElement[]> GenericCreate<TElement>() {
            // check if element is fixed size
            // then, if pinnable - use pointer directly to pinned array
            // else, copy element-by-element to a buffer

            return new ArrayBinaryConverter<TElement>();
        }

        public static object Create(Type type) {
            MethodInfo method = typeof(ArrayConvertorFactory).GetMethod("GenericCreate");
            MethodInfo generic = method.MakeGenericMethod(type);
            return generic.Invoke(null, null);
        }
    }

    internal class ArrayBinaryConverter<TElement> : IBinaryConverter<TElement[]> {
        public bool IsFixedSize => false;
        public int Size => 0;
        public int Version => TypeHelper<TElement>.Version;

        private static int _itemSize = TypeHelper<TElement>.Size;

        public int SizeOf(TElement[] value) {
            if (_itemSize > 0) {
                return _itemSize * value.Length;
            }
            throw new NotImplementedException();
        }

        public void ToPtr(TElement[] value, IntPtr ptr) {
            throw new NotImplementedException();
        }

        public TElement[] FromPtr(IntPtr ptr) {
            throw new NotImplementedException();
        }
    }


    internal class ByteArrayBinaryConverter : IBinaryConverter<byte[]> {
        public bool IsFixedSize => false;
        public int Size => 0;
        public int SizeOf(byte[] value) => value.Length;

        public void ToPtr(byte[] value, IntPtr ptr) {
            // version
            Marshal.WriteInt32(ptr, 0);
            // size
            Marshal.WriteInt32(ptr + 4, value.Length);
            // payload
            Marshal.Copy(value, 0, ptr + 8, value.Length);
        }

        public byte[] FromPtr(IntPtr ptr) {
            var version = Marshal.ReadInt32(ptr);
            if (version != 0) throw new NotSupportedException();
            var length = Marshal.ReadInt32(ptr + 4);
            var bytes = new byte[length];
            Marshal.Copy(ptr + 8, bytes, 0, length);
            return bytes;
        }

        public int Version => 0;
    }
}
