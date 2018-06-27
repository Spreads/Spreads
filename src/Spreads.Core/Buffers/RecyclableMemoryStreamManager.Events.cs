// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// ---------------------------------------------------------------------
// Copyright (c) 2015 Microsoft
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// ---------------------------------------------------------------------

using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace Spreads.Buffers
{
    public sealed partial class RecyclableMemoryStreamManager
    {
        [EventSource(Name = "Spreads-Buffers-RecyclableMemoryStream", Guid = "{AE48A701-2840-48A5-B40F-8093160CADD5}")]
        public sealed class Events : EventSource
        {
            public new static readonly Events Write = new Events();

            public enum MemoryStreamBufferType
            {
                Small,
                Large
            }

            public enum MemoryStreamDiscardReason
            {
                TooLarge,
                EnoughFree
            }

            [Event(1, Level = EventLevel.Verbose)]
            [Conditional("TRACE_RMS")]
            public void MemoryStreamCreated(long id, string tag, int requestedSize)
            {
                if (IsEnabled(EventLevel.Verbose, EventKeywords.None))
                {
                    WriteEvent(1, id, tag ?? string.Empty, requestedSize);
                }
            }

            [Event(2, Level = EventLevel.Verbose)]
            [Conditional("TRACE_RMS")]
            public void MemoryStreamDisposed(long id, string tag)
            {
                if (IsEnabled(EventLevel.Verbose, EventKeywords.None))
                {
                    WriteEvent(2, id, tag ?? string.Empty);
                }
            }

            [Event(3, Level = EventLevel.Critical)]
            [Conditional("TRACE_RMS")]
            public void MemoryStreamDoubleDispose(long id, string tag, string allocationStack, string disposeStack1,
                                                  string disposeStack2)
            {
                if (IsEnabled())
                {
                    WriteEvent(3, id, tag ?? string.Empty, allocationStack ?? string.Empty,
                                    disposeStack1 ?? string.Empty, disposeStack2 ?? string.Empty);
                }
            }

            [Event(4, Level = EventLevel.Error)]
            [Conditional("TRACE_RMS")]
            public void MemoryStreamFinalized(long id, string tag, string allocationStack)
            {
                if (IsEnabled())
                {
                    WriteEvent(4, id, tag ?? string.Empty, allocationStack ?? string.Empty);
                }
            }

            [Event(5, Level = EventLevel.Verbose)]
            [Conditional("TRACE_RMS")]
            public void MemoryStreamToArray(long id, string tag, string stack, int size)
            {
                if (IsEnabled(EventLevel.Verbose, EventKeywords.None))
                {
                    WriteEvent(5, id, tag ?? string.Empty, stack ?? string.Empty, size);
                }
            }

            [Event(6, Level = EventLevel.Informational)]
            [Conditional("TRACE_RMS")]
            public void MemoryStreamManagerInitialized(int blockSize, int largeBufferMultiple, int maximumBufferSize)
            {
                if (IsEnabled())
                {
                    WriteEvent(6, blockSize, largeBufferMultiple, maximumBufferSize);
                }
            }

            [Event(7, Level = EventLevel.Verbose)]
            [Conditional("TRACE_RMS")]
            public void MemoryStreamNewBlockCreated(long smallPoolInUseBytes)
            {
                if (IsEnabled(EventLevel.Verbose, EventKeywords.None))
                {
                    WriteEvent(7, smallPoolInUseBytes);
                }
            }

            [Event(8, Level = EventLevel.Verbose)]
            [Conditional("TRACE_RMS")]
            public void MemoryStreamNewLargeBufferCreated(int requiredSize, long largePoolInUseBytes)
            {
                if (IsEnabled(EventLevel.Verbose, EventKeywords.None))
                {
                    WriteEvent(8, requiredSize, largePoolInUseBytes);
                }
            }

            [Event(9, Level = EventLevel.Verbose)]
            [Conditional("TRACE_RMS")]
            public void MemoryStreamNonPooledLargeBufferCreated(int requiredSize, string tag, string allocationStack)
            {
                if (IsEnabled(EventLevel.Verbose, EventKeywords.None))
                {
                    WriteEvent(9, requiredSize, tag ?? string.Empty, allocationStack ?? string.Empty);
                }
            }

            [Event(10, Level = EventLevel.Warning)]
            [Conditional("TRACE_RMS")]
            public void MemoryStreamDiscardBuffer(MemoryStreamBufferType bufferType, string tag,
                                                  MemoryStreamDiscardReason reason)
            {
                if (IsEnabled())
                {
                    WriteEvent(10, bufferType, tag ?? string.Empty, reason);
                }
            }

            [Event(11, Level = EventLevel.Error)]
            [Conditional("TRACE_RMS")]
            public void MemoryStreamOverCapacity(int requestedCapacity, long maxCapacity, string tag,
                                                 string allocationStack)
            {
                if (IsEnabled())
                {
                    WriteEvent(11, requestedCapacity, maxCapacity, tag ?? string.Empty, allocationStack ?? string.Empty);
                }
            }
        }
    }
}