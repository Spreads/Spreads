// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace Spreads.Core.Tests
{
    [TestFixture]
    public class YepppTests
    {
        [Test]
        public void RunYeppTest()
        {
            YepppTest.Run();
        }
    }

    internal class YepppTest
    {
        public static void Run()
        {
            /* Size of the array of elements to compute the polynomial on */
            const int arraySize = 500; // * 1024 * 8;
            const int rounds = 100000; // * 1024 * 8;
            /* Allocate arrays of inputs and outputs */
            double[] x = new double[arraySize];
            double[] y = new double[arraySize];
            double[] pYeppp = new double[arraySize];
            double[] pNaive = new double[arraySize];
            /* Populate the array of inputs with random data */
            Random rng = new Random();
            for (int i = 0; i < x.Length; i++)
            {
                x[i] = rng.NextDouble();
                y[i] = x[i];
            }
            /* Zero-initialize the output arrays */
            Array.Clear(pYeppp, 0, pYeppp.Length);
            Array.Clear(pNaive, 0, pYeppp.Length);
            /* Retrieve the number of timer ticks per second */
            ulong frequency = Yeppp.Library.GetTimerFrequency();
            /* Retrieve the number of timer ticks before calling the C version of polynomial evaluation */
            ulong startTimeNaive = Yeppp.Library.GetTimerTicks();
            /* Evaluate polynomial using C# implementation */
            for (int i = 0; i < rounds; i++)
            {
                if (!EvaluatePolynomialNaive(x, y)) throw new ApplicationException();
                //EvaluatePolynomialNaive(x, pNaive);
            }
            /* Retrieve the number of timer ticks after calling the C version of polynomial evaluation */
            ulong endTimeNaive = Yeppp.Library.GetTimerTicks();
            /* Retrieve the number of timer ticks before calling Yeppp! polynomial evaluation */
            ulong startTimeYeppp = Yeppp.Library.GetTimerTicks();
            /* Evaluate polynomial using Yeppp! */
            for (int i = 0; i < rounds; i++)
            {
                //Yeppp.Core.Subtract_V64fV64f_V64f(x, 0, y, 0, pYeppp, 0, x.Length);
                //if(0.0 != Yeppp.Core.SumSquares_V64f_S64f(pYeppp, 0, x.Length)) throw new ApplicationException();
                Yeppp.Core.Multiply_V64fS64f_V64f(x, 0, 3.1415, y, 0, x.Length);
                ////Yeppp.Core.Multiply_IV64fS64f_IV64f(x, 0, 3.1415, x.Length);
                //Yeppp.Math.Exp_V64f_V64f(y, 0, pYeppp, 0, x.Length);
                Yeppp.Math.Log_V64f_V64f(y, 0, pYeppp, 0, x.Length);
                //Yeppp.Math.EvaluatePolynomial_V64fV64f_V64f(coefs, 0, x, 0, pYeppp, 0, coefs.Length, x.Length);
            }
            /* Retrieve the number of timer ticks after calling Yeppp! polynomial evaluation */
            ulong endTimeYeppp = Yeppp.Library.GetTimerTicks();
            /* Compute time in seconds and performance in FLOPS */
            double secsNaive = ((double)(endTimeNaive - startTimeNaive)) / ((double)(frequency));
            double secsYeppp = ((double)(endTimeYeppp - startTimeYeppp)) / ((double)(frequency));
            double flopsNaive = (double)(arraySize * rounds * (2) * 2) / secsNaive;
            double flopsYeppp = (double)(arraySize * rounds * (2) * 2) / secsYeppp;
            /* Report the timing and performance results */
            Console.WriteLine("Naive implementation:");
            Console.WriteLine("\tTime = {0:F2} secs", secsNaive);
            Console.WriteLine("\tPerformance = {0:F2} GFLOPS", flopsNaive * 1.0e-9);
            Console.WriteLine("Yeppp! implementation:");
            Console.WriteLine("\tTime = {0:F2} secs", secsYeppp);
            Console.WriteLine("\tPerformance = {0:F2} GFLOPS", flopsYeppp * 1.0e-9);
            /* Make sure the result is correct. */
            Console.WriteLine("Max difference: {0:F3}%", ComputeMaxDifference(pNaive, pYeppp) * 100.0f);

            Console.ReadLine();
        }

        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int memcmp(double[] b1, double[] b2, long count);

        private static bool ArrayCompare(double[] b1, double[] b2)
        {
            // Validate buffers are the same length.
            // This also ensures that the count does not exceed the length of either buffer.
            return b1.Length == b2.Length && memcmp(b1, b2, b1.Length * 8) == 0;
        }

        private static unsafe bool UnsafeCompare(double[] a1, double[] a2)
        {
            if (a1 == null || a2 == null || a1.Length != a2.Length)
                return false;
            fixed (double* p1 = a1, p2 = a2)
            {
                double* x1 = p1, x2 = p2;
                int l = a1.Length;
                for (int i = 0; i < l; i++, x1 += 1, x2 += 1)
                    if (*(x1) != *(x2)) return false;
                return true;
            }
        }

        /* C# implementation with hard-coded coefficients. */

        private static bool EvaluatePolynomialNaive(double[] xArray, double[] yArray)
        {
            //Debug.Assert(xArray.Length == yArray.Length);

            //return UnsafeCompare(xArray, yArray);
            //return ArrayCompare(xArray, yArray);

            //return xArray.SequenceEqual(yArray);
            //double[] z = new double[xArray.Length];
            for (int i = 0; i < xArray.Length; i++)
            {
                yArray[i] = Math.Log(xArray[i] * 3.1415); //Math.Log(xArray[i]*2);
            }
            return true;
        }

        /* This function computes the maximum relative error between two vectors. */

        private static double ComputeMaxDifference(double[] xArray, double[] yArray)
        {
            Debug.Assert(xArray.Length == yArray.Length);
            double maxDiff = 0.0;
            for (int index = 0; index < xArray.Length; index++)
            {
                if (xArray[index] == 0.0)
                    continue;
                double diff = Math.Abs(xArray[index] - yArray[index]) / Math.Abs(xArray[index]);
                maxDiff = Math.Max(maxDiff, diff);
            }
            return maxDiff;
        }
    }
}