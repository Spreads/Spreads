namespace Spreads.DataTypes
{
    public enum Side : byte
    {
        None = 0, // By default, this should be always zero, so if there is an error and we forget to specify the Side, we must fail fast.
        Buy = 1,
        Sell = 255, // -1 for signed byte
    }
}