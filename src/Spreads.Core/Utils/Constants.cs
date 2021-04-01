// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Spreads.Utils
{
    public static class Constants
    {
        internal const MethodImplOptions MethodImplAggressiveAll = MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                                                                   | MethodImplOptions.AggressiveOptimization
#endif
            ;

    }
}
