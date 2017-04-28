// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;
using Spreads.Core.Tests.Algorithms;
using Spreads.Core.Tests.Enumerators;
using Spreads.Core.Tests.Collections;
using Spreads.Core.Tests.Cursors;

namespace Spreads.Core.Tests
{
    
    internal class Program
    {
        private static void Main(string[] args)
        {
            for (int i = 0; i < 10; i++)
            {
                (new KeyComparerTests()).ComparerInterfaceAndCachedConstrainedComparer();
            }

            Console.ReadLine();
        }
    }
}
