using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Spreads.Storage;

namespace Spreads.Extensions.Tests {
    [TestFixture]
    public class SeriesRepositoryTests {

        [Test]
        public void CouldCreateRepositoryAndGetSeries() {

            using (var repo = new SeriesRepository("../CouldGetPersistentSeries")) {
                var ps = repo.WriteSeries<DateTime, double>("test_CouldGetPersistentSeries").Result;
                Assert.AreEqual(ps.Count, ps.Version);
                var initialVersion = ps.Version;
                ps.Add(DateTime.Now, 123.45);
                Console.WriteLine($"Count: {ps.Count}, version: {ps.Version}");
                Assert.AreEqual(initialVersion + 1, ps.Version);
                Assert.AreEqual(ps.Count, ps.Version);
            }

        }

        [Test]
        public void CouldCreateRepositoryAndGetSeriesInALoop() {
            for (int i = 0; i < 100; i++) {
                CouldCreateRepositoryAndGetSeries();
            }
        }

    }
}
