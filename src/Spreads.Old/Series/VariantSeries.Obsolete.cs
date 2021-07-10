//// This Source Code Form is subject to the terms of the Mozilla Public
//// License, v. 2.0. If a copy of the MPL was not distributed with this
//// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
//using Spreads.DataTypes;
//
//// ReSharper disable once CheckNamespace
//namespace Spreads.Obsolete
//{
//    public class VariantSeries<TKey, TValue> : ConvertSeries<TKey, TValue, Variant, Variant, VariantSeries<TKey, TValue>>, ISeries
//    {
//        public VariantSeries()
//        {
//
//        }
//
//        public VariantSeries(ISeries<TKey, TValue> inner) : base(inner)
//        {
//        }
//
//        public override Variant ToKey2(TKey key)
//        {
//            return Variant.Create(key);
//        }
//
//        public override Variant ToValue2(TValue value)
//        {
//            return Variant.Create(value);
//        }
//
//        public override TKey ToKey(Variant key)
//        {
//            return key.Get<TKey>();
//        }
//
//        public override TValue ToValue(Variant value)
//        {
//            return value.Get<TValue>();
//        }
//
//        public TypeEnum KeyType { get; } = VariantHelper<TKey>.TypeEnum;
//        public TypeEnum ValueType { get; } = VariantHelper<TValue>.TypeEnum;
//    }
//
//    public class MutableVariantSeries<TKey, TValue> : ConvertMutableSeries<TKey, TValue, Variant, Variant, MutableVariantSeries<TKey, TValue>>, ISeries
//    {
//        public MutableVariantSeries()
//        {
//        }
//
//        public MutableVariantSeries(IMutableSeries<TKey, TValue> innerSeries) : base(innerSeries)
//        {
//        }
//
//        public override Variant ToKey2(TKey key)
//        {
//            return Variant.Create(key);
//        }
//
//        public override Variant ToValue2(TValue value)
//        {
//            return Variant.Create(value);
//        }
//
//        public override TKey ToKey(Variant key)
//        {
//            return key.Get<TKey>();
//        }
//
//        public override TValue ToValue(Variant value)
//        {
//            return value.Get<TValue>();
//        }
//
//        /// <inheritdoc />
//        public TypeEnum KeyType { get; } = VariantHelper<TKey>.TypeEnum;
//        /// <inheritdoc />
//        public TypeEnum ValueType { get; } = VariantHelper<TValue>.TypeEnum;
//    }
//}