using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Spreads.Storage;
using Spreads.Storage.Aeron;
using Spreads.Storage.Aeron.Logbuffer;
using Spreads.Storage.Aeron.Protocol;

namespace Spreads.Extensions.Tests {
    [TestFixture]
    public class AeronLogBufferTests {
        [Test]
        public unsafe void CouldCreateLogBuffers() {
            var sw = new Stopwatch();

            var l1 = new LogBuffers("../AeronLogBufferTests");
            Assert.IsTrue(l1.TermLength >= LogBufferDescriptor.TERM_MIN_LENGTH);
            Console.WriteLine($"TermLength: {l1.TermLength}");

            var activePartitionIndex = LogBufferDescriptor.ActivePartitionIndex(l1.LogMetaData);
            Console.WriteLine($"Active partition: {activePartitionIndex}");

            var activePartition = l1.Partitions[activePartitionIndex];
            var rawTail = activePartition.RawTailVolatile;
            Console.WriteLine($"Raw tail: {rawTail}");
            
            var ta = new TermAppender(activePartition);
            var defaultHeader = DataHeaderFlyweight.CreateDefaultHeader(0, 0, activePartition.TermId);
            var headerWriter = new HeaderWriter(defaultHeader);

            BufferClaim claim;
            ta.Claim(headerWriter, 100, out claim);
            claim.Commit();

        }


    }
}
