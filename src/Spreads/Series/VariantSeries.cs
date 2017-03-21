using Spreads.DataTypes;

// ReSharper disable once CheckNamespace
namespace Spreads
{
    public class VariantSeries<TKey, TValue> : ConvertSeries<TKey, TValue, Variant, Variant, VariantSeries<TKey, TValue>>
    {
        public VariantSeries()
        {
        }

        public VariantSeries(IReadOnlySeries<TKey, TValue> inner) : base(inner)
        {
        }

        public override Variant ToKey2(TKey key)
        {
            return Variant.Create(key);
        }

        public override Variant ToValue2(TValue value)
        {
            return Variant.Create(value);
        }

        public override TKey ToKey(Variant key)
        {
            return key.Get<TKey>();
        }

        public override TValue ToValue(Variant value)
        {
            return value.Get<TValue>();
        }

        public TypeEnum KeyType { get; } = VariantHelper<TKey>.TypeEnum;
        public TypeEnum ValueType { get; } = VariantHelper<TValue>.TypeEnum;
    }

    public class VariantMutableSeries<TKey, TValue> : ConvertMutableSeries<TKey, TValue, Variant, Variant, VariantMutableSeries<TKey, TValue>>
    {
        public VariantMutableSeries()
        {
        }

        public VariantMutableSeries(IMutableSeries<TKey, TValue> innerSeries) : base(innerSeries)
        {
        }

        public override Variant ToKey2(TKey key)
        {
            return Variant.Create(key);
        }

        public override Variant ToValue2(TValue value)
        {
            return Variant.Create(value);
        }

        public override TKey ToKey(Variant key)
        {
            return key.Get<TKey>();
        }

        public override TValue ToValue(Variant value)
        {
            return value.Get<TValue>();
        }

        public TypeEnum KeyType { get; } = VariantHelper<TKey>.TypeEnum;
        public TypeEnum ValueType { get; } = VariantHelper<TValue>.TypeEnum;
    }
}