using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using System.Runtime.InteropServices;
using Spreads.Collections;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json;

namespace Spreads.Extensions.Tests {
    [TestFixture]
    public class CursorSeriesExtensionsTests {

       
        [Test]
        public void CouldCalculateIncompleteMovingAverage()
        {
            var sm = new SortedMap<int, double>();
            for (int i = 0; i < 20; i++)
            {
                sm.Add(i, i);
            }

            var sma = sm.SMA(2, true).ToSortedMap();

            var c = 0;
            foreach (var kvp in sma)
            {
                if (c == 0)
                {
                    Assert.AreEqual(c, kvp.Value);
                }
                else
                {
                    Assert.AreEqual(0.5*(c + (double)(c - 1)), kvp.Value);
                }

                c++;
            }

        }
        
    }

}
