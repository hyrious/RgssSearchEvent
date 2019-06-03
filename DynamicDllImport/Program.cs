using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

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
                foreach (string path in TestGetProcessWorkingDirectory(image)) {
                    TestDynamicDllImport(path);
                }
            }
        }

        private static string[] TestGetProcessWorkingDirectory(string image) {
            List<string> ret = new List<string>();
            Process[] ps = Process.GetProcessesByName(image);
            foreach (Process p in ps) {
                Console.WriteLine($"{p.ProcessName} pid: {p.Id}");

                IntPtr handle = OpenProcess(p, ProcessAccessFlags.All);
                IntPtr pPbi = Marshal.AllocHGlobal(PROCESS_BASIC_INFORMATION.Size);
                IntPtr outLong = Marshal.AllocHGlobal(sizeof(long));

                Console.WriteLine($"handle: {handle}");

                int _ = NtQueryInformationProcess(handle, 0,
                    pPbi, (uint)PROCESS_BASIC_INFORMATION.Size, outLong);

                if (_ != 0) {
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
                if (!String.IsNullOrEmpty(cwd)) {
                    ret.Add(cwd);
                }
                Console.WriteLine($"cwd: {cwd}");

                CloseHandle(handle);
                Console.WriteLine();
            }
            return ret.ToArray();
        }

        static void TestDynamicDllImport(string path) {
            Directory.SetCurrentDirectory(path);

            dllHandle = LoadLibrary(@"System\RGSS301.dll");
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
            int ret = eval(@"# encoding: utf-8

F = 'rgss_search_events.txt'

version = 0
version = 1 if !Dir.glob('*.rxproj').empty?
version = 2 if !Dir.glob('*.rvproj').empty?
version = 3 if !Dir.glob('*.rvproj2').empty?

V = version

def encode(str)
  [str].pack('m0')
end

class Mat
  def initialize v, n
    @v = v
    @n = n
  end
  def === n
    return false unless @v == V
    @n === n
  end
end

def xp(n)
  Mat.new 1, n
end

def vx(n)
  Mat.new 2, n
end

def va(n)
  Mat.new 3, n
end

if version == 0
  out = ['!', encode('invalid project: not found *.rvproj2')].join(' ')
  File.open(file, 'w') { |f| f.puts out }
else
  out = []; mapids = []
  ext = [nil, 'rxdata', 'rvdata', 'rvdata2'][version]
  if File.exist? ""Data/MapInfos.#{ext}""
    mapinfos = load_data ""Data/MapInfos.#{ext}""
    mapinfos.each do |i, v|
      mapids << i
      out << ['M', i, encode(v.name)].join(' ')
    end
  end
  if File.exist? ""Data/System.#{ext}""
    systems = load_data ""Data/System.#{ext}""
    systems.switches.each_with_index do |s, i|
      next if s == nil || s == ''
      out << ['S', i, encode(s)].join(' ')
    end
    systems.variables.each_with_index do |s, i|
      next if s == nil || s == ''
      out << ['V', i, encode(s)].join(' ')
    end
  end
  mapids.each do |mapid|
    file = ""Data/Map%03d.#{ext}"" % mapid
    next if !File.exist?(file)
    map = load_data file
    map.events.each_value do |e|
      e.pages.each_with_index do |page, pageid|
        s = []; v = []
        if page.condition.switch1_valid
          s << page.condition.switch1_id
        end
        if page.condition.switch2_valid
          s << page.condition.switch2_id
        end
        if page.condition.variable_valid
          v << page.condition.variable_id
        end
        page.list.each do |command|
          params = command.parameters
          case command.code
          when 111 # if
            case params[0]
            when 0 # switch
              s << params[1]
            when 1 # variable
              v << params[1]
              if params[2] != 0
                v << params[3]
              end
            end
          when 121 # switch =
            (params[0]..params[1]).each do |i|
              s << i
            end
          when 122 # var =
            if params[3] == 1
              v << params[4]
            end
            (params[0]..params[1]).each do |i|
              v << i
            end
          when 201, vx(202), va(202) # transfer
            if params[0] != 0
              v << params[1]
              v << params[2]
              v << params[3]
            end
          when xp(202), vx(203), va(203) # move event
            if params[1] == 1
              v << params[2]
              v << params[3]
            end
          when 231 # show pic
            if params[3] != 0
              v << params[4]
              v << params[5]
            end
          when 232 # move pic
            if params[3] != 0
              v << params[4]
              v << params[5]
            end
          when va(285) # get location info
            if params[2] != 0
              v << params[3]
              v << params[4]
            end
            v << params[0]
          when 301 # battle
            if @params[0] == 1
              v << @params[1]
            end
          end
        end
        s = s.uniq
        s = s.empty? ? '0' : encode(s.join(' '))
        v = v.uniq
        v = v.empty? ? '0' : encode(v.join(' '))
        name = e.name.empty? ? '0' : encode(e.name)
        out << [mapid, e.id, pageid, e.x, e.y, s, v, name].join(' ')
      end
    end
  end
  File.open(F, 'w') { |f| f.puts out }
end
");

            Console.WriteLine(ret);
        }
    }
}
