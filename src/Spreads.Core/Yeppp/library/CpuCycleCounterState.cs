namespace Spreads.Yeppp
{

	/// <summary>The state of the processor cycle counter.</summary>
	/// <remarks>This class is intended for use only through <see cref="Library.AcquireCycleCounter" /> and <see cref="Library.ReleaseCycleCounter" /> methods.</remarks>
	/// <seealso cref="Library.AcquireCycleCounter" />
	/// <seealso cref="Library.ReleaseCycleCounter" />
	public sealed class CpuCycleCounterState
	{

		internal CpuCycleCounterState(ulong state)
		{
			this.state = state;
		}

		/// <summary>Destroys the state object and releases the allocated system resources if they were not released by a call to <see cref="Library.ReleaseCycleCounter" />.</summary>
		~CpuCycleCounterState()
		{
			if (this.IsValid)
			{
				Library.ReleaseCycleCounter(this);
			}
		}

		/// <summary>Indicates whether this is a valid state of a processor cycle counter (i.e. it was not yet released via a call to <see cref="Library.ReleaseCycleCounter" />).</summary>
		public bool IsValid
		{
			get
			{
				return this.state != 0;
			}
		}

		internal ulong state;

	}

}
