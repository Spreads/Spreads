using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads.Persistence {

    public interface ISeriesSelector<T> {
        string Namespace { get; }
        string ToSeriesId(T selector);
        T FromSeriesId(string seriesId);

        IEnumerable<T> Search(string term, int count = 0);
        IEnumerable<T> Search(T term, int count = 0);
    }
}
