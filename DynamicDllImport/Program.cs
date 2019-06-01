using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DynamicDllImport {
    class Program {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string name);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string name);

        private delegate void RGSSInitialize3();
        private delegate int RGSSEval(string text);
        private static IntPtr dllHandle;

        static void Main(string[] args) {
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
