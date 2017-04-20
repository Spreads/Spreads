// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Newtonsoft.Json;
using NUnit.Framework;
using Spreads.DataTypes;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Spreads.Core.Tests
{
    [TestFixture]
    public class VariantTests
    {
        [KnownType(255)]
        private class TestKnownType : IEquatable<TestKnownType>
        {
            public int Int { get; set; }
            public string Str { get; set; }

            public bool Equals(TestKnownType other)
            {
                return other != null && (this.Int == other.Int && this.Str == other.Str);
            }
        }

        [Test]
        public void CouldCreateAndReadWriteInlinedVariant()
        {
            var v = Variant.Create(123.0);
            Assert.AreEqual(123.0, v.Get<double>());
            Assert.Throws<InvalidCastException>(() =>
            {
                v.Set(456); // no implicit conversion
            });
            v.Set(456.0);
            Assert.AreEqual(456.0, v.Get<double>());
            Assert.AreEqual(1, v.Count);

            // correctly parse boxed primitives
            var obj = (object)42.0;
            var v2 = Variant.Create(obj);
            Assert.AreEqual(v2.TypeEnum, TypeEnum.Float64);
            Assert.AreEqual(42.0, v2.Get<double>());
            Assert.AreEqual(1, v2.Count);

            var span = v2.Span<double>();
            Assert.AreEqual(42, span[0]);
            Assert.AreEqual(1, span.Length);
            span[0] = 43;
            Assert.AreEqual(43.0, v2.Get<double>());

            var x = Variant.Create(44.0);
            Assert.AreEqual(44.0, x.Get<double>());
            Assert.Throws<InvalidCastException>(() =>
            {
                v2[0] = Variant.Create(44); // no implicit conversion
            });
            v2[0] = Variant.Create(44.0);
            var v3 = v2[0];
            Assert.AreEqual(44.0, v3.Get<double>());
        }

        [Test]
        public void CouldCreateAndReadWriteArrayVariant()
        {
            var array = new double[2];
            array[0] = 123;
            array[1] = 456;

            var v = Variant.Create(array);
            Assert.AreEqual(TypeEnum.Array, v.TypeEnum);
            Assert.AreEqual(TypeEnum.Float64, v.ElementTypeEnum);
            Assert.AreEqual(8, v.ElementByteSize);
            Assert.AreEqual(2, v.Count);

            var boxedArray = (object)array;
            var v2 = Variant.Create(boxedArray);
            Assert.AreEqual(TypeEnum.Array, v2.TypeEnum);
            Assert.AreEqual(TypeEnum.Float64, v2.ElementTypeEnum);
            Assert.AreEqual(8, v2.ElementByteSize);
            Assert.AreEqual(2, v2.Count);

            var span = v2.Span<double>();
            Assert.AreEqual(123.0, span[0]);
            Assert.AreEqual(456.0, span[1]);
            Assert.AreEqual(2, span.Length);

            span[0] = 42;
            span[1] = 43;
            // array values are assigned via span
            Assert.AreEqual(42, array[0]);
            Assert.AreEqual(43, array[1]);

            var x = Variant.Create(44.0);
            Assert.AreEqual(TypeEnum.Float64, x.TypeEnum);
            v2[0] = x;
            v2[1] = Variant.Create(45.0);
            Assert.AreEqual(44, array[0]);
            Assert.AreEqual(45, array[1]);
        }

        #region Benchmarks

        [Test, Ignore]
        public void CouldCreateAndReadInlinedVariantInALoop()
        {
            var count = 100000000;
            var sw = new Stopwatch();
            for (int round = 0; round < 10; round++)
            {
                sw.Restart();
                var sum = 0.0;
                for (int i = 0; i < count; i++)
                {
                    var v = Variant.Create<double>(i);
                    var d = v.Get<double>();
                    sum += d;
                }
                sw.Stop();
                if (sum < double.MaxValue)
                    Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds}, Mops: {count / (sw.ElapsedMilliseconds * 0.001)}");
            }
        }

        private struct Container<T>
        {
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

        private struct BoxedContainer
        {
            private object _value;

            public object Value
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get { return _value; }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set { _value = value; }
            }
        }

        private struct BoxedDummyContainer
        {
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
        public void CouldCreateWriteReadInlinedVariantInALoop()
        {
            var count = 100000000;
            var sw = new Stopwatch();
            for (int round = 0; round < 10; round++)
            {
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
                for (int i = 0; i < count; i++)
                {
                    v.UnsafeSetInlined<double>(i);
                    var d = v.UnsafeGetInilned<double>();
                    sum += d;
                }
                sw.Stop();
                if (sum < double.MaxValue)
                    Console.WriteLine($"Unsafe Variant Elapsed: {sw.ElapsedMilliseconds}, Mops: {count / (sw.ElapsedMilliseconds * 0.001)}");

                sw.Restart();
                for (int i = 0; i < count; i++)
                {
                    var span = v.Span<double>();
                    span[0] = i;
                    var d = span[0];
                    sum += d;
                }
                sw.Stop();
                if (sum < double.MaxValue)
                    Console.WriteLine($"Span (Inline) Variant Elapsed: {sw.ElapsedMilliseconds}, Mops: {count / (sw.ElapsedMilliseconds * 0.001)}");

                var v2 = Variant.Create<double>(new[] { 0.0 });
                sw.Restart();
                for (int i = 0; i < count; i++)
                {
                    var span = v2.Span<double>();
                    span[0] = i;
                    var d = span[0];
                    sum += d;
                }
                sw.Stop();
                if (sum < double.MaxValue)
                    Console.WriteLine($"Span (Array) Variant Elapsed: {sw.ElapsedMilliseconds}, Mops: {count / (sw.ElapsedMilliseconds * 0.001)}");

                sw.Restart();
                for (int i = 0; i < count; i++)
                {
                    v2.Set<double>(0, 0.0);
                    var d = v2.Get<double>(0);
                    sum += d;
                }
                sw.Stop();
                if (sum < double.MaxValue)
                    Console.WriteLine($"Get/Set(idx) Variant Elapsed: {sw.ElapsedMilliseconds}, Mops: {count / (sw.ElapsedMilliseconds * 0.001)}");

                sw.Restart();
                sum = 0.0;
                var array = new[] { 0.0 };
                for (int i = 0; i < count; i++)
                {
                    array[0] = i;
                    var d = array[0];
                    sum += d;
                }
                sw.Stop();
                if (sum < double.MaxValue && array[0] > 0)
                    Console.WriteLine($"Array Elapsed: {sw.ElapsedMilliseconds}, Mops: {count / (sw.ElapsedMilliseconds * 0.001)}");

                sw.Restart();
                sum = 0.0;
                var container = new Container<double>();
                for (int i = 0; i < count; i++)
                {
                    container.Value = i;
                    var d = container.Value;
                    sum += d;
                }
                sw.Stop();
                if (sum < double.MaxValue && container.Value > 0)
                    Console.WriteLine($"Inlined Property Elapsed: {sw.ElapsedMilliseconds}, Mops: {count / (sw.ElapsedMilliseconds * 0.001)}");

                sw.Restart();
                sum = 0.0;
                var dummyContainer = new DummyContainer<double>();
                for (int i = 0; i < count; i++)
                {
                    dummyContainer.Value = i;
                    var d = dummyContainer.Value;
                    sum += d;
                }
                sw.Stop();
                if (sum < double.MaxValue && dummyContainer.Value > 0)
                    Console.WriteLine($"Noninlined Property Elapsed: {sw.ElapsedMilliseconds}, Mops: {count / (sw.ElapsedMilliseconds * 0.001)}");

                sw.Restart();
                sum = 0.0;
                var boxed = new BoxedContainer();
                for (int i = 0; i < count; i++)
                {
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
                for (int i = 0; i < count; i++)
                {
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

        #endregion Benchmarks

        [Test]
        public void CouldUseVariantJsonConverter()
        {
            var variant = Variant.Create(123);
            string json = JsonConvert.SerializeObject(variant);
            Console.WriteLine(json);

            var arrayVariant = Variant.Create(new[] { 123, 456 });
            var json2 = JsonConvert.SerializeObject(arrayVariant);
            Console.WriteLine(json2);

            var variant2 = JsonConvert.DeserializeObject<Variant>(json);
            Assert.AreEqual(123, variant2.Get<int>(0));
            Assert.AreEqual(123, variant2.Get<int>());

            var arrayVariant2 = JsonConvert.DeserializeObject<Variant>(json2);

            Assert.AreEqual(123, arrayVariant2.Get<int>(0));
            Assert.AreEqual(456, arrayVariant2.Get<int>(1));
        }

        [Test]
        public void CouldJsonConvertArrayOfVariants()
        {
            var arrayVariant = new Variant[] { Variant.Create(123), Variant.Create(456.7) };
            var json = JsonConvert.SerializeObject(arrayVariant);
            Console.WriteLine(json);

            var arrayVariant2 = JsonConvert.DeserializeObject<Variant[]>(json);

            Assert.AreEqual(123, arrayVariant2[0].Get<int>());
            Assert.AreEqual(123, arrayVariant2[0].Get<int>(0));
            Assert.AreEqual(456.7, arrayVariant2[1].Get<double>());
            Assert.AreEqual(456.7, arrayVariant2[1].Get<double>(0));
        }

        [Test]
        public void CouldJsonConvertVariantArrayOfVariants()
        {
            var arrayVariant = Variant.Create(new Variant[] { Variant.Create(123), Variant.Create(456.7) });
            var json = JsonConvert.SerializeObject(arrayVariant);
            Console.WriteLine(json);

            var arrayVariant2 = JsonConvert.DeserializeObject<Variant>(json);

            Assert.AreEqual(123, arrayVariant2[0].Get<int>());
            Assert.AreEqual(123, arrayVariant2[0].Get<int>(0));
            Assert.AreEqual(456.7, arrayVariant2[1].Get<double>());
            Assert.AreEqual(456.7, arrayVariant2[1].Get<double>(0));
        }

        [Test]
        public void CouldJsonConvertVariantArrayOfVariantsWithString()
        {
            var arrayVariant = Variant.Create(new Variant[] { Variant.Create(123), Variant.Create("456.7") });
            var arrayVariantNested = Variant.Create(new Variant[] { Variant.Create(arrayVariant), Variant.Create("456.7") });
            var json = JsonConvert.SerializeObject(arrayVariant);
            var json2 = JsonConvert.SerializeObject(arrayVariantNested);
            Console.WriteLine(json);
            Console.WriteLine(json2);

            var arrayVariant2 = JsonConvert.DeserializeObject<Variant>(json);

            Assert.AreEqual(123, arrayVariant2[0].Get<int>());
            Assert.AreEqual(123, arrayVariant2[0].Get<int>(0));
            Assert.AreEqual("456.7", arrayVariant2[1].Get<string>());
            Assert.AreEqual("456.7", arrayVariant2[1].Get<string>(0));
        }

        [Test]
        public void CouldJsonConvertVariantWithCustomObject()
        {
            var variant = Variant.Create(new TestKnownType { Int = 123, Str = "str" });
            var json = JsonConvert.SerializeObject(variant);
            Console.WriteLine(json);

            var variant2 = JsonConvert.DeserializeObject<Variant>(json);

            Assert.AreEqual(variant._object, variant2._object);
        }

        [Test]
        public void DefaultVariantHasTypeNone()
        {
            var d = default(Variant);
            Assert.AreEqual(TypeEnum.None, d.TypeEnum);
        }

        [Test]
        public void CouldCreateAndReadBool()
        {
            var b = Variant.Create(true);
            Assert.AreEqual(TypeEnum.Bool, b.TypeEnum);
            Assert.AreEqual(true, b.Get<bool>());
        }

        [Test]
        public void CouldCreateAndReadErrorCode()
        {
            var variant = Variant.Create(new ErrorCode { Code = 123456789 });
            Assert.AreEqual(TypeEnum.ErrorCode, variant.TypeEnum);
            Assert.AreEqual(123456789, variant.Get<ErrorCode>().Code);
            var obj = variant.ToObject();
            Assert.AreEqual(typeof(ErrorCode), obj.GetType());
        }
    }
}