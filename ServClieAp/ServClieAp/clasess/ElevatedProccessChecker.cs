using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace ServClieAp.clasess
{
    internal static class ElevatedProccessChecker
    {
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(IntPtr TokenHandle, TOKEN_INFORMATION_CLASS TokenInformationClass, IntPtr TokenInformation, int TokenInformationLength, out int ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint TOKEN_QUERY = 0x0008;

        private enum TOKEN_INFORMATION_CLASS
        {
            TokenUser = 1,
            TokenElevation,
            // Other values are omitted for brevity
        }

        private struct TOKEN_ELEVATION
        {
            public int TokenIsElevated;
        }

        private static bool IsProcessElevated(Process process)
        {
            IntPtr tokenHandle = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(process.Handle, TOKEN_QUERY, out tokenHandle))
                {
                    Console.WriteLine($"OpenProcessToken failed for process {process.ProcessName}, ID {process.Id}");
                    return false;
                }

                TOKEN_ELEVATION elevation;
                int elevationSize = Marshal.SizeOf(typeof(TOKEN_ELEVATION));
                IntPtr elevationPtr = Marshal.AllocHGlobal(elevationSize);

                try
                {
                    if (!GetTokenInformation(tokenHandle, TOKEN_INFORMATION_CLASS.TokenElevation, elevationPtr, elevationSize, out _))
                    {
                        Console.WriteLine($"GetTokenInformation failed for process {process.ProcessName}, ID {process.Id}");
                        return false;
                    }

                    elevation = Marshal.PtrToStructure<TOKEN_ELEVATION>(elevationPtr);
                    return elevation.TokenIsElevated != 0;
                }
                finally
                {
                    Marshal.FreeHGlobal(elevationPtr);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception while checking elevation for process {process.ProcessName}, ID {process.Id}: {ex.Message}");
                return false;
            }
            finally
            {
                if (tokenHandle != IntPtr.Zero)
                {
                    CloseHandle(tokenHandle);
                }
            }
        }

        public static string GetProcesses()
        {
            try
            {
                Process[] processes = Process.GetProcesses().OrderBy(p => p.ProcessName).ToArray();
                StringBuilder result = new StringBuilder();
                result.AppendLine("Processes:");
                foreach (Process process in processes)
                {
                    string isElevated = IsProcessElevated(process) ? "Admin" : "User";
                    result.AppendLine($"- Name: {process.ProcessName}, ID: {process.Id}, Privileges: {isElevated}");
                }
                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting processes: {ex.Message}";
            }
        }
    }
}
