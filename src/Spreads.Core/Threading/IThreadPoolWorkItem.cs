// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace Spreads.Threading
{
#if !NETCOREAPP3_0

    /// <summary>Represents a work item that can be executed by the ThreadPool.</summary>
    public interface IThreadPoolWorkItem
    {
        void Execute();
    }

#endif
}