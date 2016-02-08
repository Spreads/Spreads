using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spreads;
using Spreads.Collections;


namespace Spreads.Generation {
    public static class SeriesGenerator {

        public static Series<DateTime, double> GenerateDummyTimeSeries(int length,
            DateTime? startDate = null, TimeSpan? dateStep = null,
            double startValue = 0.0, double valueStep = 1.0) {
            var sm = new SortedMap<DateTime, double>(length);

            if (startDate == null) {
                startDate = DateTime.Today.ToUniversalTime();
            }

            if (dateStep == null) {
                dateStep = TimeSpan.FromSeconds(1);
            }

            for (int i = 0; i < length; i++) {
                sm.AddLast(startDate.Value, startValue);
                startDate += dateStep;
                startValue += valueStep;
            }

            return sm;
        }

        public static Series<DateTime, double>[] DummySeries(int length, int width,
            DateTime? startDate = null, TimeSpan? dateStep = null,
            double startValue = 0.0, double valueStep = 1.0, double columnMultiple = 10.0) {

            var series = new Series<DateTime, double>[width];

            for (var i = 0; i < width; i++) {
                series[i] = new SortedMap<DateTime, double>(length);
            }

            if (startDate == null) {
                startDate = DateTime.Today.ToUniversalTime();
            }

            if (dateStep == null) {
                dateStep = TimeSpan.FromSeconds(1);
            }

            for (var i = 0; i < length; i++) {
                for (var c = 0; c < width; c++) {
                    var sm = series[c] as SortedMap<DateTime, double>;
                    sm.AddLast(startDate.Value, startValue);
                    startDate += dateStep;
                    startValue += valueStep;
                }
            }

            return series;
        }

    }
}
