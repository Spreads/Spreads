using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads {
    public class EmptyArray<TElement> {
        public static readonly TElement[] Instance = new TElement[0];
    }
}
