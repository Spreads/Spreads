// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
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
            Assert.AreEqual(0, d.Count);
            Assert.AreEqual(expected, d.GetCapacity());
        }

        [Test]
        public void SingleEntry()
        {
            var d = new DictionarySlim<ulong, int>();
            d.GetOrAddValueRef(7)++;
            d.GetOrAddValueRef(7) += 3;
            Assert.AreEqual(4, d.GetOrAddValueRef(7));
        }

        [Test]
        public void ContainKey()
        {
            var d = new DictionarySlim<ulong, int>();
            d.GetOrAddValueRef(7) = 9;
            d.GetOrAddValueRef(10) = 10;
            Assert.True(d.ContainsKey(7));
            Assert.True(d.ContainsKey(10));
            Assert.False(d.ContainsKey(1));
        }

        [Test]
        public void TryGetValue_Present()
        {
            var d = new DictionarySlim<char, int>();
            d.GetOrAddValueRef('a') = 9;
            d.GetOrAddValueRef('b') = 11;
            Assert.AreEqual(true, d.TryGetValue('a', out int value));
            Assert.AreEqual(9, value);
            Assert.AreEqual(true, d.DangerousTryGetValue('a', out value));
            Assert.AreEqual(9, value);

            Assert.AreEqual(true, d.TryGetValue('b', out value));
            Assert.AreEqual(11, value);
            Assert.AreEqual(true, d.DangerousTryGetValue('b', out value));
            Assert.AreEqual(11, value);
        }

        [Test]
        public void TryGetValueRef_Present()
        {
            var d = new DictionarySlim<char, int>();
            d.GetOrAddValueRef('a') = 9;
            d.GetOrAddValueRef('b') = 11;
            Assert.AreEqual(9, d.TryGetValueRef('a', out var found));
            Assert.AreEqual(true, found);
            Assert.AreEqual(9, d.DangerousTryGetValueRef('a', out found));
            Assert.AreEqual(true, found);

            Assert.AreEqual(11, d.TryGetValueRef('b', out found));
            Assert.AreEqual(true, found);
            Assert.AreEqual(11, d.DangerousTryGetValueRef('b', out found));
            Assert.AreEqual(true, found);
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
            Assert.AreEqual(false, d.TryGetValue('z', out int value));
            Assert.AreEqual(defaultValue, value);
            Assert.AreEqual(false, d.TryGetValue('b', out value));
            Assert.AreEqual(defaultValue, value);

            Assert.AreEqual(false, d.DangerousTryGetValue('z', out value));
            Assert.AreEqual(defaultValue, value);
            Assert.AreEqual(false, d.DangerousTryGetValue('b', out value));
            Assert.AreEqual(defaultValue, value);
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
            Assert.AreEqual(defaultValue, d.TryGetValueRef('z', out var found));
            Assert.AreEqual(false, found);
            Assert.AreEqual(defaultValue, d.TryGetValueRef('b', out found));
            Assert.AreEqual(false, found);

            Assert.AreEqual(defaultValue, d.DangerousTryGetValueRef('z', out found));
            Assert.AreEqual(false, found);
            Assert.AreEqual(defaultValue, d.DangerousTryGetValueRef('b', out found));
            Assert.AreEqual(false, found);
        }

        [Test]
        [TestCase(null)]
        [TestCase("n/a")]
        public void TryGetValue_RefTypeValue(string? defaultValue)
        {
            var d = new DictionarySlim<int, string>(0, defaultValue);
            d.GetOrAddValueRef(1) = "a";
            d.GetOrAddValueRef(2) = "b";
            Assert.AreEqual(true, d.TryGetValue(1, out string value));
            Assert.AreEqual("a", value);
            Assert.AreEqual(true, d.DangerousTryGetValue(1, out value));
            Assert.AreEqual("a", value);

            Assert.AreEqual(false, d.TryGetValue(99, out value));
            Assert.AreEqual(defaultValue, value);
            Assert.AreEqual(false, d.DangerousTryGetValue(99, out value));
            Assert.AreEqual(defaultValue, value);
        }

        [Test]
        public void RemoveNonExistent()
        {
            var d = new DictionarySlim<int, int>();
            Assert.False(d.Remove(0));
        }

        [Test]
        public void RemoveSimple()
        {
            var d = new DictionarySlim<int, int>();
            d.GetOrAddValueRef(0) = 0;
            Assert.True(d.Remove(0));
            Assert.AreEqual(0, d.Count);
        }

        [Test]
        public void RemoveOneOfTwo()
        {
            var d = new DictionarySlim<char, int>();
            d.GetOrAddValueRef('a') = 0;
            d.GetOrAddValueRef('b') = 1;
            Assert.True(d.Remove('a'));
            Assert.AreEqual(1, d.GetOrAddValueRef('b'));
            Assert.AreEqual(1, d.Count);
        }

        [Test]
        public void RemoveThenAdd()
        {
            var d = new DictionarySlim<char, int>();
            d.GetOrAddValueRef('a') = 0;
            d.GetOrAddValueRef('b') = 1;
            d.GetOrAddValueRef('c') = 2;
            Assert.True(d.Remove('b'));
            d.GetOrAddValueRef('d') = 3;
            Assert.AreEqual(3, d.Count);
            Assert.AreEqual(new[] {'a', 'c', 'd' }, d.OrderBy(i => i.Key).Select(i => i.Key));
            Assert.AreEqual(new[] { 0, 2, 3 }, d.OrderBy(i => i.Key).Select(i => i.Value));
        }

        [Test]
        public void RemoveThenAddAndAddBack()
        {
            var d = new DictionarySlim<char, int>();
            d.GetOrAddValueRef('a') = 0;
            d.GetOrAddValueRef('b') = 1;
            d.GetOrAddValueRef('c') = 2;
            Assert.True(d.Remove('b'));
            d.GetOrAddValueRef('d') = 3;
            d.GetOrAddValueRef('b') = 7;
            Assert.AreEqual(4, d.Count);
            Assert.AreEqual(new[] { 'a', 'b', 'c', 'd' }, d.OrderBy(i => i.Key).Select(i => i.Key));
            Assert.AreEqual(new[] { 0, 7, 2, 3 }, d.OrderBy(i => i.Key).Select(i => i.Value));
        }

        [Test]
        public void RemoveEnd()
        {
            var d = new DictionarySlim<char, int>();
            d.GetOrAddValueRef('a') = 0;
            d.GetOrAddValueRef('b') = 1;
            d.GetOrAddValueRef('c') = 2;
            Assert.True(d.Remove('c'));
            Assert.AreEqual(2, d.Count);
            Assert.AreEqual(new[] { 'a', 'b' }, d.OrderBy(i => i.Key).Select(i => i.Key));
            Assert.AreEqual(new[] { 0, 1 }, d.OrderBy(i => i.Key).Select(i => i.Value));
        }

        [Test]
        public void RemoveEndTwice()
        {
            var d = new DictionarySlim<char, int>();
            d.GetOrAddValueRef('a') = 0;
            d.GetOrAddValueRef('b') = 1;
            d.GetOrAddValueRef('c') = 2;
            Assert.True(d.Remove('c'));
            Assert.True(d.Remove('b'));
            Assert.AreEqual(1, d.Count);
            Assert.AreEqual(new[] { 'a' }, d.OrderBy(i => i.Key).Select(i => i.Key));
            Assert.AreEqual(new[] { 0 }, d.OrderBy(i => i.Key).Select(i => i.Value));
        }

        [Test]
        public void RemoveEndTwiceThenAdd()
        {
            var d = new DictionarySlim<char, int>();
            d.GetOrAddValueRef('a') = 0;
            d.GetOrAddValueRef('b') = 1;
            d.GetOrAddValueRef('c') = 2;
            Assert.True(d.Remove('c'));
            Assert.True(d.Remove('b'));
            d.GetOrAddValueRef('c') = 7;
            Assert.AreEqual(2, d.Count);
            Assert.AreEqual(new[] { 'a', 'c' }, d.OrderBy(i => i.Key).Select(i => i.Key));
            Assert.AreEqual(new[] { 0, 7 }, d.OrderBy(i => i.Key).Select(i => i.Value));
        }

        [Test]
        public void RemoveSecondOfTwo()
        {
            var d = new DictionarySlim<char, int>();
            d.GetOrAddValueRef('a') = 0;
            d.GetOrAddValueRef('b') = 1;
            Assert.True(d.Remove('b'));
            Assert.AreEqual(0, d.GetOrAddValueRef('a'));
            Assert.AreEqual(1, d.Count);
        }

        [Test]
        public void RemoveSlotReused()
        {
            var d = new DictionarySlim<Collider, int>();
            d.GetOrAddValueRef(C(0)) = 0;
            d.GetOrAddValueRef(C(1)) = 1;
            d.GetOrAddValueRef(C(2)) = 2;
            Assert.True(d.Remove(C(0)));
            Console.WriteLine("{0} {1}", d.GetCapacity(), d.Count);
            var capacity = d.GetCapacity();

            d.GetOrAddValueRef(C(0)) = 3;
            Console.WriteLine("{0} {1}", d.GetCapacity(), d.Count);
            Assert.AreEqual(d.GetOrAddValueRef(C(0)), 3);
            Assert.AreEqual(3, d.Count);
            Assert.AreEqual(capacity, d.GetCapacity());

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
            Assert.False(ret.TryGetTarget(out _));
        }

        [Test]
        public void RemoveEnumerate()
        {
            var d = new DictionarySlim<int, int>();
            d.GetOrAddValueRef(0) = 0;
            Assert.True(d.Remove(0));
            Assert.IsEmpty(d);
        }

        [Test]
        public void RemoveOneOfTwoEnumerate()
        {
            var d = new DictionarySlim<char, int>();
            d.GetOrAddValueRef('a') = 0;
            d.GetOrAddValueRef('b') = 1;
            Assert.True(d.Remove('a'));
            Assert.AreEqual(KeyValuePair.Create('b', 1), d.Single());
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
            Assert.AreEqual(d.Count, d.Count());
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
            Assert.True(d.Remove(i));
            Assert.AreEqual(d.Count, d.Count());
        }

        private KeyValuePair<TKey, TValue> P<TKey, TValue>(TKey key, TValue value) => new KeyValuePair<TKey, TValue>(key, value);

        [Test]
        public void EnumerateReset()
        {
            var d = new DictionarySlim<int, int>();
            d.GetOrAddValueRef(1) = 10;
            d.GetOrAddValueRef(2) = 20;
            IEnumerator<KeyValuePair<int, int>> e = d.GetEnumerator();
            Assert.AreEqual(P(0, 0), e.Current);
            Assert.AreEqual(true, e.MoveNext());
            Assert.AreEqual(P(1, 10), e.Current);
            e.Reset();
            Assert.AreEqual(P(0, 0), e.Current);
            Assert.AreEqual(true, e.MoveNext());
            Assert.AreEqual(true, e.MoveNext());
            Assert.AreEqual(P(2, 20), e.Current);
            Assert.AreEqual(false, e.MoveNext());
            e.Reset();
            Assert.AreEqual(P(0, 0), e.Current);
        }

        [Test]
        public void Clear()
        {
            var d = new DictionarySlim<int, int>();
            Assert.AreEqual(1, d.GetCapacity());
            d.GetOrAddValueRef(1) = 10;
            d.GetOrAddValueRef(2) = 20;
            Assert.AreEqual(2, d.Count);
            Assert.AreEqual(2, d.GetCapacity());
            d.Clear();
            Assert.AreEqual(0, d.Count);
            Assert.AreEqual(false, d.ContainsKey(1));
            Assert.AreEqual(false, d.ContainsKey(2));
            Assert.AreEqual(1, d.GetCapacity());
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

                rd.GetOrAddValueRef(k) += v;

                if (d.TryGetValue(k, out int t))
                    d[k] = t + v;
                else
                    d[k] = v;
            }

            Assert.AreEqual(d.Count, rd.Count);
            Assert.AreEqual(d.OrderBy(i => i.Key), (rd.OrderBy(i => i.Key)));
            Assert.AreEqual(d.OrderBy(i => i.Value), (rd.OrderBy(i => i.Value)));
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

                    rd.GetOrAddValueRef(k) += v;

                    if (d.TryGetValue(k, out int t))
                        d[k] = t + v;
                    else
                        d[k] = v;
                }

                if (rand.Next(3) == 0 && d.Count > 0)
                {
                    var el = GetRandomElement(d);
                    Assert.True(rd.Remove(el));
                    Assert.True(d.Remove(el));
                }
            }

            Assert.AreEqual(d.Count, rd.Count);
            Assert.AreEqual(d.OrderBy(i => i.Key), (rd.OrderBy(i => i.Key)));
            Assert.AreEqual(d.OrderBy(i => i.Value), (rd.OrderBy(i => i.Value)));
        }

        private TKey GetRandomElement<TKey, TValue>(IDictionary<TKey, TValue> d)
        {
            int index = 0;
            var rand = new Random(42);
            foreach(var entry in d)
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
            Assert.AreEqual(3, d.GetOrAddValueRef(C(5)));
            Assert.AreEqual(9, d.GetOrAddValueRef(C(7)));
            Assert.AreEqual(11, d.GetOrAddValueRef(C(10)));
            d.GetOrAddValueRef(C(23))++;
            d.GetOrAddValueRef(C(23)) += 3;
            Assert.AreEqual(4, d.GetOrAddValueRef(C(23)));
        }

        [Test]
        public void UsedIEquatable()
        {
            var d = new DictionarySlim<KeyUseTracking, int>();
            var key = new KeyUseTracking(5);
            d.GetOrAddValueRef(key)++;
            Assert.AreEqual(2, key.GetHashCodeCount);
            Assert.AreEqual(0, key.EqualsCount);
        }

        [Test]
        public void UsedIEquatable2()
        {
            var d = new DictionarySlim<KeyUseTracking, int>();
            var key = new KeyUseTracking(5);
            d.GetOrAddValueRef(key)++;
            d.GetOrAddValueRef(key)++;
            Assert.AreEqual(3, key.GetHashCodeCount);
            Assert.AreEqual(1, key.EqualsCount);
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
