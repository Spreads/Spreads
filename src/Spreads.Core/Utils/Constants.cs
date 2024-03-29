﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Spreads.Utils
{
    internal static class Constants
    {
        internal const MethodImplOptions MethodImplAggressiveAll = MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                                                                   | MethodImplOptions.AggressiveOptimization
#endif
            ;

        internal const MethodImplOptions MethodImplAggressiveOptimization = 0
#if HAS_AGGR_OPT
                                                                   | MethodImplOptions.AggressiveOptimization
#endif
            ;

    }
}
