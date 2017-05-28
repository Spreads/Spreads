// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace Spreads.Core.Tests
{
    public static class SimdMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SIMDAdd(float[] array, float scalar, float[] result, int count)
        {
            var vectorSize = Vector<float>.Count;
            var scalarVector = new Vector<float>(scalar);
            int i;

            for (i = 0; i + vectorSize < count; i = i + vectorSize)
            {
                var vector = new Vector<float>(array, i);
                var resultVector = vector * scalarVector;
                resultVector.CopyTo(result, i);
            }
            if (count > vectorSize)
            {
                i = i - vectorSize + 1;
            }
            for (int j = i; j < count; j++)
            {
                result[j] = array[j] + scalar;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void LoopAdd(float[] array, float scalar, float[] result, int count)
        {
            fixed (float* src = &array[0])
            fixed (float* tgt = &result[0])
            {
                for (var i = 0; i < count; i++)
                {
                    *(float*)(tgt + i) = ((*(float*)(src + i)) + scalar);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LoopSafeAdd(float[] array, float scalar, float[] result, int count)
        {
            for (var i = 0; i < count; i++)
            {
                result[i] = array[i] + scalar;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LoopSafeExp(double[] array, double[] result, int count)
        {
            for (var i = 0; i < count; i++)
            {
                result[i] = System.Math.Exp(array[i]);
            }
        }
    }
}