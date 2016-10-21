// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;

namespace Spreads.Slices
{
    static class Contract
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Requires(bool condition)
        {
            if (!condition)
            {
                throw NewArgumentException();
            }
        }

        public static void RequiresNonNegative(int n)
        {
            if (n < 0)
            {
                throw NewArgumentOutOfRangeException();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RequiresInRange(long start, uint length)
        {
            if ((ulong)start >= length)
            {
                throw NewArgumentOutOfRangeException();
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RequiresInRange(ulong start, ulong length)
        {
            if (start >= length)
            {
                throw NewArgumentOutOfRangeException();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RequiresInInclusiveRange(int start, uint length)
        {
            if ((uint)start > length)
            {
                throw NewArgumentOutOfRangeException();
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RequiresInInclusiveRange(uint start, uint length)
        {
            if (start > length)
            {
                throw NewArgumentOutOfRangeException();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RequiresInInclusiveRange(int start, int length, uint existingLength)
        {
            if ((uint)start > existingLength
                || length < 0
                || (uint)(start + length) > existingLength)
            {
                throw NewArgumentOutOfRangeException();
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RequiresInInclusiveRange(uint start, uint length, uint existingLength)
        {
            if (start > existingLength
                || length < 0
                || (start + length) > existingLength)
            {
                throw NewArgumentOutOfRangeException();
            }
        }

        
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Exception NewArgumentException()
        {
            return new ArgumentException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Exception NewArgumentOutOfRangeException()
        {
            return new ArgumentOutOfRangeException();
        }
    }
}

