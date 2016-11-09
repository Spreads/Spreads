// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.DataTypes;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Spreads.Core.Tests {

    [TestFixture]
    public class VariantTests {

        [Test]
        public void CouldCreateAndReadWriteInlinedVariant() {
            var v = Variant.Create<double>(123);
            Assert.AreEqual(123.0, v.Get<double>());
            Assert.Throws<InvalidOperationException>(() => {
                v.Set(456); // no implicit conversion
            });
            v.Set(456.0);
            Assert.AreEqual(456.0, v.Get<double>());

            // correctly parse boxed primitives
            var obj = (object)42.0;
            var v2 = Variant.Create(obj);
            Assert.AreEqual(v2.TypeEnum, TypeEnum.Float64);
            Assert.AreEqual(42.0, v2.Get<double>());
        }

        [Test, Ignore]
        public void CouldCreateAndReadInlinedVariantInALoop() {
            var count = 100000000;
            var sw = new Stopwatch();
            for (int round = 0; round < 10; round++) {
                sw.Restart();
                var sum = 0.0;
                for (int i = 0; i < count; i++) {
                    var v = Variant.Create<double>(i);
                    var d = v.Get<double>();
                    sum += d;
                }
                sw.Stop();
                if (sum < double.MaxValue)
                    Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds}, Mops: {count / (sw.ElapsedMilliseconds * 0.001)}");
            }
        }

        private struct Container<T> {
            private T _value;

            public T Value
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get { return _value; }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set { _value = value; }
            }
        }

        // not inlined access to the field
        private struct DummyContainer<T>
        {
            private T _value;

            public T Value
            {
                [MethodImpl(MethodImplOptions.NoInlining)]
                get { return _value; }
                [MethodImpl(MethodImplOptions.NoInlining)]
                set { _value = value; }
            }
        }

        private struct BoxedContainer {
            private object _value;

            public object Value
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get { return _value; }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set { _value = value; }
            }
        }

        private struct BoxedDummyContainer {
            private object _value;

            public object Value
            {
                [MethodImpl(MethodImplOptions.NoInlining)]
                get { return _value; }
                [MethodImpl(MethodImplOptions.NoInlining)]
                set { _value = value; }
            }
        }

        [Test, Ignore]
        public void CouldCreateWriteReadInlinedVariantInALoop() {
            var count = 100000000;
            var sw = new Stopwatch();
            for (int round = 0; round < 10; round++) {
                sw.Restart();
                var sum = 0.0;
                var v = Variant.Create<double>(0.0);
                for (int i = 0; i < count; i++)
                {
                    v.Set<double>(i);
                    var d = v.Get<double>();
                    sum += d;
                }
                sw.Stop();
                if (sum < double.MaxValue)
                    Console.WriteLine($"Variant Elapsed: {sw.ElapsedMilliseconds}, Mops: {count / (sw.ElapsedMilliseconds * 0.001)}");


                sw.Restart();
                sum = 0.0;
                var container = new Container<double>();
                for (int i = 0; i < count; i++) {
                    container.Value = i;
                    var d = container.Value;
                    sum += d;
                }
                sw.Stop();
                if (sum < double.MaxValue)
                    Console.WriteLine($"Inlined Property Elapsed: {sw.ElapsedMilliseconds}, Mops: {count / (sw.ElapsedMilliseconds * 0.001)}");


                sw.Restart();
                sum = 0.0;
                var dummyContainer = new DummyContainer<double>();
                for (int i = 0; i < count; i++) {
                    dummyContainer.Value = i;
                    var d = dummyContainer.Value;
                    sum += d;
                }
                sw.Stop();
                if (sum < double.MaxValue)
                    Console.WriteLine($"Noninlined Property Elapsed: {sw.ElapsedMilliseconds}, Mops: {count / (sw.ElapsedMilliseconds * 0.001)}");

                sw.Restart();
                sum = 0.0;
                var boxed = new BoxedContainer();
                for (int i = 0; i < count; i++) {
                    boxed.Value = (double)i;
                    var d = (double)boxed.Value;
                    sum += d;
                }
                sw.Stop();
                if (sum < double.MaxValue)
                    Console.WriteLine($"Inlined Boxed Elapsed: {sw.ElapsedMilliseconds}, Mops: {count / (sw.ElapsedMilliseconds * 0.001)}");


                sw.Restart();
                sum = 0.0;
                var boxedDummy = new BoxedDummyContainer();
                for (int i = 0; i < count; i++) {
                    boxedDummy.Value = (double)i;
                    var d = (double)boxed.Value;
                    sum += d;
                }
                sw.Stop();
                if (sum < double.MaxValue)
                    Console.WriteLine($"Noninlined Boxed Elapsed: {sw.ElapsedMilliseconds}, Mops: {count / (sw.ElapsedMilliseconds * 0.001)}");

                Console.WriteLine("---------------------------------");
            }
        }


    }
}
