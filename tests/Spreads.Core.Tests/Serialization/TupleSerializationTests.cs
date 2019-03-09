// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.DataTypes;
using Spreads.Serialization;
using Spreads.Utils;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Linq.Expressions;
using Spreads.Buffers;
using Spreads.Threading;

namespace Spreads.Core.Tests.Serialization
{
    [TestFixture]
    public class TupleSerializationTests
    {
        //[Test]
        //public void ExpressionTest()
        //{
        //    Expression<Func<(int, long), long>> ex = ExpressionMethod(x => x.Item1 + x.Item2);

        //}

        //public Expression<Func<(int, long), long>> ExpressionMethod(Func<(int, long), long> ex)
        //{
        //    return (Expression<Func<(int, long), long>>)ex;
        //}



        [Test]
        public void CouldSerializeTaggedKeyValue()
        {
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            (int,long) val = (10, 20L);
            
            var serializationFormats = new[] { SerializationFormat.Binary }; // Enum.GetValues(typeof(SerializationFormat)).Cast<SerializationFormat>()};

            foreach (var serializationFormat in serializationFormats)
            {
                db.Write(0, 0);

                var len = BinarySerializer.SizeOf(val, out var tempBuf, serializationFormat);
                Assert.AreEqual(12, len);

                var len2 = BinarySerializer.Write(val, db, tempBuf, serializationFormat);
                Assert.AreEqual(4 + 12, len2);

                Assert.AreEqual(len + 4, len2);


                var len3 = BinarySerializer.Read(db, out (int first, long second) val2);

                

                Assert.AreEqual(len2, len3);

                Assert.AreEqual(val, val2);

                
            }

            rm.Dispose();
        }

        [Test]
        public void TupleSize()
        {
            Console.WriteLine(Unsafe.SizeOf<(byte, long)>());
            Console.WriteLine(TypeHelper<(byte, long)>.FixedSize);
            Console.WriteLine(Unsafe.SizeOf<TupleTest<byte, long>>());
            Console.WriteLine(TypeHelper<TupleTest<byte, long>>.FixedSize);

            Console.WriteLine(Unsafe.SizeOf<(byte, long, string)>());

            Console.WriteLine(TypeHelper<DateTime>.PinnedSize);
        }

        // ReSharper disable  InconsistentNaming
        [BinarySerialization(preferBlittable: true)]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct TupleTest<A, B> //, C, D, E, F
        {
            public A a;
            public B b;
            //public C c;
            //public D d;
            //public E e;
            //public F f;

            //public Test(ValueTuple<A, B, C, D, E, F> tuple)
            //{
            //    (a, b, c, d, e, f) = tuple;

            //}

            public TupleTest((A a, B b) tuple) //, C c, D d, E e, F f
            {
                (a, b) = tuple; //, c, d, e, f
            }
        }
    }
}
