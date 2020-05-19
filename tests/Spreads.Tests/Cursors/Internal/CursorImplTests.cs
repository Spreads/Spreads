using NUnit.Framework;
using Spreads.Collections.Internal;
using Spreads.Core.Tests;
using Spreads.Cursors.Internal;

namespace Spreads.Tests.Cursors.Internal
{
    [TestFixture]
    public class CursorImplTests
    {
        [Test]
        public void CouldCreateCursorImpl()
        {
            var c = CursorImpl.Create();
            c.Dispose();
        }
        
        [Test]
        public void CouldMoveRooted()
        {
            using var c = CursorImpl.Create();

            var block = DataBlock.CreateSeries<int, int>();
            var lastBlock = block;
            
            for (int i = 0; i < DataBlock.MaxNodeSize * 2; i++)
            {
                DataBlock.Append<int, int>(block, ref lastBlock, i, i);
            }

            c.CurrentBlock = block.UnsafeGetValue<DataBlock>(0);

            for (int i = 0; i < DataBlock.MaxNodeSize; i++)
            {
                int key = default;
                int value = default;
                c.Move(1, false, ref key, ref value).ShouldEqual(1);
                key.ShouldEqual(i);
                value.ShouldEqual(i);
            }
            
            for (int i = DataBlock.MaxNodeSize - 2; i >= 0; i--)
            {
                int key = default;
                int value = default;
                c.Move(-1, false, ref key, ref value).ShouldEqual(-1);
                key.ShouldEqual(i);
                value.ShouldEqual(i);
            }
        }
    }
}