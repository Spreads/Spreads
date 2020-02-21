namespace Spreads.Collections.Concurrent
{
    /// <summary>
    /// Make all fields of inherited classes padded by 48 bytes,
    /// which together with the object header and the method
    /// table pointer give 64 bytes. 
    /// </summary>
    public class LeftPad48
    {
        private long _padding0;
        private long _padding1;
        private long _padding2;
        private long _padding3;
        private long _padding4;
        private long _padding5;
    }

    /// <summary>
    /// Make all fields of inherited classes padded by 112 bytes,
    /// which together with the object header and the method
    /// table pointer give 128 bytes. 
    /// </summary>
    public class LeftPad112 : LeftPad48
    {
        private long _padding0;
        private long _padding1;
        private long _padding2;
        private long _padding3;
        private long _padding4;
        private long _padding5;
        private long _padding6;
        private long _padding7;
    }
}