using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using System.Runtime.InteropServices;
using System.Threading;
using Spreads.Buffers;
using Spreads.Serialization;


namespace Spreads.Core.Tests {



    [TestFixture]
    public class TypeHelperTests {


        public struct NonBlittableStruct
        {
            public int Value1;
        }

        [Serialization(BlittableSize = 4)]
        public struct BlittableStruct1 {
            public int Value1;
        }

        [StructLayout(LayoutKind.Sequential, Size = 4)]
        public struct BlittableStruct2 {
            public int Value1;
        }

        public class MyPoco {
            public string String { get; set; }
            public long Long { get; set; }
        }

        [Serialization(BlittableSize = 5)]
        public struct BlittableStructWrong {
            public int Value1;
        }

        public class MyPocoWithConvertor : IBinaryConverter<MyPocoWithConvertor> {
            public string String { get; set; }
            public long Long { get; set; }
            public bool IsFixedSize => false;
            public int Size => 0;
            public byte Version => 1;
            public int SizeOf(MyPocoWithConvertor value, out MemoryStream payloadStream) {
                throw new NotImplementedException();
            }

            public int ToPtr(MyPocoWithConvertor value, IntPtr ptr, MemoryStream payloadStream = null) {
                throw new NotImplementedException();
            }

            public int FromPtr(IntPtr ptr, ref MyPocoWithConvertor value) {
                throw new NotImplementedException();
            }
        }

        [Test]
        public void CouldGetSizeOfDoubleArray() {
            Console.WriteLine(TypeHelper<double[]>.Size);

        }

        [Test]
        public void CouldGetSizeOfReferenceType() {

            Console.WriteLine(TypeHelper<string>.Size);

        }


        [Test]
        public void CouldWritePOCO() {

            var ptr = Marshal.AllocHGlobal(1024);
            var myPoco = new MyPoco {
                String = "MyString",
                Long = 123
            };
            TypeHelper<MyPoco>.ToPtr(myPoco, ptr);
            var newPoco = default(MyPoco);
            TypeHelper<MyPoco>.FromPtr(ptr, ref newPoco);
            Assert.AreEqual(myPoco.String, newPoco.String);
            Assert.AreEqual(myPoco.Long, newPoco.Long);

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

        [Test]
        public void CouldWriteArray() {

            var ptr = Marshal.AllocHGlobal(1024);
            var myArray = new int[2];
            myArray[0] = 123;
            myArray[1] = 456;

            TypeHelper<int[]>.ToPtr(myArray, ptr);

            var newArray = default(int[]);
            TypeHelper<int[]>.FromPtr(ptr, ref newArray);
            Assert.IsTrue(myArray.SequenceEqual(newArray));

        }

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


        [Test]
        public void CouldCreateNongenericDelegates() {
            var ptr = Marshal.AllocHGlobal(1024);
            var buffer = new DirectBuffer(1024, ptr);


            var fromPtrInt = TypeHelper.GetFromPtrDelegate(typeof(int));

            TypeHelper<int>.ToPtr(12345, ptr);

            object res = null;
            fromPtrInt(ptr, ref res);
            Assert.AreEqual((int)res, 12345);


            var toPtrInt = TypeHelper.GetToPtrDelegate(typeof(int));
            toPtrInt(42, ptr);

            int temp = 0;
            TypeHelper<int>.FromPtr(ptr, ref temp);
            Assert.AreEqual(42, temp);

            var sizeOfInt = TypeHelper.GetSizeOfDelegate(typeof(int));
            MemoryStream tmp;
            Assert.AreEqual(sizeOfInt(42, out tmp), 4);
            Assert.IsNull(tmp);

            Assert.AreEqual(4, TypeHelper.GetSize(typeof(int)));
            Assert.AreEqual(0, TypeHelper.GetSize(typeof(string)));
            Assert.AreEqual(-1, TypeHelper.GetSize(typeof(LinkedList<int>)));
        }


        [Test]
        public void CouldGetSizeOfPrimitivesDateTimeAndDecimal() {

            Assert.AreEqual(4, TypeHelper<int>.Size);
            Assert.AreEqual(8, TypeHelper<DateTime>.Size);
            Assert.AreEqual(-1, TypeHelper<decimal>.Size);
            Assert.AreEqual(-1, TypeHelper<char>.Size);
            Assert.AreEqual(0, TypeHelper<MyPocoWithConvertor>.Size);
            TypeHelper<MyPocoWithConvertor>.RegisterConvertor(new MyPocoWithConvertor(), true);
            Assert.AreEqual(0, TypeHelper<MyPocoWithConvertor>.Size);
        }


        [Test]
        public void BlittableAttributesAreHonored() {

            Assert.AreEqual(-1, TypeHelper<NonBlittableStruct>.Size);
            Assert.AreEqual(4, TypeHelper<BlittableStruct1>.Size);
            Assert.AreEqual(4, TypeHelper<BlittableStruct2>.Size);
            Assert.AreEqual(4, TypeHelper<BlittableStructWrong>.Size);
        }
    }
}
