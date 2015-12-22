using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Spreads.Collections;

namespace Spreads.Collections.Tests
{

    [TestFixture]
    public class IndexedMapTests
    {

        [SetUp]
        public void Init()
        {
            
        }

        [Test]
        public void ObeysContracts()
        {
            // TODO! implement specific ones not covered buy the contract suite
            Trace.TraceWarning("IndexedMap contract test is not implemented");
        }

        [Test]
        public void CouldAddToIndexedMap()
        {
            var im = new IndexedMap<string, object>();

            im.Add("first", "obj1");
            im.Add("second", "obj2");
            im.Add("third", "obj3");
            im.Add("forths", "obj4");
            for (int i = 0; i < 10000; i++)
            {
                im.Add(i.ToString(), i);
            }
        }
    }
}
