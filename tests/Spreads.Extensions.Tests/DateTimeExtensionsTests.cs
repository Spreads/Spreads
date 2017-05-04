// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Linq;
using NUnit.Framework;
using Spreads.Collections;
using Spreads.DataTypes;

namespace Spreads.Tests
{

    [TestFixture]
    public class DateTimeExtensionsTests
    {

        [Test]
        public void CouldGetZoneOffsets()
        {

            var utcOffsets = DateTimeExtensions.GetOffsetsFromUtc("ru");
            foreach (var offset in utcOffsets)
            {
                Console.WriteLine($"{offset.Key} - {offset.Value}");
            }

            Console.WriteLine("---------------------------------------");

            var localOffsets = DateTimeExtensions.GetOffsetsFromZoned("ru");
            foreach (var offset in localOffsets)
            {
                Console.WriteLine($"{offset.Key} - {offset.Value}");
            }

            Assert.True(utcOffsets.Values.SequenceEqual(localOffsets.Values));
            Assert.False(utcOffsets.Keys.SequenceEqual(localOffsets.Keys));
        }


    }
}
