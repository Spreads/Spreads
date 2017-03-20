// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Runtime.InteropServices;

namespace Spreads.Yeppp
{
    /// <summary>Vector mathematical functions</summary>
    public partial class Math
    {
        /// <summary>Computes natural logarithm on double precision (64-bit) floating-point elements.</summary>
        /// <param name="xArray">Input array.</param>
        /// <param name="xOffset">Offset of the first element in xArray.</param>
        /// <param name="yArray">Output array.</param>
        /// <param name="yOffset">Offset of the first element in yArray.</param>
        /// <param name="length">The length of the subarrays to be used in computation.</param>
        /// <exception cref="System.NullReferenceException">If xArray or yArray is null.</exception>
        /// <exception cref="System.DataMisalignedException">If xArray or yArray is not naturally aligned.</exception>
        /// <exception cref="System.ArgumentException">If length is negative.</exception>
        /// <exception cref="System.IndexOutOfRangeException">If xOffset is negative, xOffset + length exceeds the length of xArray, yOffset is negative, yOffset + length exceeds the length of yArray, or length is negative.</exception>
        public static unsafe void Log_V64f_V64f(double[] xArray, int xOffset, double[] yArray, int yOffset, int length)
        {
            if (xOffset < 0)
                throw new System.IndexOutOfRangeException();

            if (xOffset + length > xArray.Length)
                throw new System.IndexOutOfRangeException();

            if (yOffset < 0)
                throw new System.IndexOutOfRangeException();

            if (yOffset + length > yArray.Length)
                throw new System.IndexOutOfRangeException();

            if (length < 0)
                throw new System.ArgumentException();

            fixed (double* x = &xArray[xOffset])
            {
                fixed (double* y = &yArray[yOffset])
                {
                    Log_V64f_V64f(x, y, length);
                }
            }
        }

        /// <summary>Computes natural logarithm on an array of double precision (64-bit) floating-point elements.</summary>
        /// <param name="x">Pointer to the array of elements on which logarithm will be computed.</param>
        /// <param name="y">Pointer the array where the computed logarithms will be stored.</param>
        /// <param name="length">Length of the arrays specified by x and y.</param>
        /// <exception cref="System.NullReferenceException">If x or y is null.</exception>
        /// <exception cref="System.DataMisalignedException">If x or y is not naturally aligned.</exception>
        /// <exception cref="System.ArgumentException">If length is negative.</exception>
        public static unsafe void Log_V64f_V64f(double* x, double* y, int length)
        {
            if (length < 0)
                throw new System.ArgumentException();

            Status status = yepMath_Log_V64f_V64f(x, y, new System.UIntPtr(unchecked((uint)length)));
            if (status != Status.Ok)
                throw Library.GetException(status);
        }

        [DllImport("yeppp", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "yepMath_Log_V64f_V64f")]
        internal static unsafe extern Status yepMath_Log_V64f_V64f(double* x, double* y, System.UIntPtr length);
    }
}