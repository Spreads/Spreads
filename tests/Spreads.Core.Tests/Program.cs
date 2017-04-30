// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Core.Tests.Collections;
using Spreads.Utils;
using System;

namespace Spreads.Core.Tests
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Benchmark.ForceSilence = true;

            for (int i = 0; i < 5; i++)
            {
                (new FastDictionaryTests()).CompareSCGAndFastDictionaryWithInts();
            }

            Console.WriteLine("Press enter to exit...");
            Console.ReadLine();
        }
    }
}