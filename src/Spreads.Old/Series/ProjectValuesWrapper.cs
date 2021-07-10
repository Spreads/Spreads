//// This Source Code Form is subject to the terms of the Mozilla Public
//// License, v. 2.0. If a copy of the MPL was not distributed with this
//// file, You can obtain one at http://mozilla.org/MPL/2.0/.

//using System;
//using System.Threading.Tasks;

//// ReSharper disable once CheckNamespace
//namespace Spreads
//{
//    /// <summary>
//    /// Projects values from source to destination and back
//    /// </summary>
//    public class ProjectValuesWrapper<TKey, TVsrc, TVdest> : ConvertMutableSeries<TKey, TVsrc, TKey, TVdest, ProjectValuesWrapper<TKey, TVsrc, TVdest>>, IPersistentSeries<TKey, TVdest>
//    {
//        private IMutableSeries<TKey, TVsrc> _innerMap;
//        private Func<TVsrc, TVdest> _srcToDest;
//        private Func<TVdest, TVsrc> _destToSrc;

//        public ProjectValuesWrapper(IMutableSeries<TKey, TVsrc> innerMap, Func<TVsrc, TVdest> srcToDest, Func<TVdest, TVsrc> destToSrc) : base(innerMap)
//        {
//            _innerMap = innerMap;
//            _srcToDest = srcToDest;
//            _destToSrc = destToSrc;
//        }

//        public ProjectValuesWrapper()
//        {
//        }

//        public sealed override TKey ToKey2(TKey key)
//        {
//            return key;
//        }

//        public sealed override TVdest ToValue2(TVsrc value)
//        {
//            return _srcToDest(value);
//        }

//        public sealed override TKey ToKey(TKey key2)
//        {
//            return key2;
//        }

//        public sealed override TVsrc ToValue(TVdest value2)
//        {
//            return _destToSrc(value2);
//        }

//        internal static ProjectValuesWrapper<TKey, TVsrc, TVdest> Create(IMutableSeries<TKey, TVsrc> innerMap, Func<TVsrc, TVdest> srcToDest, Func<TVdest, TVsrc> destToSrc)
//        {
//            var inst = Create(innerMap);
//            inst._srcToDest = srcToDest;
//            inst._destToSrc = destToSrc;
//            return inst;
//        }

//        public override void Dispose(bool disposing)
//        {
//            Flush();
//            _innerMap = null;
//            _srcToDest = null;
//            _destToSrc = null;
//            base.Dispose(disposing);
//        }

//        public new string Id => (_innerMap as IPersistentObject)?.Id ?? "";

//        public new Task Flush()
//        {
//            return (_innerMap as IPersistentObject)?.Flush();
//        }
//    }
//}