// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Core.Tests.Collections;
using Spreads.Utils;
using System;
using System.Diagnostics;
using Spreads.Core.Tests.Cursors;
using Spreads.Core.Tests.Cursors.Internal;
using Spreads.Core.Tests.Cursors.Online;
using Spreads.Core.Tests.DataTypes;

namespace Spreads.Core.Tests
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            using (Process p = Process.GetCurrentProcess())
            {
                p.PriorityClass = ProcessPriorityClass.High;
            }

            //Benchmark.ForceSilence = true;

            for (int i = 0; i < 20; i++)
            {
                //new ArithmeticTests().CouldUseStructSeries();
                //new ZipCursorTests().CouldAddTwoSeriesWithSameKeysBenchmark();
                //new SCMTests().EnumerateScmSpeed();
                //new VariantTests().CouldCreateAndReadInlinedVariantInALoop();
                new StatTests().Stat2StDevBenchmark();
            }

            Console.WriteLine("Press enter to exit...");
            Console.ReadLine();
        }
    }
}