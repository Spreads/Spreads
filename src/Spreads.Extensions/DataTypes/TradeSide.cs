namespace Spreads.DataTypes
{
    public enum TradeSide : byte
    {
        /// <summary>
        /// By default, this should be always zero, so if there is an error and we forget to specify the Side, we must fail fast.
        /// </summary>
        None = 0 ,
        Buy = 1,
        Sell = 255, // -1 for signed byte
    }
}