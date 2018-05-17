// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Collections;
using Spreads.DataTypes;

namespace Spreads.Tests
{

    [TestFixture]
    public class VariantSeriesTest
    {


        [Test]
        public void CouldReadVariantSeries()
        {

            var sm = new SortedMap<int, string>();
            for (int i = 0; i < 100; i++)
            {
                sm.Add(i, (i * 100).ToString());
            }

            var vs = new VariantSeries<int, string>(sm);


            foreach (var item in vs)
            {
                System.Console.WriteLine(item.Key.Get<int>() + ": " + item.Value.Get<string>());
            }

            Assert.AreEqual(Variant.Create(0), vs.First.Key);
            Assert.AreEqual(Variant.Create("0"), vs.First.Value);
            Assert.AreEqual(Variant.Create(99), vs.Last.Key);
            Assert.AreEqual(Variant.Create("9900"), vs.Last.Value);


            var cursorSeries = new CursorSeries<int, string>(sm);
            Assert.AreEqual(0, cursorSeries.First.Key);
        }


        [Test]
        public void CouldAddVariantSeries()
        {

            var sm = new SortedMap<int, double>();
            for (int i = 0; i < 100; i++)
            {
                sm.Add(i, (i * 100));
            }

            var vs = new VariantSeries<int, double>(sm);

            var doubled = vs + vs;

            foreach (var item in doubled)
            {
                System.Console.WriteLine(item.Key.Get<int>() + ": " + item.Value.Get<double>());
            }

            Assert.AreEqual(Variant.Create(0), doubled.First.Key);
            Assert.AreEqual(Variant.Create(0.0), doubled.First.Value);
            Assert.AreEqual(Variant.Create(99), doubled.Last.Key);
            Assert.AreEqual(Variant.Create(9900.0 * 2), doubled.Last.Value);


            var cursorSeries = doubled.ReadOnly();
            Assert.AreEqual(Variant.Create(0), cursorSeries.First.Key);
        }


        //[Test]
        //public void CouldAddConvertSeries()
        //{

        //    var sm = new SortedMap<int, double>();
        //    for (int i = 0; i < 100; i++)
        //    {
        //        sm.Add(i, (i * 100));
        //    }

        //    var vs = new ConvertSeries<int, double>(sm);

        //    var doubled = vs + vs;

        //    foreach (var item in doubled)
        //    {
        //        System.Console.WriteLine(item.Key.Get<int>() + ": " + item.Value.Get<double>());
        //    }

        //    Assert.AreEqual(Variant.Create(0), vs.First.Key);
        //    Assert.AreEqual(Variant.Create("0"), vs.First.Value);
        //    Assert.AreEqual(Variant.Create(99), vs.Last.Key);
        //    Assert.AreEqual(Variant.Create("9900"), vs.Last.Value);


        //    var cursorSeries = doubled.ReadOnly();
        //    Assert.AreEqual(0, cursorSeries.First.Key);
        //}
    }
}
