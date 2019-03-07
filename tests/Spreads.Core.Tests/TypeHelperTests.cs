// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Spreads.Buffers;
using Spreads.DataTypes;
using Spreads.Serialization;

namespace Spreads.Core.Tests
{
    [Category("CI")]
    [TestFixture]
    public class TypeHelperTests
    {
        public struct NonBlittableStruct
        {
            public int Value1;
        }

        [BinarySerialization(4)]
        public struct BlittableStruct1
        {
            public int Value1;
        }

        [StructLayout(LayoutKind.Sequential, Size = 4)]
        public struct BlittableStruct2
        {
            public int Value1;
        }

        public class MyPoco
        {
            public string String { get; set; }
            public long Long { get; set; }
        }

        [BinarySerialization(5)]
        public struct BlittableStructWrong
        {
            public int Value1;
        }

        public class MyPocoWithConvertor : IBinarySerializer<MyPocoWithConvertor>
        {
            public string String { get; set; }
            public long Long { get; set; }
            public bool IsFixedSize => false;
            public int FixedSize => 0;
            public byte SerializerVersion => 1;
            public int SizeOf(MyPocoWithConvertor value, out RetainedMemory<byte> temporaryBuffer, out bool withPadding)
            {
                throw new NotImplementedException();
            }

            public int Write(MyPocoWithConvertor value, ref DirectBuffer destination)
            {
                throw new NotImplementedException();
            }

            public int Read(ref DirectBuffer source, out MyPocoWithConvertor value)
            {
                throw new NotImplementedException();
            }

            public int SizeOf(MyPocoWithConvertor map, out MemoryStream temporaryStream, 
                SerializationFormat format = SerializationFormat.Binary, Timestamp timestamp = default)
            {
                throw new NotImplementedException();
            }

            public int Write(MyPocoWithConvertor value, IntPtr destination, MemoryStream temporaryStream = null, 
                SerializationFormat format = SerializationFormat.Binary, Timestamp timestamp = default)
            {
                throw new NotImplementedException();
            }

            public int Read(IntPtr ptr, out MyPocoWithConvertor value, out Timestamp timestamp)
            {
                throw new NotImplementedException();
            }
        }

        // using System.Runtime.CompilerServices.Unsafe;
        [Test]
        public unsafe void CouldUseNewUnsafePackage()
        {
            var dt = new KeyValuePair<DateTime, decimal>[2];
            dt[0] = new KeyValuePair<DateTime, decimal>(DateTime.UtcNow.Date, 123.456M);
            dt[1] = new KeyValuePair<DateTime, decimal>(DateTime.UtcNow.Date.AddDays(1), 789.101M);
            var obj = (object)dt;
            byte[] asBytes = Unsafe.As<byte[]>(obj);

            //Console.WriteLine(asBytes.Length); // prints 2
            fixed (byte* ptr = &asBytes[0])
            {
                // reading this: https://github.com/dotnet/coreclr/issues/5870
                // it looks like we could fix byte[] and actually KeyValuePair<DateTime, decimal>[] will be fixed
                // because:
                // "GC does not care about the exact types, e.g. if type of local object
                // reference variable is not compatible with what is actually stored in it,
                // the GC will still track it fine."
                for (int i = 0; i < (8 + 16) * 2; i++)
                {
                    Console.WriteLine(*(ptr + i));
                }
                var firstDate = *(DateTime*)ptr;
                Assert.AreEqual(DateTime.UtcNow.Date, firstDate);
                Console.WriteLine(firstDate);
                var firstDecimal = *(decimal*)(ptr + 8);
                Assert.AreEqual(123.456M, firstDecimal);
                Console.WriteLine(firstDecimal);
                var secondDate = *(DateTime*)(ptr + 8 + 16);
                Assert.AreEqual(DateTime.UtcNow.Date.AddDays(1), secondDate);
                Console.WriteLine(secondDate);
                var secondDecimal = *(decimal*)(ptr + 8 + 16 + 8);
                Assert.AreEqual(789.101M, secondDecimal);
                Console.WriteLine(secondDecimal);
            }

            var ptr2 = Marshal.AllocHGlobal(1000);
            var myPoco = new MyPoco();
            Unsafe.Write((void*)ptr2, myPoco);
        }

        [Test]
        public void CouldGetSizeOfDoubleArray()
        {
            Console.WriteLine(TypeHelper<double[]>.FixedSize);
        }

        [Test]
        public void CouldGetSizeOfReferenceType()
        {
            Console.WriteLine(TypeHelper<string>.FixedSize);
        }

        [Test]
        public void BoolIsFizedSizeOfOne()
        {
            Assert.AreEqual(1, TypeHelper<bool>.FixedSize);
        }

        // TODO extension method for T
        //[Test]
        //public void CouldWritePOCOToBuffer() {
        //    var ptr = Marshal.AllocHGlobal(1024);
        //    var buffer = new DirectBuffer(1024, ptr);
        //    var myPoco = new MyPoco {
        //        String = "MyString",
        //        Long = 123
        //    };
        //    buffer.Write(0, myPoco);
        //    var newPoco = buffer.Read<MyPoco>(0);
        //    Assert.AreEqual(myPoco.String, newPoco.String);
        //    Assert.AreEqual(myPoco.Long, newPoco.Long);

        //}


        // TODO Extension method for Write<T>
        //[Test]
        //public void CouldWriteArrayToBuffer() {
        //    var ptr = Marshal.AllocHGlobal(1024);
        //    var buffer = new DirectBuffer(1024, ptr);
        //    var myArray = new int[2];
        //    myArray[0] = 123;
        //    myArray[1] = 456;

        //    buffer.Write(0, myArray);
        //    var newArray = buffer.Read<int[]>(0);
        //    Assert.IsTrue(myArray.SequenceEqual(newArray));

        //}

        // TODO extension method for T
        //[Test]
        //public void CouldWriteComplexTypeWithConverterToBuffer() {
        //    var ptr = Marshal.AllocHGlobal(1024);
        //    var buffer = new DirectBuffer(1024, ptr);

        //    var myStruct = new SetRemoveCommandBody<long, string>() {
        //        key = 123,
        //        value = "string value"
        //    };

        //    buffer.Write(0, myStruct);
        //    var newStruct = buffer.Read<SetRemoveCommandBody<long, string>>(0);
        //    Assert.AreEqual(myStruct.key, newStruct.key);
        //    Assert.AreEqual(myStruct.value, newStruct.value);

        //}

        [Test, Ignore("non-generic not used")]
        public unsafe void CouldCreateNongenericDelegates()
        {
            throw new NotImplementedException();

            //var dest = (Memory<byte>)new byte[1024];
            //var buffer = dest;
            //var handle = buffer.Pin();
            //var ptr = (IntPtr)handle.Pointer;

            //var fromPtrInt = TypeHelper.GetFromPtrDelegate(typeof(int));

            //TypeHelper<int>.Write(12345, (IntPtr)handle.Pointer);

            //object res;
            //fromPtrInt(ptr, out res);
            //Assert.AreEqual((int)res, 12345);

            //var toPtrInt = TypeHelper.GetToPtrDelegate(typeof(int));
            //toPtrInt(42, (IntPtr)handle.Pointer);

            //int temp;
            //TypeHelper<int>.Read(ptr, out temp, out _);
            //Assert.AreEqual(42, temp);

            //var sizeOfInt = TypeHelper.GetSizeOfDelegate(typeof(int));
            //MemoryStream tmp;
            //Assert.AreEqual(sizeOfInt(42, out tmp), 4);
            //Assert.IsNull(tmp);

            //Assert.AreEqual(4, TypeHelper.GetSize(typeof(int)));
            //Assert.AreEqual(-1, TypeHelper.GetSize(typeof(string)));
            //Assert.AreEqual(-1, TypeHelper.GetSize(typeof(LinkedList<int>)));
        }

        [Test]
        public void CouldGetSizeOfPrimitivesDateTimeAndDecimal()
        {
            Assert.AreEqual(4, TypeHelper<int>.FixedSize);
            Assert.AreEqual(8, TypeHelper<DateTime>.FixedSize);
            Assert.AreEqual(16, TypeHelper<decimal>.FixedSize);
            Assert.AreEqual(2, TypeHelper<char>.FixedSize);
            Assert.AreEqual(-1, TypeHelper<MyPocoWithConvertor>.FixedSize);
            TypeHelper<MyPocoWithConvertor>.RegisterConverter(new MyPocoWithConvertor(), true);
            Assert.AreEqual(-1, TypeHelper<MyPocoWithConvertor>.FixedSize);
        }

        [Test]
        public void BlittableAttributesAreHonored()
        {
            Assert.AreEqual(-1, TypeHelper<NonBlittableStruct>.FixedSize);
            Assert.AreEqual(4, TypeHelper<BlittableStruct1>.FixedSize);
            Assert.AreEqual(4, TypeHelper<BlittableStruct2>.FixedSize);

            // this will cause Environment.FailFast
            //Assert.AreEqual(4, TypeHelper<BlittableStructWrong>.Size);
        }
    }
}