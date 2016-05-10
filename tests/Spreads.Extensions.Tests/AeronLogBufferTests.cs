using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Spreads.Storage;
using Spreads.Storage.Aeron;
using Spreads.Storage.Aeron.Logbuffer;

namespace Spreads.Extensions.Tests {
    [TestFixture]
    public class AeronLogBufferTests {
        [Test]
        public unsafe void CouldCreateLogBuffers() {
            var sw = new Stopwatch();

            var l1 = new LogBuffers("../AeronLogBufferTests");
            Assert.IsTrue(l1.TermLength >= LogBufferDescriptor.TERM_MIN_LENGTH);
            Console.WriteLine($"TermLength: {l1.TermLength}");

            var ta = new TermAppender()
        }


    }
}
