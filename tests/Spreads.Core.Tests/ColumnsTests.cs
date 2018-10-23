// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Spreads.DataTypes;
using Spreads.Utils.FastMember;

namespace Spreads.Core.Tests
{
    public class TestRow
    {
        public int Col1 { get; set; }
        public double Col2 { get; set; }
        public decimal Col3 { get; set; }
        public int Col11 { get; set; }
        public double Col21 { get; set; }
        public decimal Col31 { get; set; }
    }

    [TestFixture]
    public class ColumnsTests
    {
        [Test, Explicit("long running")]
        public void ColumnTest()
        {
            var count = 100000;
            var accessor = TypeAccessor.Create(typeof(TestRow));
            var rows = new TestRow[count];
            for (int i = 0; i < count; i++)
            {
                rows[i] = new TestRow
                {
                    Col1 = i,
                    Col2 = i,
                    Col3 = i,
                    Col11 = i,
                    Col21 = i,
                    Col31 = i
                };
            }

            var memberSet = accessor.GetMembers();
            Array[] arrays = new Array[memberSet.Count];
            Variant[] variants = new Variant[memberSet.Count];

            var memberCount = memberSet.Count;
            for (int i = 0; i < memberCount; i++)
            {
                arrays[i] = Array.CreateInstance(memberSet[i].Type, count);
                var arr = arrays[i];
                variants[i] = Variant.Create(arr);
            }

            var names = memberSet.Select(m => m.Name).ToArray();

            for (int round = 0; round < 100; round++)
            {
                var sw = new Stopwatch();
                sw.Restart();

                for (var r = 0; r < count; r++)
                {
                    for (var c = 0; c < memberCount; c++)
                    {
                        var value = accessor[rows[r], names[c]];
                        //arrays[c].SetValue(value, r);
                        variants[c][r] = Variant.Create(value); // almost 4x slower
                    }
                }

                sw.Stop();

                Console.WriteLine($"To columns: {sw.ElapsedMilliseconds} msec");
            }
        }
    }
}