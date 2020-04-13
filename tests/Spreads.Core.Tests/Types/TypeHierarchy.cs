using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Spreads.Collections;

namespace Spreads.Core.Tests.Types
{

    public struct Index
    {
        private long Idx;

        public Index(long idx)
        {
            Idx = idx;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Index(System.Index index)
        {
            return new Index(index.Value);
        }
        
        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // public static implicit operator Index(int index)
        // {
        //     return new Index(index);
        // }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Index(long index)
        {
            return new Index(index);
        }
    }
    
    public struct Any
    {
        private long Idx;

        public Any(long idx)
        {
            Idx = idx;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Any(int value)
        {
            return default;
        }
       
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Any(string value)
        {
            return default;
        }
    }
    
    class Series<K,V>
    {
        // public V this[int index]
        // {
        //     get { return default; }
        // }
        
        public V this[Index index]
        {
            get { return default; }
        }

        public V this[K index]
        {
            get { return default; }
        }

        public bool TryGetValue(System.Index index, out V value)
        {
            value = default;
            return true;
        }
        
        public bool TryGetValue(K index, out V value)
        {
            value = default;
            return true;
        }
    }

    class Vector<V> : Series<Index, V>
    {
        
    }
    
    [TestFixture]
    public class TypeHierarchy
    {
        [Test]
        public void CallResolution()
        {
            TestAnyCall(123, "456");
            TestAnyCall(123, "456");
            
            var vec = new Vector<int>();
            var v = vec[^1];
            var found = vec.TryGetValue(1, out var tgv);
            
            var ser = new Series<int,int>();
            var vs = ser[1];
            found = ser.TryGetValue(1, out tgv);
        }

        public void TestAnyCall(params Any[] values )
        {
            Console.WriteLine("Called any");
        }
        
        public void TestAnyCall<T>(params T[] values )
        {
            Console.WriteLine("Called T");
        }
    }
}