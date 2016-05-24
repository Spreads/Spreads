using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using System.Runtime.InteropServices;
using System.Threading;
using Spreads.Serialization;


namespace Spreads.Core.Tests {

    

    [TestFixture]
    public class TypeHelperTests {
        
        [Test]
        public void CouldGetSizeOfDoubleArray()
        {
            Console.WriteLine(TypeHelper<double[]>.Size);

        }

        [Test]
        public void CouldGetSizeOfReferenceType() {

            Console.WriteLine(TypeHelper<string>.Size);

        }
    }
}
