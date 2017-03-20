// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;
using System.Runtime.InteropServices;
using System.Threading;
using Spreads.Buffers;
using Spreads.Serialization;
using System.Runtime.CompilerServices;
using Spreads.Utils;

namespace Spreads.Core.Tests
{
    public class TypeHelperTests
    {
        public struct NonBlittableStruct
        {
            public int Value1;
        }

        [Serialization(BlittableSize = 4)]
        public struct BlittableStruct1
        {
            public int Value1;
        }

        [StructLayout(LayoutKind.Sequential, Size = 4)]
        public struct BlittableStructWithoutAttribute
        {
            public int Value1;
        }

        public class MyPoco
        {
            public string String { get; set; }
            public long Long { get; set; }
        }

        [Serialization(BlittableSize = 5)]
        public struct BlittableStructWrong
        {
            public int Value1;
        }

        public class MyPocoWithConvertor : IBinaryConverter<MyPocoWithConvertor>
        {
            public string String { get; set; }
            public long Long { get; set; }
            public bool IsFixedSize => false;
            public int Size => 0;
            public byte Version => 1;

            public int SizeOf(MyPocoWithConvertor value, out MemoryStream temporaryStream, CompressionMethod compression = CompressionMethod.DefaultOrNone)
            {
                throw new NotImplementedException();
            }

            public int Write(MyPocoWithConvertor value, ref DirectBuffer destination, uint offset = 0, MemoryStream temporaryStream = null, CompressionMethod compression = CompressionMethod.DefaultOrNone)
            {
                throw new NotImplementedException();
            }

            public int Read(IntPtr ptr, ref MyPocoWithConvertor value)
            {
                throw new NotImplementedException();
            }
        }

        // using System.Runtime.CompilerServices.Unsafe;
        [Fact]
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
                Assert.Equal(DateTime.UtcNow.Date, firstDate);
                Console.WriteLine(firstDate);
                var firstDecimal = *(decimal*)(ptr + 8);
                Assert.Equal(123.456M, firstDecimal);
                Console.WriteLine(firstDecimal);
                var secondDate = *(DateTime*)(ptr + 8 + 16);
                Assert.Equal(DateTime.UtcNow.Date.AddDays(1), secondDate);
                Console.WriteLine(secondDate);
                var secondDecimal = *(decimal*)(ptr + 8 + 16 + 8);
                Assert.Equal(789.101M, secondDecimal);
                Console.WriteLine(secondDecimal);
            }

            var ptr2 = Marshal.AllocHGlobal(1000);
            var myPoco = new MyPoco();
            Unsafe.Write((void*)ptr2, myPoco);
        }

        [Fact]
        public void CouldGetSizeOfDoubleArray()
        {
            Console.WriteLine(TypeHelper<double[]>.Size);
        }

        [Fact]
        public void CouldGetSizeOfReferenceType()
        {
            Console.WriteLine(TypeHelper<string>.Size);
        }

        [Fact]
        public void CouldWriteBlittableStruct1()
        {
            var ptr = Marshal.AllocHGlobal(1024);
            var dest = new DirectBuffer(1024, ptr);
            var myBlittableStruct1 = new BlittableStruct1
            {
                Value1 = 12345
            };
            TypeHelper<BlittableStruct1>.Write(myBlittableStruct1, ref dest);
            var newBlittableStruct1 = default(BlittableStruct1);
            TypeHelper<BlittableStruct1>.Read(ptr, ref newBlittableStruct1);
            Assert.Equal(myBlittableStruct1.Value1, newBlittableStruct1.Value1);
        }

        // TODO extension method for T
        //[Fact]
        //public void CouldWritePOCOToBuffer() {
        //    var ptr = Marshal.AllocHGlobal(1024);
        //    var buffer = new DirectBuffer(1024, ptr);
        //    var myPoco = new MyPoco {
        //        String = "MyString",
        //        Long = 123
        //    };
        //    buffer.Write(0, myPoco);
        //    var newPoco = buffer.Read<MyPoco>(0);
        //    Assert.Equal(myPoco.String, newPoco.String);
        //    Assert.Equal(myPoco.Long, newPoco.Long);

        //}

        [Fact]
        public void CouldWriteArray()
        {
            var ptr = Marshal.AllocHGlobal(1024);
            var dest = new DirectBuffer(1024, ptr);
            var myArray = new int[2];
            myArray[0] = 123;
            myArray[1] = 456;

            TypeHelper<int[]>.Write(myArray, ref dest);

            var newArray = default(int[]);
            TypeHelper<int[]>.Read(ptr, ref newArray);
            Assert.True(myArray.SequenceEqual(newArray));
        }

        // TODO Extension method for Write<T>
        //[Fact]
        //public void CouldWriteArrayToBuffer() {
        //    var ptr = Marshal.AllocHGlobal(1024);
        //    var buffer = new DirectBuffer(1024, ptr);
        //    var myArray = new int[2];
        //    myArray[0] = 123;
        //    myArray[1] = 456;

        //    buffer.Write(0, myArray);
        //    var newArray = buffer.Read<int[]>(0);
        //    Assert.True(myArray.SequenceEqual(newArray));

        //}

        // TODO extension method for T
        //[Fact]
        //public void CouldWriteComplexTypeWithConverterToBuffer() {
        //    var ptr = Marshal.AllocHGlobal(1024);
        //    var buffer = new DirectBuffer(1024, ptr);

        //    var myStruct = new SetRemoveCommandBody<long, string>() {
        //        key = 123,
        //        value = "string value"
        //    };

        //    buffer.Write(0, myStruct);
        //    var newStruct = buffer.Read<SetRemoveCommandBody<long, string>>(0);
        //    Assert.Equal(myStruct.key, newStruct.key);
        //    Assert.Equal(myStruct.value, newStruct.value);

        //}

        [Fact]
        public void CouldCreateNongenericDelegates()
        {
            var ptr = Marshal.AllocHGlobal(1024);
            var buffer = new DirectBuffer(1024, ptr);

            var fromPtrInt = TypeHelper.GetFromPtrDelegate(typeof(int));

            TypeHelper<int>.Write(12345, ref buffer);

            object res = null;
            fromPtrInt(ptr, ref res);
            Assert.Equal((int)res, 12345);

            var toPtrInt = TypeHelper.GetToPtrDelegate(typeof(int));
            toPtrInt(42, ref buffer);

            int temp = 0;
            TypeHelper<int>.Read(ptr, ref temp);
            Assert.Equal(42, temp);

            var sizeOfInt = TypeHelper.GetSizeOfDelegate(typeof(int));
            MemoryStream tmp;
            Assert.Equal(sizeOfInt(42, out tmp), 4);
            Assert.Null(tmp);

            Assert.Equal(4, TypeHelper.GetSize(typeof(int)));
            Assert.Equal(0, TypeHelper.GetSize(typeof(string)));
            Assert.Equal(-1, TypeHelper.GetSize(typeof(LinkedList<int>)));
        }

        [Fact]
        public void CouldGetSizeOfPrimitivesDateTimeAndDecimal()
        {
            Assert.Equal(4, TypeHelper<int>.Size);
            Assert.Equal(8, TypeHelper<DateTime>.Size);
            Assert.Equal(16, TypeHelper<decimal>.Size);
            Assert.Equal(-1, TypeHelper<char>.Size);
            Assert.Equal(0, TypeHelper<MyPocoWithConvertor>.Size);
            TypeHelper<MyPocoWithConvertor>.RegisterConverter(new MyPocoWithConvertor(), true);
            Assert.Equal(0, TypeHelper<MyPocoWithConvertor>.Size);
        }

        [Fact]
        public void BlittableAttributesAreHonored()
        {
            Assert.Equal(-1, TypeHelper<NonBlittableStruct>.Size);
            Assert.Equal(4, TypeHelper<BlittableStruct1>.Size);
            Assert.Equal(-1, TypeHelper<BlittableStructWithoutAttribute>.Size);

            // this will cause Environment.FailFast
            //Assert.Equal(4, TypeHelper<BlittableStructWrong>.Size);
        }

        [Fact(Skip = "Linq.Expressions conversions is not working")]
        public void ConversionTests()
        {
            // This are not unsafe but smart casting using cached Expressions
            var dbl = Convert.ToDouble((object)"123.0");
            Assert.Equal(123.0, dbl);
            dbl = TypeHelper<double>.ConvertFrom(new int?(123));
            Assert.Equal(123.0, dbl);
            dbl = TypeHelper<double>.ConvertFrom((object)(123));
            //Assert.Equal(123.0, dbl);
            // Note that the line below fails
            // dbl = (double) (object) (123);
        }
    }
}