//// This Source Code Form is subject to the terms of the Mozilla Public
//// License, v. 2.0. If a copy of the MPL was not distributed with this
//// file, You can obtain one at http://mozilla.org/MPL/2.0/.

//using Spreads.DataTypes;
//using System;
//using System.IO;
//using System.Runtime.InteropServices;
//using static System.Runtime.CompilerServices.Unsafe;

//namespace Spreads.Serialization
//{
//    internal class MemoryStreamBinaryConverter : IBinaryConverter<MemoryStream>
//    {
//        public bool IsFixedSize => false;
//        public int Size => -1;

//        public int SizeOf(in MemoryStream value, out MemoryStream temporaryStream, SerializationFormat format = SerializationFormat.Binary)
//        {
//            if (value.Length > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(temporaryStream), "Memory stream is too large");
//            temporaryStream = null;
//            return checked((int)value.Length + 8);
//        }

//        public unsafe int Write(in MemoryStream value, IntPtr pinnedDestination, MemoryStream temporaryStream = null, SerializationFormat format = SerializationFormat.Binary)
//        {
//            if (temporaryStream != null) throw new NotSupportedException("MemoryStreamBinaryConverter does not work with temp streams.");

//            if (value.Length > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(temporaryStream), "Memory stream is too large");

//            var totalLength = checked((int)value.Length + 8);

            
//            // version
//            var header = new DataTypeHeader
//            {
//                VersionAndFlags = {
//                    Version = 0,
//                    IsBinary = true,
//                    IsDelta = false,
//                    IsCompressed = false },
//                TypeEnum = TypeEnum.Binary
//            };
//            WriteUnaligned((void*)pinnedDestination, header);
//            // size
//            WriteUnaligned((void*)(pinnedDestination + 4), totalLength - 8);

//            // payload
//            value.WriteToRef(ref AsRef<byte>((void*)(pinnedDestination + 8)));

//            return totalLength;
//        }

//        public unsafe int Read(IntPtr ptr, out MemoryStream value)
//        {
//            var header = ReadUnaligned<DataTypeHeader>((void*)ptr);
//            var payloadLength = Marshal.ReadInt32(ptr + 4);
            
//            if (header.VersionAndFlags.Version != 0) throw new NotSupportedException();
//            // TODO Use RMS large buffer
//            var bytes = new byte[payloadLength];
//            Marshal.Copy(ptr + 8, bytes, 0, payloadLength);
//            value = new MemoryStream(bytes);
//            return payloadLength + 8;
//        }

//        public byte ConverterVersion => 0;
//    }
//}