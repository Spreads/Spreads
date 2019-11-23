// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using ObjectLayoutInspector;
using System;

namespace Spreads.Tests.Series
{
    [Category("CI")]
    [TestFixture]
    public unsafe class SeriesTests
    {
        [Test, Explicit("output")]
        public void SeriesObjectSize()
        {
            TypeLayout.PrintLayout<Series<DateTime, double>>();
        }
    }
}
