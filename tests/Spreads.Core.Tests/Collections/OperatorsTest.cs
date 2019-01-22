// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using System;
using System.Runtime.CompilerServices;

namespace Spreads.Core.Tests.Collections
{
    public interface IImpl
    {
    }

    public interface ICol<T, TImpl> where TImpl : IImpl
    {
        TImpl Impl { get; }

        T Test();
    }

    public struct Impl1<T> : IImpl
    {
    }

    public struct Impl2<T> : IImpl
    {
    }

    public struct Col<T, TImpl> : ICol<T, TImpl> where TImpl : IImpl
    {
        public TImpl Impl => throw new NotImplementedException();

        public T Test() => default;

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static int operator
        //    +(Col<T, TImpl> left, Col<T, TImpl> right)
        //{
        //    return 1;
        //}
    }

    public struct ColL<TL, TR, IML, IMR> where IML : IImpl where IMR : IImpl
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ColL<TL, TR, IML, IMR>(Col<TL, IML> col)
        {
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int op_Addition(ColL<TL, TR, IML, IMR> col, ColR<TL, TR, IML, IMR> right)
        {
            return 1;
        }
    }

    public struct ColR<TL, TR, IML, IMR> where IML : IImpl where IMR : IImpl
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ColR<TL, TR, IML, IMR>(Col<TR, IMR> col)
        {
            return default;
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static int operator
        //    +(ColL<TL, TR, IML, IMR> left, ColR<TL, TR, IML, IMR> right)
        //{
        //    return 1;
        //}

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static int op_Addition(ColL<TL, TR, IML, IMR> left, ColR<TL, TR, IML, IMR> right)
        //{
        //    return 1;
        //}
    }

    [TestFixture]
    public unsafe class OperatorsTests
    {
        [Test]
        public void CouldAdd()
        {
            var c1 = default(Col<int, Impl1<int>>);
            var c2 = default(Col<long, Impl1<long>>);
            // var x = (ColL<int, long, Impl1<int>, Impl1<long>>)c1 + c2;
            // Assert.AreEqual(1, x);
        }
    }
}
