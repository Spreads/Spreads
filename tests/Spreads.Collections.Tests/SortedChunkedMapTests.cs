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

namespace Spreads.Collections.Tests {

	[TestFixture]
	public class SortedChunkedMapTests {



		[SetUp]
		public void Init() {
		}

		
		[Test]
		public void CouldRemoveFirst()
		{
		    var scm = new SortedChunkedMap<int, int>(50);
		    for (int i = 0; i < 100; i++)
		    {
		        scm.Add(i, i);
		    }

		    scm.Remove(50);
            Assert.AreEqual(50, scm.outerMap.Last.Key);

            KeyValuePair<int, int> kvp;
		    scm.RemoveFirst(out kvp);
		    Assert.AreEqual(0, kvp.Value);
            Assert.AreEqual(1, scm.First.Value);
		    Assert.AreEqual(0, scm.outerMap.First.Key);
		}

        [Test]
        public void CouldSetInsteadOfAddWithCorrectChunks() {
            var scm = new SortedChunkedMap<int, int>(50);
            for (int i = 0; i < 100; i++) {
                scm[i] = i;
            }

            Assert.AreEqual(2, scm.outerMap.Count);

            
        }

    }
}
