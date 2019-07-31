// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Tests {

    [TestFixture]
    public class BuffersAndPoolTests {

        internal struct Unpinner {
            private readonly UnpinWhenGCedTest _unpinWhenGCedTest;

            public Unpinner(UnpinWhenGCedTest UnpinWhenGCedTest) {
                _unpinWhenGCedTest = UnpinWhenGCedTest;
            }
        }

        internal class UnpinWhenGCedTest {
            private readonly GCHandle _pinnedGCHandle;
            internal static bool Unpinned = false;

            public UnpinWhenGCedTest(GCHandle pinnedGCHandle) {
                _pinnedGCHandle = pinnedGCHandle;
            }

            ~UnpinWhenGCedTest() {
                _pinnedGCHandle.Free();
                Console.WriteLine("Unpinned");
                Unpinned = true;
            }
        }

        private ConditionalWeakTable<byte[], UnpinWhenGCedTest> _cwt = new ConditionalWeakTable<byte[], UnpinWhenGCedTest>();

        [Test]
        public void PinnedBufferIsNotColelctedWhenReferencedFromCWT() {
            UnpinWhenGCedTest.Unpinned = false;
            var buffer = new byte[] { 1, 2, 3 };
            Pin(buffer);
            buffer = null;

            GC.Collect(3, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(3, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();

            // we cannot automatically unpin
            Assert.IsFalse(UnpinWhenGCedTest.Unpinned);
        }

        private void Pin(byte[] buffer) {
            _cwt.Add(buffer, new UnpinWhenGCedTest(GCHandle.Alloc(buffer,
                // NB doesn't work for pinned and normal
                GCHandleType.Pinned)));
        }

        [Test]
        public void PinnedBufferIsUnpinnedWhenUnpinnerIsCollected() {
            UnpinWhenGCedTest.Unpinned = false;
            var buffer = new byte[] { 1, 2, 3 };
            var unpinner = new Unpinner(new UnpinWhenGCedTest(GCHandle.Alloc(buffer, GCHandleType.Pinned)));

            GC.Collect(3, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(3, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();

            Assert.IsTrue(UnpinWhenGCedTest.Unpinned);
        }

        [Test]
        public void PinnedBufferIsNotUnpinnedWhenUnpinnerIsNotCollected() {
            UnpinWhenGCedTest.Unpinned = false;
            var buffer = new byte[] { 1, 2, 3 };
            var unpinner = new Unpinner(new UnpinWhenGCedTest(GCHandle.Alloc(buffer, GCHandleType.Pinned)));
            var copy = unpinner;
            GC.Collect(3, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(3, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();

            Assert.IsFalse(UnpinWhenGCedTest.Unpinned);
            //unpinner is still in scope
            Console.WriteLine(copy.ToString());
        }
    }
}