using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VideoConverter {

    /// <summary>
    /// A utility class to determine a process parent.
    /// </summary>
    /// <remarks><a href="https://stackoverflow.com/a/3346055/979493">Source</a></remarks>
    [StructLayout(LayoutKind.Sequential)]
    public struct ParentProcessUtilities {

        // These members must match PROCESS_BASIC_INFORMATION
        private readonly IntPtr Reserved1;
        private readonly IntPtr PebBaseAddress;
        private readonly IntPtr Reserved2_0;
        private readonly IntPtr Reserved2_1;
        private readonly IntPtr UniqueProcessId;
        private          IntPtr InheritedFromUniqueProcessId;

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(IntPtr  processHandle, int processInformationClass, ref ParentProcessUtilities processInformation, int processInformationLength,
                                                            out int returnLength);

        /// <summary>
        /// Gets the parent process of the current process.
        /// </summary>
        /// <returns>An instance of the Process class.</returns>
        /// <exception cref="Win32Exception"></exception>
        public static Process getParentProcess() {
            return getParentProcess(Process.GetCurrentProcess().Handle);
        }

        /// <summary>
        /// Gets the parent process of specified process.
        /// </summary>
        /// <param name="id">The process id.</param>
        /// <returns>An instance of the Process class.</returns>
        /// <exception cref="Win32Exception"></exception>
        public static Process getParentProcess(int id) {
            Process process = Process.GetProcessById(id);
            return getParentProcess(process.Handle);
        }

        /// <summary>
        /// Gets the parent process of a specified process.
        /// </summary>
        /// <param name="handle">The process handle.</param>
        /// <exception cref="Win32Exception"></exception>
        /// <returns>An instance of the Process class.</returns>
        public static Process getParentProcess(IntPtr handle) {
            ParentProcessUtilities pbi    = new();
            int                    status = NtQueryInformationProcess(handle, 0, ref pbi, Marshal.SizeOf(pbi), out int _);
            if (status != 0) {
                throw new Win32Exception(status);
            }

            try {
                return Process.GetProcessById(pbi.InheritedFromUniqueProcessId.ToInt32());
            } catch (ArgumentException) {
                // not found
                return null;
            }
        }

    }

}