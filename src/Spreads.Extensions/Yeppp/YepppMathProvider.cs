using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.FSharp.Core;
using Spreads.Collections;


namespace Spreads.NativeMath {
    public class YepppMathProvider : IVectorMathProvider {
        static YepppMathProvider()
        {
            Yeppp.Library.Init();
        }

        public bool AddBatch<K>(IReadOnlyOrderedMap<K, double> left, IReadOnlyOrderedMap<K, double> right, out IReadOnlyOrderedMap<K, double> value) {
            throw new NotImplementedException();
        }

        public bool AddBatch<K>(double scalar, IReadOnlyOrderedMap<K, double> batch, out IReadOnlyOrderedMap<K, double> value)
        {
            var sm = batch as SortedMap<K, double>;
            if (!ReferenceEquals(sm, null))
            {
                double[] newValues = new double[sm.size];
                Yeppp.Core.Add_V64fS64f_V64f(sm.values, 0, scalar, newValues, 0, sm.size);
                //Yeppp.Math.Log_V64f_V64f(sm.values, 0, newValues, 0, sm.size);
                var newKeys = sm.IsMutable ? sm.keys.ToArray() : sm.keys;
                var newSm = SortedMap<K, double>.OfSortedKeysAndValues(newKeys, newValues, sm.size, sm.Comparer, false, sm.IsRegular);
                value = newSm;
                return true;
            }
            throw new NotImplementedException();
        }

        public bool SumBatch<K>(double scalar, IReadOnlyOrderedMap<K, double> batch, out double value) {
            var sm = batch as SortedMap<K, double>;
            if (!ReferenceEquals(sm, null)) {
                double[] newValues = new double[sm.size];
                //Yeppp.Core.Add_V64fS64f_V64f(sm.values, 0, scalar, newValues, 0, sm.size);
                value = Yeppp.Core.Sum_V64f_S64f(sm.values, 0,  sm.size);
                return true;
            }
            throw new NotImplementedException();
        }

        public void AddVectors<T>(T[] x, T[] y, T[] result) {
            throw new NotImplementedException();
        }

        public bool MapBatch<K, V, V2>(FSharpFunc<V, V2> mapF, IReadOnlyOrderedMap<K, V> batch, out IReadOnlyOrderedMap<K, V2> value) {
            throw new NotImplementedException();
        }
    }
}
