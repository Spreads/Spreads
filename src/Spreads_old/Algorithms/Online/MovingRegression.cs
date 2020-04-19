// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// TODO ScanLagAllowIncompleteCursor is no longer there, find old one

//using System;
//using MathNet.Numerics.LinearAlgebra;
//using Spreads.Cursors;

//namespace Spreads.Algorithms.Online
//{
//    public static class MovingRegressionExtension
//    {
//        public static Series<DateTime, Matrix<double>, Cursor<DateTime, Matrix<double>>> MovingRegression(this Series<DateTime, double> y, Series<DateTime, double[]> xs, uint window, uint step = 1)
//        {
//            var xpx = xs.Zip(y, (dt, xrow, yrow) =>
//            {
//                // first x row transposed
//                var xt = Matrix<double>.Build.DenseOfColumnArrays(new[] { xrow });
//                var xpxi = xt.Multiply(xt.Transpose());
//                var xpy = xt.Multiply(yrow);
//                var tuple =
//                    new ValueTuple<Matrix<double>, Matrix<double>>(xpxi, xpy);
//                //return xpxi;
//                return tuple;
//            });

//            // then we need to sum xpxi and xpy over a window, make inverse of the first and multiply
//            // - the result is betas
//            var dim = xs.First.Present.Value.Length;
//            var cursorFactory = new Func<ScanLagAllowIncompleteCursor<DateTime, ValueTuple<Matrix<double>, Matrix<double>>, ValueTuple<Matrix<double>, Matrix<double>>>>(() =>
//                new ScanLagAllowIncompleteCursor<DateTime, ValueTuple<Matrix<double>, Matrix<double>>, ValueTuple<Matrix<double>, Matrix<double>>>(
//                    xpx.GetCursor, window, step,
//                    () => new ValueTuple<Matrix<double>, Matrix<double>>(Matrix<double>.Build.Dense(dim, dim), Matrix<double>.Build.Dense(dim, 1)),
//                    (st, add, sub, cnt) =>
//                    {
//                        var cov = st.Item1.Add(add.Value.Item1);
//                        if (sub.Value.Item1 != null)
//                        {
//                            cov = cov.Subtract(sub.Value.Item1);
//                        }
//                        var variance = st.Item2.Add(add.Value.Item2);
//                        if (sub.Value.Item2 != null)
//                        {
//                            variance = variance.Subtract(sub.Value.Item2);
//                        }
//                        return new ValueTuple<Matrix<double>, Matrix<double>>(cov, variance);
//                    }, false));

//            //Series<DateTime, Matrix<double>> betas;
//            var betas = new CursorSeries<DateTime, ValueTuple<Matrix<double>, Matrix<double>>>(cursorFactory).Map(
//                tpl =>
//                {
//                    return tpl.Item1.Inverse().Multiply(tpl.Item2);
//                });
//            return betas.Unspecialized;
//        }
//    }
//}
