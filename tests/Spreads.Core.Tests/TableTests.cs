// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using NUnit.Framework;
using Spreads.DataTypes;
using Spreads.Serialization;

namespace Spreads.Tests
{
    [TestFixture]
    public class TableTests
    {
        [Test, Explicit("long running")]
        public void CouldSerializeTable()
        {
            var sw = new Stopwatch();
            sw.Restart();
            var rows = 100;
            var columns = 100;
            var data = new Variant[rows, columns];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < columns; j++)
                {
                    data[i, j] = Variant.Create(i * j);
                }
            }

            var table = new Table(data);

            sw.Stop();
            Console.WriteLine($"Elapsed create: {sw.ElapsedMilliseconds}");
            sw.Restart();

            sw.Stop();
            Console.WriteLine($"Elapsed snapshot: {sw.ElapsedMilliseconds}");
            sw.Restart();

            ArraySegment<byte> tmp;
            var len = BinarySerializer.SizeOf(table, out tmp);
            Console.WriteLine($"Binary size: {len}");
            //var mem = BufferPool<byte>.Rent(len);
            //BinarySerializer.Write(table, mem, 0, tmp);

            sw.Stop();
            Console.WriteLine($"Elapsed serialize: {sw.ElapsedMilliseconds}");
            sw.Restart();

            //var str = JsonConvert.SerializeObject(table);
            //Console.WriteLine(str.Length);
            //Console.WriteLine(str);

            var delta = table.ToDelta(table);
            sw.Stop();
            Console.WriteLine($"Elapsed delta: {sw.ElapsedMilliseconds}");
            var str2 = JsonConvert.SerializeObject(delta);
            Console.WriteLine($"Delta length: {str2.Length} count: {delta.Cells.Count}");
        }

        [Test, Explicit("long running")]
        public void CouldSerializeTableManyTimes()
        {
            for (int round = 0; round < 10; round++)
            {
                CouldSerializeTable();
            }
        }
    }
}