// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Shouldly;
using Spreads.Collections.Generic;

namespace Spreads.Core.Tests.Collections
{
    [TestFixture]
    public class DictionarySlimTests
    {
        [Test]
        [TestCase(0, 2)]
        [TestCase(1, 2)]
        [TestCase(2, 2)]
        [TestCase(3, 4)]
        [TestCase(4, 4)]
        [TestCase(5, 8)]
        [TestCase(11, 16)]
        public void ConstructCapacity(int capacity, int expected)
        {
            var d = new DictionarySlim<ulong, int>(capacity);
            d.Count.ShouldBe(0);
            d.GetCapacity().ShouldBe(expected);
        }

        [Test]
        public void SingleEntry()
        {
            var d = new DictionarySlim<ulong, int>();
            d.GetOrAddValueRef(7)++;
            d.GetOrAddValueRef(7) += 3;
            d.GetOrAddValueRef(7).ShouldBe(4);
        }

        [Test]
        public void ContainKey()
        {
            var d = new DictionarySlim<ulong, int>();
            d.GetOrAddValueRef(7) = 9;
            d.GetOrAddValueRef(10) = 10;
            d.ContainsKey(7).ShouldBe(true);
            d.ContainsKey(10).ShouldBe(true);
            d.ContainsKey(1).ShouldBe(false);
        }

        [Test]
        public void TryGetValue_Present()
        {
            var d = new DictionarySlim<char, int>();
            d.GetOrAddValueRef('a') = 9;
            d.GetOrAddValueRef('b') = 11;
            d.TryGetValue('a', out int value).ShouldBe(true);
            value.ShouldBe(9);
            d.DangerousTryGetValue('a', out value).ShouldBe(true);
            value.ShouldBe(9);

            d.TryGetValue('b', out value).ShouldBe(true);
            value.ShouldBe(11);
            d.DangerousTryGetValue('b', out value).ShouldBe(true);
            value.ShouldBe(11);
        }

        [Test]
        public void TryGetValueRef_Present()
        {
            var d = new DictionarySlim<char, int>();
            d.GetOrAddValueRef('a') = 9;
            d.GetOrAddValueRef('b') = 11;
            d.TryGetValueRef('a', out var found).ShouldBe(9);
            found.ShouldBe(true);
            d.DangerousTryGetValueRef('a', out found).ShouldBe(9);
            found.ShouldBe(true);

            d.TryGetValueRef('b', out found).ShouldBe(11);
            found.ShouldBe(true);
            d.DangerousTryGetValueRef('b', out found).ShouldBe(11);
            found.ShouldBe(true);
        }

        [Test]
        [TestCase(0)]
        [TestCase(123)]
        public void TryGetValue_Missing(int defaultValue)
        {
            var d = new DictionarySlim<char, int>(0, defaultValue);
            d.GetOrAddValueRef('a') = 9;
            d.GetOrAddValueRef('b') = 11;
            d.Remove('b');

            d.TryGetValue('z', out int value).ShouldBe(false);
            value.ShouldBe(defaultValue);

            d.TryGetValue('b', out value).ShouldBe(false);
            value.ShouldBe(defaultValue);

            d.DangerousTryGetValue('z', out value).ShouldBe(false);
            value.ShouldBe(defaultValue);
            d.DangerousTryGetValue('b', out value).ShouldBe(false);
            value.ShouldBe(defaultValue);
        }

        [Test]
        [TestCase(0)]
        [TestCase(123)]
        public void TryGetValueRef_Missing(int defaultValue)
        {
            var d = new DictionarySlim<char, int>(0, defaultValue);
            d.GetOrAddValueRef('a') = 9;
            d.GetOrAddValueRef('b') = 11;
            d.Remove('b');

            d.TryGetValueRef('z', out var found).ShouldBe(defaultValue);
            found.ShouldBe(false);
            d.TryGetValueRef('b', out found).ShouldBe(defaultValue);
            found.ShouldBe(false);

            d.DangerousTryGetValueRef('z', out found).ShouldBe(defaultValue);
            found.ShouldBe(false);
            d.DangerousTryGetValueRef('b', out found).ShouldBe(defaultValue);
            found.ShouldBe(false);
        }

        [Test]
        [TestCase(null)]
        [TestCase("n/a")]
        public void TryGetValue_RefTypeValue(string? defaultValue)
        {
            var d = new DictionarySlim<int, string>(0, defaultValue);
            d.GetOrAddValueRef(1) = "a";
            d.GetOrAddValueRef(2) = "b";
            d.TryGetValue(1, out string value).ShouldBe(true);
            value.ShouldBe("a");
            d.DangerousTryGetValue(1, out value).ShouldBe(true);
            value.ShouldBe("a");

            d.TryGetValue(99, out value).ShouldBe(false);
            value.ShouldBe(defaultValue);
            d.DangerousTryGetValue(99, out value).ShouldBe(false);
            value.ShouldBe(defaultValue);
        }

        [Test]
        public void RemoveNonExistent()
        {
            var d = new DictionarySlim<int, int>();
            d.Remove(0).ShouldBe(false);
        }

        [Test]
        public void RemoveSimple()
        {
            var d = new DictionarySlim<int, int>();
            d.GetOrAddValueRef(0) = 0;
            d.Remove(0).ShouldBe(true);
            d.Count.ShouldBe(0);
        }

        [Test]
        public void RemoveOneOfTwo()
        {
            var d = new DictionarySlim<char, int>();
            d.GetOrAddValueRef('a') = 0;
            d.GetOrAddValueRef('b') = 1;
            d.Remove('a').ShouldBe(true);
            d.GetOrAddValueRef('b').ShouldBe(1);
            d.Count.ShouldBe(1);
        }

        [Test]
        public void RemoveThenAdd()
        {
            var d = new DictionarySlim<char, int>();
            d.GetOrAddValueRef('a') = 0;
            d.GetOrAddValueRef('b') = 1;
            d.GetOrAddValueRef('c') = 2;
            d.Remove('b').ShouldBe(true);
            d.GetOrAddValueRef('d') = 3;
            d.Count.ShouldBe(3);
            d.OrderBy(i => i.Key).Select(i => i.Key).ShouldBe(new[] { 'a', 'c', 'd' });
            d.OrderBy(i => i.Key).Select(i => i.Value).ShouldBe(new[] { 0, 2, 3 });
        }

        [Test]
        public void RemoveThenAddAndAddBack()
        {
            var d = new DictionarySlim<char, int>();
            d.GetOrAddValueRef('a') = 0;
            d.GetOrAddValueRef('b') = 1;
            d.GetOrAddValueRef('c') = 2;
            d.Remove('b').ShouldBe(true);
            d.GetOrAddValueRef('d') = 3;
            d.GetOrAddValueRef('b') = 7;
            d.Count.ShouldBe(4);
            d.OrderBy(i => i.Key).Select(i => i.Key).ShouldBe(new[] { 'a', 'b', 'c', 'd' });
            d.OrderBy(i => i.Key).Select(i => i.Value).ShouldBe(new[] { 0, 7, 2, 3 });
        }

        [Test]
        public void RemoveEnd()
        {
            var d = new DictionarySlim<char, int>();
            d.GetOrAddValueRef('a') = 0;
            d.GetOrAddValueRef('b') = 1;
            d.GetOrAddValueRef('c') = 2;
            d.Remove('c').ShouldBe(true);
            d.Count.ShouldBe(2);
            d.OrderBy(i => i.Key).Select(i => i.Key).ShouldBe(new[] { 'a', 'b' });
            d.OrderBy(i => i.Key).Select(i => i.Value).ShouldBe(new[] { 0, 1 });
        }

        [Test]
        public void RemoveEndTwice()
        {
            var d = new DictionarySlim<char, int>();
            d.GetOrAddValueRef('a') = 0;
            d.GetOrAddValueRef('b') = 1;
            d.GetOrAddValueRef('c') = 2;
            d.Remove('c').ShouldBe(true);
            d.Remove('b').ShouldBe(true);
            d.Count.ShouldBe(1);
            d.OrderBy(i => i.Key).Select(i => i.Key).ShouldBe(new[] { 'a' });
            d.OrderBy(i => i.Key).Select(i => i.Value).ShouldBe(new[] { 0 });
        }

        [Test]
        public void RemoveEndTwiceThenAdd()
        {
            var d = new DictionarySlim<char, int>();
            d.GetOrAddValueRef('a') = 0;
            d.GetOrAddValueRef('b') = 1;
            d.GetOrAddValueRef('c') = 2;
            d.Remove('c').ShouldBe(true);
            d.Remove('b').ShouldBe(true);
            d.GetOrAddValueRef('c') = 7;
            d.Count.ShouldBe(2);
            d.OrderBy(i => i.Key).Select(i => i.Key).ShouldBe(new[] { 'a', 'c' });
            d.OrderBy(i => i.Key).Select(i => i.Value).ShouldBe(new[] { 0, 7 });
        }

        [Test]
        public void RemoveSecondOfTwo()
        {
            var d = new DictionarySlim<char, int>();
            d.GetOrAddValueRef('a') = 0;
            d.GetOrAddValueRef('b') = 1;
            d.Remove('b').ShouldBe(true);
            d.GetOrAddValueRef('a').ShouldBe(0);
            d.Count.ShouldBe(1);
        }

        [Test]
        public void RemoveSlotReused()
        {
            var d = new DictionarySlim<Collider, int>();
            d.GetOrAddValueRef(C(0)) = 0;
            d.GetOrAddValueRef(C(1)) = 1;
            d.GetOrAddValueRef(C(2)) = 2;
            d.Remove(C(0)).ShouldBe(true);
            Console.WriteLine("{0} {1}", d.GetCapacity(), d.Count);
            var capacity = d.GetCapacity();

            d.GetOrAddValueRef(C(0)) = 3;
            Console.WriteLine("{0} {1}", d.GetCapacity(), d.Count);
            d.GetOrAddValueRef(C(0)).ShouldBe(3);
            d.Count.ShouldBe(3);
            d.GetCapacity().ShouldBe(capacity);

        }

        [Test]
        public void RemoveReleasesReferences()
        {
            var d = new DictionarySlim<KeyUseTracking, KeyUseTracking>();

            WeakReference<KeyUseTracking> a()
            {
                var kut = new KeyUseTracking(0);
                var wr = new WeakReference<KeyUseTracking>(kut);
                d.GetOrAddValueRef(kut) = kut;

                d.Remove(kut);
                return wr;
            }

            var ret = a();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            ret.TryGetTarget(out _).ShouldBe(false);
        }

        [Test]
        public void RemoveEnumerate()
        {
            var d = new DictionarySlim<int, int>();
            d.GetOrAddValueRef(0) = 0;
            d.Remove(0).ShouldBe(true);
            Assert.IsEmpty(d);
        }

        [Test]
        public void RemoveOneOfTwoEnumerate()
        {
            var d = new DictionarySlim<char, int>();
            d.GetOrAddValueRef('a') = 0;
            d.GetOrAddValueRef('b') = 1;
            d.Remove('a').ShouldBe(true);
            d.Single().ShouldBe(new KeyValuePair<char, int>('b', 1));
        }

        [Test]
        public void EnumerateCheckEnding()
        {
            var d = new DictionarySlim<int, int>();
            int i = 0;
            d.GetOrAddValueRef(++i) = -i;
            d.GetOrAddValueRef(++i) = -i;
            d.GetOrAddValueRef(++i) = -i;
            d.GetOrAddValueRef(++i) = -i;
            while (d.Count < d.GetCapacity())
                d.GetOrAddValueRef(++i) = -i;
            d.Count().ShouldBe(d.Count);
        }

        [Test]
        public void EnumerateCheckEndingRemoveLast()
        {
            var d = new DictionarySlim<int, int>();
            int i = 0;
            d.GetOrAddValueRef(++i) = -i;
            d.GetOrAddValueRef(++i) = -i;
            d.GetOrAddValueRef(++i) = -i;
            d.GetOrAddValueRef(++i) = -i;
            while (d.Count < d.GetCapacity())
                d.GetOrAddValueRef(++i) = -i;
            d.Remove(i).ShouldBe(true);
            d.Count().ShouldBe(d.Count);
        }

        private KeyValuePair<TKey, TValue> P<TKey, TValue>(TKey key, TValue value) => new KeyValuePair<TKey, TValue>(key, value);

        [Test]
        public void EnumerateReset()
        {
            var d = new DictionarySlim<int, int>();
            d.GetOrAddValueRef(1) = 10;
            d.GetOrAddValueRef(2) = 20;
            IEnumerator<KeyValuePair<int, int>> e = d.GetEnumerator();
            e.Current.ShouldBe(P(0, 0));
            e.MoveNext().ShouldBe(true);
            e.Current.ShouldBe(P(1, 10));
            e.Reset();
            e.Current.ShouldBe(P(0, 0));
            e.MoveNext().ShouldBe(true);
            e.MoveNext().ShouldBe(true);
            e.Current.ShouldBe(P(2, 20));
            e.MoveNext().ShouldBe(false);
            e.Reset();
            e.Current.ShouldBe(P(0, 0));
        }

        [Test]
        public void Clear()
        {
            var d = new DictionarySlim<int, int>();
            d.GetCapacity().ShouldBe(1);
            d.GetOrAddValueRef(1) = 10;
            d.GetOrAddValueRef(2) = 20;
            d.Count.ShouldBe(2);
            d.GetCapacity().ShouldBe(2);
            d.Clear();
            d.Count.ShouldBe(0);
            d.ContainsKey(1).ShouldBe(false);
            d.ContainsKey(2).ShouldBe(false);
            d.GetCapacity().ShouldBe(1);
        }

        [Test]
        public void DictionarySlimVersusDictionary()
        {
            var rand = new Random(1123);
            var rd = new DictionarySlim<ulong, int>();
            var d = new Dictionary<ulong, int>();
            var size = 1000;

            for (int i = 0; i < size; i++)
            {
                var k = (ulong)rand.Next(100) + 23;
                var v = rand.Next();

                unchecked
                {
                    rd.GetOrAddValueRef(k) += v;

                    if (d.TryGetValue(k, out int t))
                        d[k] = t + v;
                    else
                        d[k] = v;
                }
            }

            rd.Count.ShouldBe(d.Count);
            rd.OrderBy(i => i.Key).ShouldBe(d.OrderBy(i => i.Key));
            rd.OrderBy(i => i.Value).ShouldBe(d.OrderBy(i => i.Value));
        }

        [Test]
        public void DictionarySlimVersusDictionary_AllCollisions()
        {
            var rand = new Random(333);
            var rd = new DictionarySlim<Collider, int>();
            var d = new Dictionary<Collider, int>();
            var size = rand.Next(1234);

            for (int i = 0; i < size; i++)
            {
                if (rand.Next(5) != 0)
                {
                    var k = C(rand.Next(100) + 23);
                    var v = rand.Next();

                    unchecked
                    {
                        rd.GetOrAddValueRef(k) += v;

                        if (d.TryGetValue(k, out int t))
                            d[k] = t + v;
                        else
                            d[k] = v;
                    }
                }

                if (rand.Next(3) == 0 && d.Count > 0)
                {
                    var el = GetRandomElement(d);
                    rd.Remove(el).ShouldBe(true);
                    d.Remove(el).ShouldBe(true);
                }
            }

            rd.Count.ShouldBe(d.Count);
            rd.OrderBy(i => i.Key).ShouldBe(d.OrderBy(i => i.Key));
            rd.OrderBy(i => i.Value).ShouldBe(d.OrderBy(i => i.Value));
        }

        private TKey GetRandomElement<TKey, TValue>(IDictionary<TKey, TValue> d)
        {
            int index = 0;
            var rand = new Random(42);
            foreach (var entry in d)
            {
                if (rand.Next(d.Count) == 0 || index == d.Count - 1)
                {
                    return entry.Key;
                }

                index++;
            }

            throw new InvalidOperationException();
        }

        [DebuggerStepThrough]
        internal Collider C(int val) => new Collider(val);

        [Test]
        public void Collision()
        {
            var d = new DictionarySlim<Collider, int>();
            d.GetOrAddValueRef(C(5)) = 3;
            d.GetOrAddValueRef(C(7)) = 9;
            d.GetOrAddValueRef(C(10)) = 11;

            d.GetOrAddValueRef(C(5)).ShouldBe(3);
            d.GetOrAddValueRef(C(7)).ShouldBe(9);
            d.GetOrAddValueRef(C(10)).ShouldBe(11);

            d.GetOrAddValueRef(C(23))++;
            d.GetOrAddValueRef(C(23)) += 3;
            d.GetOrAddValueRef(C(23)).ShouldBe(4);
        }

        [Test]
        public void UsedIEquatable()
        {
            var d = new DictionarySlim<KeyUseTracking, int>();
            var key = new KeyUseTracking(5);
            d.GetOrAddValueRef(key)++;
            key.GetHashCodeCount.ShouldBe(2);
            key.EqualsCount.ShouldBe(0);
        }

        [Test]
        public void UsedIEquatable2()
        {
            var d = new DictionarySlim<KeyUseTracking, int>();
            var key = new KeyUseTracking(5);
            d.GetOrAddValueRef(key)++;
            d.GetOrAddValueRef(key)++;
            key.GetHashCodeCount.ShouldBe(3);
            key.EqualsCount.ShouldBe(1);
        }
    }

    internal static class DictionarySlimExtensions
    {
        // Capacity is not exposed publicly, but is valuable in tests to help
        // ensure everything is working as expected internally
        public static int GetCapacity<TKey, TValue>(this DictionarySlim<TKey, TValue> dict) where TKey : IEquatable<TKey>
        {
            FieldInfo fi = typeof(DictionarySlim<TKey, TValue>).GetField("_entries", BindingFlags.NonPublic | BindingFlags.Instance);
            Object entries = fi.GetValue(dict);

            PropertyInfo pi = typeof(Array).GetProperty("Length");
            return (int)pi.GetValue(entries);
        }
    }

    [DebuggerDisplay("{key}")]
    internal struct Collider : IEquatable<Collider>, IComparable<Collider>
    {
        int key;

        [DebuggerStepThrough]
        internal Collider(int key)
        {
            this.key = key;
        }

        internal int Key => key;

        [DebuggerStepThrough]
        public override int GetHashCode() => 42;

        public override bool Equals(object obj) => obj.GetType() == typeof(Collider) && Equals((Collider)obj);

        public bool Equals(Collider that) => that.Key == Key;

        public int CompareTo(Collider that) => key.CompareTo(that.key);

        public override string ToString() => Convert.ToString(key);
    }

    [DebuggerDisplay("{Value}")]
    internal class KeyUseTracking : IEquatable<KeyUseTracking>
    {
        public int Value { get; }
        public int EqualsCount { get; private set; }
        public int GetHashCodeCount { get; private set; }

        public KeyUseTracking(int v)
        {
            Value = v;
        }

        public bool Equals(KeyUseTracking o)
        {
            EqualsCount++;
            return Value == o.Value;
        }

        public override bool Equals(object o)
        {
            return o is KeyUseTracking ck && Value == ck.Value;
        }

        public override int GetHashCode()
        {
            GetHashCodeCount++;
            return Value;
        }
    }
}
