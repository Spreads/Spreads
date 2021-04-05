using System;
using System.Text;

namespace Spreads.Core.Tests.Serialization
{
    public struct TestValue : IEquatable<TestValue>
    {
        // public sbyte Number1 { get; set; }
        //
        // public short Number2 { get; set; }
        //
        // public int Number3 { get; set; }
        //
        // public long Number4 { get; set; }
        //
        // public byte Number5 { get; set; }
        //
        // public ushort Number6 { get; set; }
        //
        // public uint Number7 { get; set; }
        //
        // public ulong Number8 { get; set; }

        public double Number9 { get; set; }

        public static TestValue Create(Random random, bool withDouble = false)
        {
            unchecked
            {
                return new TestValue
                {
                    // Number1 = (sbyte)random.Next(),
                    // Number2 = (short)random.Next(),
                    // Number3 = (int)random.Next(),
                    // Number4 = (long)new LongUnion { Int1 = random.Next(), Int2 = random.Next() }.Long,
                    // Number5 = (byte)random.Next(),
                    // Number6 = (ushort)random.Next(),
                    // Number7 = (uint)random.Next(),
                    // Number8 = (ulong)new LongUnion { Int1 = random.Next(), Int2 = random.Next() }.Long,
                    Number9 = withDouble ? Math.Round(random.NextDouble()*1000, 13) : 0,
                };
            }
        }

        public bool Equals(TestValue other)
        {
            return
                // Number1 == other.Number1
                //    && Number2 == other.Number2
                //    && Number3 == other.Number3
                //    && Number4 == other.Number4
                //    && Number5 == other.Number5
                //    && Number6 == other.Number6
                //    && Number7 == other.Number7
                //    && Number8 == other.Number8
                //    &&
                   Number9.Equals(other.Number9)
                ;
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() != this.GetType()) return false;
            return Equals((TestValue) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                // var hashCode = Number1.GetHashCode();
                // hashCode = (hashCode * 397) ^ Number2.GetHashCode();
                // hashCode = (hashCode * 397) ^ Number3;
                // hashCode = (hashCode * 397) ^ Number4.GetHashCode();
                // hashCode = (hashCode * 397) ^ Number5.GetHashCode();
                // hashCode = (hashCode * 397) ^ Number6.GetHashCode();
                // hashCode = (hashCode * 397) ^ (int) Number7;
                // hashCode = (hashCode * 397) ^ Number8.GetHashCode();
                // hashCode = (hashCode * 397) ^ Number9.GetHashCode();
                // return hashCode;

                return Number9.GetHashCode();
            }
        }
    }
}
