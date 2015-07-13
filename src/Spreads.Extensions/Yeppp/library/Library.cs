/*
 *                      Yeppp! library implementation
 *
 * This file is part of Yeppp! library and licensed under the New BSD license.
 * See LICENSE.txt for the full text of the license.
 */

using Bootstrap;
using System.Runtime.InteropServices;

/*! <summary>Yeppp! library functions.</summary> */
namespace Yeppp
{

	/// <summary>Yeppp! library functions.</summary>
	internal static class NamespaceDoc
	{
	}

	internal enum Status : uint
	{
		Ok = 0,
		NullPointer = 1,
		MisalignedPointer = 2,
		InvalidArgument = 3,
		InvalidData = 4,
		InvalidState = 5,
		UnsupportedHardware = 6,
		UnsupportedSoftware = 7,
		InsufficientBuffer = 8,
		OutOfMemory = 9,
		SystemError = 10
	}

	internal enum Enumeration : uint
	{
		Status = 0,
		CpuArchitecture = 1,
		CpuVendor = 2,
		CpuMicroarchitecture = 3,
		CpuBriefName = 4,
		CpuFullName = 5,
		GenericIsaFeature    = 0x100 + 0,
		GenericSimdFeature   = 0x200 + 0,
		GenericSystemFeature = 0x300 + 0,
		X86IsaFeature        = 0x100 + 1,
		X86SimdFeature       = 0x200 + 1,
		X86SystemFeature     = 0x300 + 1,
		ARMIsaFeature        = 0x100 + 2,
		ARMSimdFeature       = 0x200 + 2,
		ARMSystemFeature     = 0x300 + 2,
		MIPSIsaFeature       = 0x100 + 3,
		MIPSSimdFeature      = 0x200 + 3,
		MIPSSystemFeature    = 0x300 + 3,
		PowerPCIsaFeature    = 0x100 + 4,
		PowerPCSimdFeature   = 0x200 + 4,
		PowerPCSystemFeature = 0x300 + 4,
		IA64IsaFeature       = 0x100 + 5,
		IA64SimdFeature      = 0x200 + 5,
		IA64SystemFeature    = 0x300 + 5,
		SPARCIsaFeature      = 0x100 + 6,
		SPARCSimdFeature     = 0x200 + 6,
		SPARCSystemFeature   = 0x300 + 6
	}

	internal enum StringType : uint
	{
		Description = 0,
		ID = 1
	}

	/// <summary>Non-computational functions for checking library version, quering information about processor, and benchmarking.</summary>
	public class Library
	{
	    static Library()
	    {
	        ABI = Bootstrapper.ABI;
	        Init();

	    }
        internal static ABI ABI { get; set; }
        internal static void Init()
		{
			Status status = yepLibrary_Init();
			if (status != Status.Ok)
				throw new System.SystemException("Failed to initialize Yeppp! library");
		}

		/// <summary>Deinitializes Yeppp! library and releases all associated resources.</summary>
		/// <remarks>This function is inteded for .Net libraries which might be unloaded (e.g. plugins) and should be called immediately before the module is unloaded.</remarks>
		/// <remarks>Calling Yeppp! functions after after a call to this method is prohibited and might crash the application.</remarks>
		/// <exception cref="System.SystemException">Resources allocated by Yeppp! library could not be unloaded due to memory corruption.</exception>
		/// <exception cref="System.InvalidOperationException">Yeppp! library is already released by this module.</exception>
		public static void Release()
		{
			Status status = yepLibrary_Release();
			if (status != Status.Ok)
				throw GetException(status);
		}

		/// <summary>Provides information about Yeppp! library version.</summary>
		/// <returns>An object describing Yeppp! library version.</returns>
		/// <seealso cref="Version" />
		public static Version GetVersion()
		{
			System.IntPtr versionPointer = yepLibrary_GetVersion();
			uint major = unchecked((uint)Marshal.ReadInt32(versionPointer, 0));
			uint minor = unchecked((uint)Marshal.ReadInt32(versionPointer, 4));
			uint patch = unchecked((uint)Marshal.ReadInt32(versionPointer, 8));
			uint build = unchecked((uint)Marshal.ReadInt32(versionPointer, 12));
			System.IntPtr releaseNamePointer = Marshal.ReadIntPtr(versionPointer, 16);
			string releaseName = string.Empty;
			int length = 0;
			while (Marshal.ReadByte(releaseNamePointer, length) != 0)
				length++;

			if (length != 0)
			{
				byte[] releaseNameBuffer = new byte[length];
				Marshal.Copy(releaseNamePointer, releaseNameBuffer, 0, length);
				releaseName = System.Text.Encoding.UTF8.GetString(releaseNameBuffer);
			}

			return new Version(major, minor, patch, build, releaseName);
		}

		/// <summary>Checks if the specified ISA extension is supported by the processor.</summary>
		/// <param name="isaFeature">An object specifying the ISA extension of interest.</param>
		/// <returns>true if processor supports the specified ISA extension and false otherwise.</returns>
		/// <seealso cref="CpuIsaFeature" />
		/// <seealso cref="X86CpuIsaFeature" />
		/// <seealso cref="ArmCpuIsaFeature" />
		/// <seealso cref="MipsCpuIsaFeature" />
		/// <seealso cref="IA64CpuIsaFeature" />
		public static bool IsSupported(CpuIsaFeature isaFeature)
		{
			ulong isaFeaturesMask = 0;
			Status status = yepLibrary_GetCpuIsaFeatures(out isaFeaturesMask);
			if (status != Status.Ok)
				throw GetException(status);

			return (unchecked((long)isaFeaturesMask) & (1L << unchecked((int)isaFeature.Id))) != 0;
		}

		/// <summary>Checks if the specified SIMD extension is supported by the processor.</summary>
		/// <param name="simdFeature">An object specifying the SIMD extension of interest.</param>
		/// <returns>true if processor supports the specified SIMD extension and false otherwise.</returns>
		/// <seealso cref="CpuSimdFeature" />
		/// <seealso cref="X86CpuSimdFeature" />
		/// <seealso cref="ArmCpuSimdFeature" />
		/// <seealso cref="MipsCpuSimdFeature" />
		public static bool IsSupported(CpuSimdFeature simdFeature)
		{
			ulong simdFeaturesMask = 0;
			Status status = yepLibrary_GetCpuSimdFeatures(out simdFeaturesMask);
			if (status != Status.Ok)
				throw GetException(status);

			return (unchecked((long)simdFeaturesMask) & (1L << unchecked((int)simdFeature.Id))) != 0;
		}

		/// <summary>Checks if the specified non-ISA CPU or system extension is supported by the processor.</summary>
		/// <param name="systemFeature">An object specifying the non-ISA CPU or system extension of interest.</param>
		/// <returns>true if processor supports the specified non-ISA CPU or system extension and false otherwise.</returns>
		/// <seealso cref="CpuSimdFeature" />
		/// <seealso cref="X86CpuSystemFeature" />
		/// <seealso cref="ArmCpuSystemFeature" />
		public static bool IsSupported(CpuSystemFeature systemFeature)
		{
			ulong systemFeaturesMask = 0;
			Status status = yepLibrary_GetCpuSystemFeatures(out systemFeaturesMask);
			if (status != Status.Ok)
				throw GetException(status);

			return (unchecked((long)systemFeaturesMask) & (1L << unchecked((int)systemFeature.Id))) != 0;
		}

		/// <summary>Provides information about the vendor of the processor.</summary>
		/// <returns>A CpuVendor object with information about the company which designed the CPU core.</returns>
		/// <seealso cref="CpuVendor" />
		public static CpuVendor GetCpuVendor()
		{
			uint cpuVendorId = 0;
			Status status = yepLibrary_GetCpuVendor(out cpuVendorId);
			if (status != Status.Ok)
				throw GetException(status);

			return new CpuVendor(cpuVendorId);
		}

		/// <summary>Provides information about the architecture of the processor.</summary>
		/// <returns>A CpuArchitecture instance with information about the architecture of the CPU.</returns>
		/// <seealso cref="CpuArchitecture" />
		public static CpuArchitecture GetCpuArchitecture()
		{
			uint cpuArchitectureId = 0;
			Status status = yepLibrary_GetCpuArchitecture(out cpuArchitectureId);
			if (status != Status.Ok)
				throw GetException(status);

			return new CpuArchitecture(cpuArchitectureId);
		}

		/// <summary>Provides information about the microarchitecture of the processor.</summary>
		/// <returns>A CpuMicroarchitecture instance with information about the microarchitecture of the CPU core.</returns>
		/// <seealso cref="CpuMicroarchitecture" />
		public static CpuMicroarchitecture GetCpuMicroarchitecture()
		{
			uint cpuMicroarchitectureId = 0;
			Status status = yepLibrary_GetCpuMicroarchitecture(out cpuMicroarchitectureId);
			if (status != Status.Ok)
				throw GetException(status);

			return new CpuMicroarchitecture(cpuMicroarchitectureId);
		}

		/// <summary>Initializes the processor cycle counter and starts counting the processor cycles.</summary>
		/// <remarks>Call <see cref="ReleaseCycleCounter" /> to get the number of processor cycles passed.</remarks>
		/// <remarks>The cycle counters are not guaranteed to be syncronized across different processors/cores in a multiprocessor/multicore system. It is recommended to bind the current thread to a particular logical processor before using this function.</remarks>
		/// <returns>An object representing the state of the processor cycle counter. Pass this object to <see cref="ReleaseCycleCounter" /> to get the number of cycles passed.</returns>
		/// <exception cref="System.PlatformNotSupportedException">The processor does not have a cycle counter or the operating system does not provide access to the CPU cycle counter</exception>
		/// <exception cref="System.SystemException">If the attempt to read the cycle counter or release the OS resources failed inside the OS kernel.</exception>
		/// <seealso cref="ReleaseCycleCounter" />
		public static CpuCycleCounterState AcquireCycleCounter() {
			ulong state = 0;
			Status status = yepLibrary_GetCpuCyclesAcquire(out state);
			if (status != Status.Ok)
				throw GetException(status);

			return new CpuCycleCounterState(state);
		}

		/// <summary>Stops counting the processor cycles, releases the system resources associated with the cycle counter, and returns the number of cycles elapsed.</summary>
		/// <param name="cycleCounter">An object representing the state of the cycle counter returned by <see cref="AcquireCycleCounter()" />. The cycle counter should be released only once, and this function invalidates the state object.</param>
		/// <returns>The number of cycles elapsed since the call to <see cref="AcquireCycleCounter()" />.</returns>
		/// <exception cref="System.InvalidOperationException">The cycleCounter object is not a valid state of the cycle counter. This can happen if the cycleCounter object was released previously.</exception>
		/// <exception cref="System.PlatformNotSupportedException">The processor does not have a cycle counter or the operating system does not provide access to the CPU cycle counter</exception>
		/// <exception cref="System.SystemException">If the attempt to read the cycle counter or release the OS resources failed inside the OS kernel.</exception>
		/// <seealso cref="AcquireCycleCounter()" />
		public static ulong ReleaseCycleCounter(CpuCycleCounterState cycleCounter) {
			ulong ticks;
			Status status = yepLibrary_GetCpuCyclesRelease(ref cycleCounter.state, out ticks);
			if (status != Status.Ok)
				throw GetException(status);

			return ticks;
		}

		/// <summary>Queries the ticks count of the high-resolution system timer.</summary>
		/// <remarks>The difference in ticks between two time moments divided by timer frequency gives the number of seconds between two time moments.</remarks>
		/// <returns>The current ticks count of the high-resolution system timer.</returns>
		/// <exception cref="System.SystemException">The attempt to read the high-resolution timer failed inside the OS kernel.</exception>
		public static ulong GetTimerTicks()
		{
			ulong ticks;
			Status status = yepLibrary_GetTimerTicks(out ticks);
			if (status != Status.Ok)
				throw GetException(status);

			return ticks;
		}

		/// <summary>Queries the frequency (number of ticks per second) of the high-resolution system timer.</summary>
		/// <remarks>The difference in ticks between two time moments divided by timer frequency gives the number of seconds between two time moments.</remarks>
		/// <returns>The frequency of the high-resolution system timer.</returns>
		/// <exception cref="System.SystemException">The attempt to read the high-resolution timer frequency failed inside the OS kernel.</exception>
		public static ulong GetTimerFrequency()
		{
			ulong frequency;
			Status status = yepLibrary_GetTimerFrequency(out frequency);
			if (status != Status.Ok)
				throw GetException(status);

			return frequency;
		}

		/// <summary>Detects the minimum time difference in nanoseconds which can be measured by the high-resolution system timer.</summary>
		/// <returns>The accuracy (in nanoseconds) of the high-resolution system timer.</returns>
		/// <exception cref="System.SystemException">If the attempt to measure the accuracy of high-resolution timer failed inside the OS kernel.</exception>
		public static ulong GetTimerAccuracy()
		{
			ulong accuracy;
			Status status = yepLibrary_GetTimerAccuracy(out accuracy);
			if (status != Status.Ok)
				throw GetException(status);

			return accuracy;
		}

		internal static string GetString(Enumeration enumeration, uint value, StringType stringType) {
			System.UIntPtr length = System.UIntPtr.Zero;
			Status status = Library.yepLibrary_GetString(enumeration, value, stringType, System.IntPtr.Zero, ref length);
			if (status != Status.InsufficientBuffer)
				throw new System.SystemException();

			int smallLength = checked((int)length);
			System.IntPtr unmanagedBuffer = Marshal.AllocHGlobal(smallLength);
			try {
				status = yepLibrary_GetString(enumeration, value, stringType, unmanagedBuffer, ref length);
				if (status != Status.Ok)
					throw new System.SystemException();

				byte[] managedBuffer = new byte[smallLength];
				Marshal.Copy(unmanagedBuffer, managedBuffer, 0, smallLength);
				return System.Text.Encoding.UTF8.GetString(managedBuffer);
			} finally {
				Marshal.FreeHGlobal(unmanagedBuffer);
			}
		}

		internal static bool IsDefined(Enumeration enumeration, uint value) {
			System.UIntPtr length = System.UIntPtr.Zero;
			Status status = Library.yepLibrary_GetString(enumeration, value, StringType.Description, System.IntPtr.Zero, ref length);
			return status == Status.InsufficientBuffer;
		}

		internal static System.Exception GetException(Status status) {
			switch (status) {
				case Status.Ok:
					return null;
				case Status.NullPointer:
					return new System.NullReferenceException(GetString(Enumeration.Status, unchecked((uint)status), StringType.Description));
				case Status.MisalignedPointer:
					return new System.DataMisalignedException(GetString(Enumeration.Status, unchecked((uint)status), StringType.Description));
				case Status.InvalidArgument:
					return new System.ArgumentException(GetString(Enumeration.Status, unchecked((uint)status), StringType.Description));
				case Status.InvalidData:
					return new System.IO.InvalidDataException(GetString(Enumeration.Status, unchecked((uint)status), StringType.Description));
				case Status.InvalidState:
					return new System.InvalidOperationException(GetString(Enumeration.Status, unchecked((uint)status), StringType.Description));
				case Status.UnsupportedHardware:
				case Status.UnsupportedSoftware:
					return new System.PlatformNotSupportedException(GetString(Enumeration.Status, unchecked((uint)status), StringType.Description));
				case Status.InsufficientBuffer:
					return new System.IO.InternalBufferOverflowException(GetString(Enumeration.Status, unchecked((uint)status), StringType.Description));
				case Status.OutOfMemory:
					return new System.OutOfMemoryException(GetString(Enumeration.Status, unchecked((uint)status), StringType.Description));
				case Status.SystemError:
					return new System.SystemException(GetString(Enumeration.Status, unchecked((uint)status), StringType.Description));
				default:
					return new System.Exception(GetString(Enumeration.Status, unchecked((uint)status), StringType.Description));
			}
		}

		[DllImport("yeppp", ExactSpelling=true, CallingConvention=CallingConvention.Cdecl, EntryPoint="yepLibrary_Init")]
		private static extern Status yepLibrary_Init();

		[DllImport("yeppp", ExactSpelling=true, CallingConvention=CallingConvention.Cdecl, EntryPoint="yepLibrary_Release")]
		private static extern Status yepLibrary_Release();

		[DllImport("yeppp", ExactSpelling=true, CallingConvention=CallingConvention.Cdecl, EntryPoint="yepLibrary_GetVersion")]
		[return : MarshalAs(UnmanagedType.SysInt)]
		private static extern System.IntPtr yepLibrary_GetVersion();

		[DllImport("yeppp", ExactSpelling=true, CallingConvention=CallingConvention.Cdecl, EntryPoint="yepLibrary_GetCpuIsaFeatures")]
		private static extern Status yepLibrary_GetCpuIsaFeatures(out ulong isaFeatures);

		[DllImport("yeppp", ExactSpelling=true, CallingConvention=CallingConvention.Cdecl, EntryPoint="yepLibrary_GetCpuSimdFeatures")]
		private static extern Status yepLibrary_GetCpuSimdFeatures(out ulong simdFeatures);

		[DllImport("yeppp", ExactSpelling=true, CallingConvention=CallingConvention.Cdecl, EntryPoint="yepLibrary_GetCpuSystemFeatures")]
		private static extern Status yepLibrary_GetCpuSystemFeatures(out ulong systemFeatures);

		[DllImport("yeppp", ExactSpelling=true, CallingConvention=CallingConvention.Cdecl, EntryPoint="yepLibrary_GetCpuVendor")]
		private static extern Status yepLibrary_GetCpuVendor(out uint vendor);

		[DllImport("yeppp", ExactSpelling=true, CallingConvention=CallingConvention.Cdecl, EntryPoint="yepLibrary_GetCpuArchitecture")]
		private static extern Status yepLibrary_GetCpuArchitecture(out uint architecture);

		[DllImport("yeppp", ExactSpelling=true, CallingConvention=CallingConvention.Cdecl, EntryPoint="yepLibrary_GetCpuMicroarchitecture")]
		private static extern Status yepLibrary_GetCpuMicroarchitecture(out uint vendor);

		[DllImport("yeppp", ExactSpelling=true, CallingConvention=CallingConvention.Cdecl, EntryPoint="yepLibrary_GetCpuCyclesAcquire")]
		private static extern Status yepLibrary_GetCpuCyclesAcquire(out ulong state);

		[DllImport("yeppp", ExactSpelling=true, CallingConvention=CallingConvention.Cdecl, EntryPoint="yepLibrary_GetCpuCyclesRelease")]
		private static extern Status yepLibrary_GetCpuCyclesRelease(ref ulong state, out ulong cycles);

		[DllImport("yeppp", ExactSpelling=true, CallingConvention=CallingConvention.Cdecl, EntryPoint="yepLibrary_GetTimerTicks")]
		private static extern Status yepLibrary_GetTimerTicks(out ulong ticks);

		[DllImport("yeppp", ExactSpelling=true, CallingConvention=CallingConvention.Cdecl, EntryPoint="yepLibrary_GetTimerFrequency")]
		private static extern Status yepLibrary_GetTimerFrequency(out ulong frequency);

		[DllImport("yeppp", ExactSpelling=true, CallingConvention=CallingConvention.Cdecl, EntryPoint="yepLibrary_GetTimerAccuracy")]
		private static extern Status yepLibrary_GetTimerAccuracy(out ulong accuracy);

		[DllImport("yeppp", ExactSpelling=true, CallingConvention=CallingConvention.Cdecl, EntryPoint="yepLibrary_GetString")]
		private static extern Status yepLibrary_GetString(Enumeration enumeration, uint value, StringType stringType, System.IntPtr buffer, ref System.UIntPtr length);

	}

}
