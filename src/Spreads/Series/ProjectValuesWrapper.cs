// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;

// ReSharper disable once CheckNamespace
namespace Spreads {

    /// <summary>
    /// Projects values from source to destination and back
    /// </summary>
    public class ProjectValuesWrapper<K, Vsrc, Vdest> : ConvertMutableSeries<K, Vsrc, K, Vdest, ProjectValuesWrapper<K, Vsrc, Vdest>>, IPersistentSeries<K, Vdest> {
        private IMutableSeries<K, Vsrc> _innerMap;
        private Func<Vsrc, Vdest> _srcToDest;
        private Func<Vdest, Vsrc> _destToSrc;

        public ProjectValuesWrapper(IMutableSeries<K, Vsrc> innerMap, Func<Vsrc, Vdest> srcToDest, Func<Vdest, Vsrc> destToSrc) : base(innerMap) {
            _innerMap = innerMap;
            _srcToDest = srcToDest;
            _destToSrc = destToSrc;
        }

        public ProjectValuesWrapper() {
        }

        public sealed override K ToKey2(K key) {
            return key;
        }

        public sealed override Vdest ToValue2(Vsrc value) {
            return _srcToDest(value);
        }

        public sealed override K ToKey(K key2) {
            return key2;
        }

        public sealed override Vsrc ToValue(Vdest value2) {
            return _destToSrc(value2);
        }

        internal static ProjectValuesWrapper<K, Vsrc, Vdest> Create(IMutableSeries<K, Vsrc> innerMap, Func<Vsrc, Vdest> srcToDest, Func<Vdest, Vsrc> destToSrc) {
            var inst = Create(innerMap);
            inst._srcToDest = srcToDest;
            inst._destToSrc = destToSrc;
            return inst;
        }

        public override void Dispose(bool disposing) {
            Flush();
            _innerMap = null;
            _srcToDest = null;
            _destToSrc = null;
            base.Dispose(disposing);
        }

        public string Id => (_innerMap as IPersistentObject)?.Id ?? "";

        public void Flush() {
            (_innerMap as IPersistentObject)?.Flush();
        }
    }
}
