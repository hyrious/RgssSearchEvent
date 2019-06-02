using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace DynamicDllImport
{
    class Program
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string name);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string name);

        private delegate void RGSSInitialize3();
        private delegate int RGSSEval(string text);
        private static IntPtr dllHandle;

        //---

        [DllImport("ntdll.dll", SetLastError = true)]
        static extern int NtQueryInformationProcess(
            IntPtr processHandle,
            int processInformationClass,
            IntPtr processInformation,
            uint processInformationLength,
            IntPtr returnLength
        );

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr ExitStatus;
            public IntPtr PebBaseAddress;
            public IntPtr AffinityMask;
            public IntPtr BasePriority;
            public UIntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;

            public static int Size {
                get { return Marshal.SizeOf(typeof(PROCESS_BASIC_INFORMATION)); }
            }
        }

        [Flags]
        public enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(
            ProcessAccessFlags processAccess,
            bool bInheritHandle,
            int processId
        );

        public static IntPtr OpenProcess(Process proc, ProcessAccessFlags flags) {
            return OpenProcess(flags, true, proc.Id);
        }

        private enum PROCESSINFOCLASS : int
        {
            ProcessBasicInformation = 0
        };

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hHandle);

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct PEB
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2 + 1 + 1 + 4 * 2 + 4)]
            public byte[] Unused1;
            public IntPtr ProcessParameters;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 104 + 4 * 52 + 1 + 128 + 4 * 1 + 4)]
            public byte[] Unused2;

            public static int Size {
                get { return Marshal.SizeOf(typeof(PEB)); }
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct UNICODE_STRING : IDisposable
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;

            public UNICODE_STRING(string s) {
                Length = (ushort)(s.Length * 2);
                MaximumLength = (ushort)(Length + 2);
                Buffer = Marshal.StringToHGlobalUni(s);
            }

            public void Dispose() {
                Marshal.FreeHGlobal(Buffer);
                Buffer = IntPtr.Zero;
            }

            public override string ToString() {
                return Marshal.PtrToStringUni(Buffer);
            }

            public static int Size {
                get { return Marshal.SizeOf(typeof(UNICODE_STRING)); }
            }
        }


        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct RTL_USER_PROCESS_PARAMETERS
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9 * 4)]
            public byte[] Unused1;
            public UNICODE_STRING CurrentDirectoryPath;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7 * 4)]
            public byte[] Unused2;

            public static int Size {
                get { return Marshal.SizeOf(typeof(RTL_USER_PROCESS_PARAMETERS)); }
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(
           IntPtr hProcess,
           IntPtr lpBaseAddress,
           IntPtr lpBuffer,
           int nSize,
           IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        static extern void RtlZeroMemory(IntPtr dst, int length);

        public static string[] ImageNames = { "RPGVXAce", "RPGVX", "RPGXP" };

        public static void Main(string[] args) {
            foreach (string image in ImageNames) {
                TestGetProcessWorkingDirectory(image);
            }
        }

        private static void TestGetProcessWorkingDirectory(string image) {
            Process[] ps = Process.GetProcessesByName(image);
            foreach (Process p in ps) {
                Console.WriteLine($"{p.ProcessName} pid: {p.Id}");

                IntPtr handle = OpenProcess(p, ProcessAccessFlags.All);
                IntPtr pPbi = Marshal.AllocHGlobal(PROCESS_BASIC_INFORMATION.Size);
                IntPtr outLong = Marshal.AllocHGlobal(sizeof(long));

                Console.WriteLine($"handle: {handle}");

                int ret = NtQueryInformationProcess(handle, 0,
                    pPbi, (uint)PROCESS_BASIC_INFORMATION.Size, outLong);

                if (ret != 0) {
                    Console.WriteLine($"Error {ret}");
                    break;
                }

                PROCESS_BASIC_INFORMATION pbi = Marshal.PtrToStructure<PROCESS_BASIC_INFORMATION>(pPbi);
                Console.WriteLine($"peb: {pbi.PebBaseAddress}");

                IntPtr pPeb = Marshal.AllocHGlobal(PEB.Size);
                ReadProcessMemory(handle, pbi.PebBaseAddress, pPeb, PEB.Size, outLong);
                PEB peb = Marshal.PtrToStructure<PEB>(pPeb);

                IntPtr pUpp = Marshal.AllocHGlobal(RTL_USER_PROCESS_PARAMETERS.Size);
                ReadProcessMemory(handle, peb.ProcessParameters, pUpp, RTL_USER_PROCESS_PARAMETERS.Size, outLong);
                RTL_USER_PROCESS_PARAMETERS upp = Marshal.PtrToStructure<RTL_USER_PROCESS_PARAMETERS>(pUpp);

                IntPtr pStr = Marshal.AllocHGlobal(upp.CurrentDirectoryPath.Length + 2);
                RtlZeroMemory(pStr, upp.CurrentDirectoryPath.Length + 2);
                ReadProcessMemory(handle, upp.CurrentDirectoryPath.Buffer, pStr, upp.CurrentDirectoryPath.Length, outLong);
                string cwd = Marshal.PtrToStringUni(pStr);
                Console.WriteLine($"cwd: {cwd}");

                CloseHandle(handle);
                Console.WriteLine();
            }
        }

        static void TestDynamicDllImport() {
            dllHandle = LoadLibrary("RGSS301.dll");
            Console.WriteLine("Loaded library");

            if (dllHandle == IntPtr.Zero) return;
            IntPtr addr = GetProcAddress(dllHandle, "RGSSInitialize3");
            Console.WriteLine($"RGSSInitialize3 Address {addr}");

            if (addr == IntPtr.Zero) return;
            RGSSInitialize3 init = (RGSSInitialize3)Marshal.GetDelegateForFunctionPointer(addr, typeof(RGSSInitialize3));
            init();

            IntPtr addr2 = GetProcAddress(dllHandle, "RGSSEval");
            Console.WriteLine($"RGSSEval Address {addr2}");

            if (addr2 == IntPtr.Zero) return;
            RGSSEval eval = (RGSSEval)Marshal.GetDelegateForFunctionPointer(addr2, typeof(RGSSEval));
            int ret = eval("p 3 + 5");

            Console.WriteLine(ret);
        }
    }
}
