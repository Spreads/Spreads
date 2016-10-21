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
using System.Runtime.CompilerServices;
using Spreads.Utils;

namespace Spreads.Core.Tests {



    [TestFixture]
    public class TypeHelperTests {


        public struct NonBlittableStruct {
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
            public int SizeOf(MyPocoWithConvertor value, out MemoryStream temporaryStream) {
                throw new NotImplementedException();
            }

            public int Write(MyPocoWithConvertor value, ref DirectBuffer destination, uint offset = 0, MemoryStream temporaryStream = null) {
                throw new NotImplementedException();
            }


            public int Read(IntPtr ptr, ref MyPocoWithConvertor value) {
                throw new NotImplementedException();
            }
        }


        // using System.Runtime.CompilerServices.Unsafe;
        [Test]
        public unsafe void CouldUseNewUnsafePackage() {
            var dt = new KeyValuePair<DateTime, decimal>[2];
            dt[0] = new KeyValuePair<DateTime, decimal>(DateTime.UtcNow.Date, 123.456M);
            dt[1] = new KeyValuePair<DateTime, decimal>(DateTime.UtcNow.Date.AddDays(1), 789.101M);
            var obj = (object)dt;
            byte[] asBytes = Unsafe.As<byte[]>(obj);

            //Console.WriteLine(asBytes.Length); // prints 2
            fixed (byte* ptr = &asBytes[0]) {
                // reading this: https://github.com/dotnet/coreclr/issues/5870
                // it looks like we could fix byte[] and actually KeyValuePair<DateTime, decimal>[] will be fixed
                // because:
                // "GC does not care about the exact types, e.g. if type of local object 
                // reference variable is not compatible with what is actually stored in it, 
                // the GC will still track it fine."
                for (int i = 0; i < (8 + 16) * 2; i++) {
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
        public void CouldGetSizeOfDoubleArray() {
            Console.WriteLine(TypeHelper<double[]>.Size);

        }

        [Test]
        public void CouldGetSizeOfReferenceType() {

            Console.WriteLine(TypeHelper<string>.Size);

        }


        [Test]
        public void CouldWriteBlittableStruct1() {

            var ptr = Marshal.AllocHGlobal(1024);
            var dest = new DirectBuffer(1024, ptr);
            var myBlittableStruct1 = new BlittableStruct1 {
                Value1 = 12345
            };
            TypeHelper<BlittableStruct1>.Write(myBlittableStruct1, ref dest);
            var newBlittableStruct1 = default(BlittableStruct1);
            TypeHelper<BlittableStruct1>.Read(ptr, ref newBlittableStruct1);
            Assert.AreEqual(myBlittableStruct1.Value1, newBlittableStruct1.Value1);

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
            var dest = new DirectBuffer(1024, ptr);
            var myArray = new int[2];
            myArray[0] = 123;
            myArray[1] = 456;

            TypeHelper<int[]>.Write(myArray, ref dest);

            var newArray = default(int[]);
            TypeHelper<int[]>.Read(ptr, ref newArray);
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

            TypeHelper<int>.Write(12345, ref buffer);

            object res = null;
            fromPtrInt(ptr, ref res);
            Assert.AreEqual((int)res, 12345);


            var toPtrInt = TypeHelper.GetToPtrDelegate(typeof(int));
            toPtrInt(42, ref buffer);

            int temp = 0;
            TypeHelper<int>.Read(ptr, ref temp);
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
            Assert.AreEqual(16, TypeHelper<decimal>.Size);
            Assert.AreEqual(-1, TypeHelper<char>.Size);
            Assert.AreEqual(0, TypeHelper<MyPocoWithConvertor>.Size);
            TypeHelper<MyPocoWithConvertor>.RegisterConverter(new MyPocoWithConvertor(), true);
            Assert.AreEqual(0, TypeHelper<MyPocoWithConvertor>.Size);
        }


        [Test]
        public void BlittableAttributesAreHonored() {

            Assert.AreEqual(-1, TypeHelper<NonBlittableStruct>.Size);
            Assert.AreEqual(4, TypeHelper<BlittableStruct1>.Size);
            Assert.AreEqual(4, TypeHelper<BlittableStruct2>.Size);

            // this will cause Environment.FailFast
            //Assert.AreEqual(4, TypeHelper<BlittableStructWrong>.Size);
        }


        [Test]
        public void ConversionTests() {
            // This are not unsafe but smart casting using cached Expressions
            var dbl = Convert.ToDouble((object)"123.0");
            Assert.AreEqual(123.0, dbl);
            dbl = TypeHelper<double>.ConvertFrom(new int?(123));
            Assert.AreEqual(123.0, dbl);
            dbl = TypeHelper<double>.ConvertFrom((object)(123));
            //Assert.AreEqual(123.0, dbl);
            // Note that the line below fails
            // dbl = (double) (object) (123);
        }
    }
}
