//// This Source Code Form is subject to the terms of the Mozilla Public
//// License, v. 2.0. If a copy of the MPL was not distributed with this
//// file, You can obtain one at http://mozilla.org/MPL/2.0/.

//using System;

//// ReSharper disable once CheckNamespace
//namespace Spreads
//{
//    /// <summary>
//    /// All keys are transformed into UTC on writes and back on reads
//    /// </summary>
//    internal class ZonedSeries<TValue> : ConvertMutableSeries<DateTime, TValue, DateTime, TValue, ZonedSeries<TValue>>
//    {
//        //private readonly IMutableSeries<DateTime, V> _map;
//        private readonly string _tz;

//        public ZonedSeries(IMutableSeries<DateTime, TValue> map, string originalTimeZone) : base(map)
//        {
//            //_map = map;
//            _tz = originalTimeZone;
//        }

//        public ZonedSeries()
//        {
//        }

//        public override DateTime ToKey2(DateTime key)
//        {
//            return key.ConvertToZoneWithUncpecifiedKind(_tz);
//        }

//        public override TValue ToValue2(TValue value)
//        {
//            return value;
//        }

//        public override DateTime ToKey(DateTime key2)
//        {
//            return key2.ConvertToUtcWithUncpecifiedKind(_tz);
//        }

//        public sealed override TValue ToValue(TValue value2)
//        {
//            return value2;
//        }

//        public override void Dispose(bool disposing)
//        {
//            // disable pooling
//            var disposable = Inner as IDisposable;
//            disposable?.Dispose();
//            if (disposing)
//            {
//                GC.SuppressFinalize(this);
//            }
//        }
//    }
//}