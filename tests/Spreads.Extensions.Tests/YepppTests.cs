using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using System.Runtime.InteropServices;
using Spreads.Collections;

namespace Spreads.Extensions.Tests {


    [TestFixture]
    public class YepppTests {

        [Test]
        public void RunYeppTest() {
            Bootstrap.YepppTest.Run();
        }

    }

}
