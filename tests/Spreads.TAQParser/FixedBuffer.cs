using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Spreads;
using Spreads.Serialization;

namespace TAQParse {

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct TaqTrade {
        public DateTime Time => new DateTime(_timeUTCTicks, DateTimeKind.Utc);
        private long _timeUTCTicks;

        public byte Exchange;

        private fixed byte _symbol[16];
        public string Symbol {
            get {
                fixed (void* ptr = _symbol)
                {
                    return Marshal.PtrToStringAnsi((IntPtr)ptr).Trim(' ');

                }
            }
        }

        private fixed byte _saleCondition[4];
        public string SaleCondition {
            get {
                fixed (void* ptr = _saleCondition)
                {
                    return Marshal.PtrToStringAnsi((IntPtr)ptr);

                }
            }
        }

        public uint TradeVolume;
        public ulong TradePrice;
        public byte TradeStopStockIndicator;
        public byte TradeCorrectionIndicator;
        public ulong TradeSequenceNumber;
        public byte SourceOfTrade;
        public byte TradeReportingFacility;
        public DateTime ParticipantTimestamp => new DateTime(_participantTimestampUtcTicks, DateTimeKind.Utc);
        private long _participantTimestampUtcTicks;

        private fixed byte _rnn[8];
        public string RNN {
            get {
                fixed (void* ptr = _rnn)
                {
                    return Marshal.PtrToStringAnsi((IntPtr)ptr);

                }
            }
        }

        public DateTime TRFTimestamp => new DateTime(_TRFTimestampUtcTicks, DateTimeKind.Utc);
        private long _TRFTimestampUtcTicks;

        public TaqTrade(DateTime date, FixedBuffer fb)
        {
            // Read<> over fb is much slower
            var db = fb.DirectBuffer;
            // Time
            _timeUTCTicks = ReadHHMMSSXXXXXXAsUtcTicks(date, fb, 0);
            // Exchange
            Exchange = db.ReadByte(12);
            // Symbol
            fixed (void* ptr = _symbol)
            {
                fb.Copy((byte*)ptr, 13, 16);
            }
            // Sale Condition
            fixed (void* ptr = _saleCondition)
            {
                fb.Copy((byte*)ptr, 29, 4);
            }

            TradeVolume = (uint)ReadUInt64(fb, 33, 9);
            TradePrice = ReadUInt64(fb, 42, 11);
            TradeStopStockIndicator = db.ReadByte(53);

            TradeCorrectionIndicator = (byte)ReadUInt64(fb, 54, 2);
            TradeSequenceNumber = ReadUInt64(fb, 56, 16);
            SourceOfTrade = db.ReadByte(72);
            TradeReportingFacility = db.ReadByte(73);

            _participantTimestampUtcTicks = ReadHHMMSSXXXXXXAsUtcTicks(date, fb, 74);

            fixed (void* ptr = _rnn)
            {
                fb.Copy((byte*)ptr, 86, 8);
            }

            _TRFTimestampUtcTicks = ReadHHMMSSXXXXXXAsUtcTicks(date, fb, 94);
        }


        private static long ReadHHMMSSXXXXXXAsUtcTicks(DateTime date, DirectBuffer db, int index)
        {
            // TODO method ReadAsciiDigit
            var hh = (db.ReadByte(index) - '0') * 10 + db.ReadByte(index + 1) - '0';
            var mm = (db.ReadByte(index + 2) - '0') * 10 + db.ReadByte(index + 3) - '0';
            var ss = (db.ReadByte(index + 4) - '0')* 10 + db.ReadByte(index + 5) - '0';
            var micros = (db.ReadByte(index + 6) - '0')* 100000
                          + (db.ReadByte(index + 7) - '0')*10000
                          + (db.ReadByte(index + 8) - '0')*1000
                          + (db.ReadByte(index + 9) - '0')*100
                          + (db.ReadByte(index + 10) - '0')*10
                          + (db.ReadByte(index + 11) - '0');
            var ticks = date.Date.Ticks
            // hours
            + hh * TimeSpan.TicksPerHour
            // minutes
            + mm * TimeSpan.TicksPerMinute
            // seconds
            + ss * TimeSpan.TicksPerSecond
            // micros
            + micros * 10;
            var dt = new DateTime(ticks, DateTimeKind.Unspecified);

            // this is pefromance killer - for the same date delta is always the same, should 
            // calculate utc via ticks by adding pre-calculated delta 
            //dt = dt.ConvertToUtcWithUncpecifiedKind("ny");

            return dt.Ticks;
        }

        private static ulong ReadUInt64(DirectBuffer db, int index, int length)
        {
            ulong ret = 0;
            for (int pos = 0; pos < length; pos++)
            {
                byte b = (byte)(db.ReadByte(index + pos) - '0');
                if (b > 0)
                {
                    ret += b * (ULongPower(10, (short)(length - pos - 1)));
                }
            }
            return ret;
        }

        // takes 6.3% of entire execution. Either not inlined or not efficient
        private static ulong ULongPower(ulong x, short power) {
            if (power == 0) return 1;
            if (power == 1) return x;
            // ----------------------
            int n = 15;
            while ((power <<= 1) >= 0) n--;

            ulong tmp = x;
            while (--n > 0)
                tmp = tmp * tmp *
                     (((power <<= 1) < 0) ? x : 1);
            return tmp;
        }
    }

    public unsafe struct TaqTradeBuffer106 {
        public fixed byte buffer[106];
    }
}
