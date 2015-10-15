using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads.Persistence {

    public abstract class BaseObjectSerializer {
        public abstract byte[] SerializeObject(object value);
        public abstract object DeserializeObject(byte[] bytes);

        public abstract byte[] SerializeArrayObject(object values);

        public abstract object DeserializeArrayObject(byte[] bytes);
    }

    public abstract class BaseTypeSerializer<T> : BaseObjectSerializer {
        public abstract byte[] Serialize(T value);
        public abstract T Deserialize(byte[] bytes);

        public virtual byte[] SerializeArray(T[] values) {
            throw new NotImplementedException();
        }

        public virtual T[] DeserializeArray(byte[] bytes) {
            throw new NotImplementedException();
        }

        // implement non-generic version

        public override byte[] SerializeObject(object value)
        {
            return Serialize((T)value);
        }

        public override object DeserializeObject(byte[] bytes)
        {
            return Deserialize(bytes);
        }

        public override byte[] SerializeArrayObject(object values)
        {
            return SerializeArray((T[]) values);
        }

        public override object DeserializeArrayObject(byte[] bytes)
        {
            return DeserializeArray(bytes);
        }

    }

    /// <summary>
    /// Taken from .NET, commented out DBNull and Boolean. 
    /// </summary>
    public enum TypeCode : short {
        Empty = 0,          // Null reference
        Object = 1,         // Instance that isn't a value
        //DBNull = 2,         // Database null value
        //Boolean = 3,        // Boolean
        Char = 4,           // Unicode character
        SByte = 5,          // Signed 8-bit integer
        Byte = 6,           // Unsigned 8-bit integer
        Int16 = 7,          // Signed 16-bit integer
        UInt16 = 8,         // Unsigned 16-bit integer
        Int32 = 9,          // Signed 32-bit integer
        UInt32 = 10,        // Unsigned 32-bit integer
        Int64 = 11,         // Signed 64-bit integer
        UInt64 = 12,        // Unsigned 64-bit integer
        Single = 13,        // IEEE 32-bit float
        Double = 14,        // IEEE 64-bit double
        Decimal = 15,       // Decimal
        DateTime = 16,      // DateTime
        String = 18,        // Unicode character string
    }



    /// <summary>
    /// 
    /// </summary>
    public unsafe sealed class BinarySerializer // TODO (low) just for fun, test sealed vs struct vs ordinary class on primitives where "dynamic dispatch" is likely to be more expensive that converting to bytes
    {
        public Dictionary<Type, BaseObjectSerializer> CustomSerializers { get; } = new Dictionary<Type, BaseObjectSerializer>();

        public int Serialize<T>(T value, byte[] buffer, int offset)
        {
            fixed (byte* ptr = &buffer[0])
            {
                return Serialize<T>(value, (IntPtr)ptr, offset);
            }
        }

        public int Serialize<T>(T value, IntPtr pointer, int offset)
        {
            // TODO (perf) test what is faster - type checking with ifs or dynamic resolution
            var ty = value.GetType();
            if (ty == typeof (int))
            {
                *(int*)(pointer + offset) = Convert.ToInt32(value);
                return 1;
            }

            // NB We hard-code primitive types and select non-primitive types
            dynamic dinValue = value;
            return SerializeImpl(dinValue, pointer, offset);
        }

        private int SerializeImpl(char value, IntPtr pointer, int offset) {
            *(char*)(pointer + offset) = value;
            return 1;
        }

        private int SerializeImpl(int value, IntPtr pointer, int offset)
        {
            *(int*)(pointer + offset) = value;
            return 4;
        }


        private int SerializeImpl(object value, IntPtr pointer, int offset) {
            throw new NotImplementedException();
        }

    }
}
