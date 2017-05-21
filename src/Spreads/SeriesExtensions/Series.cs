using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spreads.DataTypes;

namespace Spreads
{
    public static partial class Series
    {

        // Read
        // Write
        // Save(append option)

        public class SeriesReader
        {
            internal static SeriesReader Instance = new SeriesReader();
            private SeriesReader()
            {
            }

            public BaseSeries<Variant, Variant> this[string expression] => Read(expression);
            public BaseSeries<Variant, Variant> this[string expression, AppendOption option]
            {
                set { throw new NotImplementedException(); }
            }

            public BaseSeries<Variant, Variant> Read(string expression)
            {
                throw new NotImplementedException();
            }
        }

        public static SeriesReader R => SeriesReader.Instance;
        public static SeriesReader Spreads => SeriesReader.Instance;

        public static BaseSeries<Variant, Variant> Read(string expression)
        {
            return R.Read(expression);
        }


        public static IMutableSeries<Variant, Variant> Write(string seriesId)
        {
            throw new NotImplementedException();
        }
    }
}
