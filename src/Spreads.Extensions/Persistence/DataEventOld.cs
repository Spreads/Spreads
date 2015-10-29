using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Spreads.Persistence {

    // NB, TODO max 16 bytes, according to the protocol spec

    public enum SpreadsCommand : byte {
        Subscribe = 0,
        Unsubscribe = 1,
        CreateSeries = 2,
        UpdateSeries = 3,
        SeriesDefinition = 4,
        SetData = 5,
        RemoveData = 6,
        CompleteData = 7,
        AcquireLock = 8,
        ReleaseLock = 9,
        Error = 10,
        Resend = 11,
        /// <summary>
        /// A sequence of commands. (No trees, just a sequence. I.e. batch of batches is an error.)
        /// </summary>
        Batch = 12,
    }


    // TODO reflect changes in wire format from Excel

    [Flags]
    public enum SpreadsCommandFlags : byte {

        None = 0,
        /// <summary>
        /// If set, indicates that payload was compressed using Blosc
        /// </summary>
        Compressed = 1,
        /// <summary>
        /// If set, indicates that payload was encrypted. FormatVersion property of a data event should indicate the encryption scheme used.
        /// </summary>
        Encrypted = 2,
        /// <summary>
        /// Data length is variable. First 4 bytes of payload contain data length. Otherwise data is packed sequentially and its width is known by series id.
        /// </summary>
        VariableLength = 4,
    }

    // 4 bytes metadata for each SpreadsCommand
    public interface IMetaData {
        SpreadsCommand Command { get; } // 4 bits
        SpreadsCommandFlags Flags { get; } // 4 bits
        byte FormatVersion { get; } // 8 bits
        short ElementCount { get; } // 16 bits
    }

    // TODO probably should manullay inline this to every command

    public unsafe struct BufferMetaData : IMetaData {
        public byte[] Array { get; private set; }
        public int Offset { get; }
        private const byte flagMask = (byte)15;

        public BufferMetaData(byte[] buffer, int offset = 0) {
            if (buffer.Length - offset < 4) {
                throw new ArgumentException("MetaData buffer is smaller that 4 bytes", nameof(buffer));
            }
            Array = buffer;
            Offset = offset;
        }

        public SpreadsCommand Command {
            get { throw new NotImplementedException(); } 
            set
            {
                throw new NotImplementedException();
            }
        }

        
        // TODO recheck, test this
        public SpreadsCommandFlags Flags {
            get {
                fixed (byte* ptr = &Array[Offset])
                {
                    var byteValue = *(ptr);
                    byteValue = (byte) (byteValue & flagMask);
                    return (SpreadsCommandFlags) (byteValue);
                }
            }
            set {
                fixed (byte* ptr = &Array[Offset])
                {
                    *ptr = (byte)value;
                }
            }
        }

        public byte FormatVersion {
            get {
                fixed (byte* ptr = &Array[Offset])
                {
                    return *(ptr + 1);
                }
            }
            set {
                fixed (byte* ptr = &Array[Offset])
                {
                    *(byte*)(ptr + 1) = value;
                }
            }
        }

        public short ElementCount {
            get {
                fixed (byte* ptr = &Array[Offset])
                {
                    return (short)*(ptr + 2);
                }
            }
            set {
                fixed (byte* ptr = &Array[Offset])
                {
                    *(short*)(ptr + 2) = value;
                }
            }
        }
    }


    // TODO we either will have to copy data from pointer to represent it as an ArraySegment,
    // or we should return IntPtr instead of ArraySegment and GC-pin the buffer

    public interface IDataEvent {
        ArraySegment<byte> SeriesId { get; }
        long SequenceId { get; }
        int DataLength { get; }
        byte FormatVersion { get; }
        SpreadsCommandFlags Flags { get; }
        short ElementCount { get; }
        ArraySegment<byte> Payload { get; }
    }

    /// <summary>
    /// DataEvent interface implemented on top of a byte buffer
    /// </summary>
    public unsafe struct BufferDataEvent : IDataEvent {

        // NB struct because its primary use is to wrap existing byte buffer, this could generate a lot of garbage if class not struct. Similar to ArraySegment<>.
        // 

        public byte[] Array { get; private set; }
        public int Offset { get; }

        public int Count => 32 + DataLength;

        // this class does not allocate an buffer, it wraps around a provided buffer (unless it is null)

        public BufferDataEvent(byte[] buffer = null, int offset = 0) {
            if (buffer == null) {
                buffer = new byte[32];
                offset = 0;
            }
            Array = buffer;
            Offset = offset;
            if (buffer.Length - offset < 32) {
                throw new ArgumentException("Buffer is smaller that the header size", nameof(buffer));
            }
        }

        public BufferDataEvent(ArraySegment<byte> buffer) : this(buffer.Array, buffer.Offset) { }

        public ArraySegment<byte> SeriesId {
            get {
                return new ArraySegment<byte>(Array, Offset, 16);
            }
            set {
                if (value.Count != 16) {
                    throw new ArgumentException("SeriesId must have length = 16");
                }
                fixed (byte* ptr = &Array[Offset])
                {
                    Marshal.Copy(value.Array, value.Offset, (IntPtr)ptr, value.Count);
                }
            }
        }

        public int DataLength {
            get {
                fixed (byte* ptr = &Array[Offset])
                {
                    return (int)*(ptr + 16);
                }
            }
            set {
                fixed (byte* ptr = &Array[Offset])
                {
                    *(int*)(ptr + 16) = value;
                }
            }
        }


        public byte FormatVersion {
            get {
                fixed (byte* ptr = &Array[Offset])
                {
                    return *(ptr + 20);
                }
            }
            set {
                fixed (byte* ptr = &Array[Offset])
                {
                    *(byte*)(ptr + 20) = value;
                }
            }
        }


        public SpreadsCommandFlags Flags {
            get {
                fixed (byte* ptr = &Array[Offset])
                {
                    return (SpreadsCommandFlags)(*(ptr + 21));
                }
            }
            set {
                fixed (byte* ptr = &Array[Offset])
                {
                    *(byte*)(ptr + 21) = (byte)value;
                }
            }
        }


        public short ElementCount {
            get {
                fixed (byte* ptr = &Array[Offset])
                {
                    return (short)*(ptr + 22);
                }
            }
            set {
                fixed (byte* ptr = &Array[Offset])
                {
                    *(short*)(ptr + 22) = value;
                }
            }
        }

        public long SequenceId {
            get {
                fixed (byte* ptr = &Array[Offset])
                {
                    return (long)*(ptr + 24);
                }
            }
            set {
                fixed (byte* ptr = &Array[Offset])
                {
                    *(long*)(ptr + 24) = value;
                }
            }
        }


        public ArraySegment<byte> Payload {
            get {
                var dlen = DataLength;
                if (dlen + 32 > Array.Length - Offset) {
                    throw new ApplicationException("BufferDataEvent has a buffer that is smaller than is required to accomodate data payload");
                }
                return new ArraySegment<byte>(Array, Offset + 32, dlen);
            }
            set {
                if (Array.Length == 32) {
                    Trace.Assert(Offset == 0);
                    var newBuffer = new byte[32 + value.Count];
                    System.Array.Copy(Array, newBuffer, 32);
                    Array = newBuffer;
                }
                var dlen = value.Count;
                if (dlen + 32 > Array.Length - Offset) {
                    throw new ApplicationException("BufferDataEvent has a buffer that is smaller than is required to accomodate data payload");
                }
                fixed (byte* ptr = &Array[Offset + 32])
                {
                    Marshal.Copy(value.Array, value.Offset, (IntPtr)ptr, value.Count);
                }
            }
        }


        public Guid SeriesGuid {
            get { return new Guid(SeriesId.ToArray()); }
            set {
                var bytes = value.ToByteArray();
                if (bytes.Length != 16) {
                    throw new ArgumentException("SeriesId must have length = 16");
                }
                fixed (byte* ptr = &Array[Offset])
                {
                    Marshal.Copy(bytes, 0, (IntPtr)ptr, 16);
                }
            }
        }


        /// <summary>
        /// Writes data event to an unmanaged pointer, without SeriesId
        /// </summary>
        public void WriteContentToPointer(IntPtr destination) {
            // if copy is non-atomic, we should set length as the last operation
            // non-zero length is like a MRE. TODO need to check how Aeron actually works with regards to this.
            Marshal.Copy(Array, Offset + 20, destination + 4, Count - 20);
            *(int*)(destination) = DataLength;
        }
    }

}
