using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RgssSearchEvent
{
    public enum RGSS_VERSION : int
    {
        XP = 1,
        VX = 2,
        VA = 3,
        Error = 233
    }

    public static class ErrorReason
    {
        public const string NoError = "没有错误";
        public const string NotDirectory = "不是文件夹";
        public const string NotFoundProj = "没有找到 proj 文件";
        public const string NotFoundIni = "没有找到 ini 文件";
        public const string NotFoundDll = "没有找到 dll 文件";
        public const string ErrorLoadDll = "载入 dll 失败";
        public const string RGSSError = "执行 RGSS 代码失败";
        public const string NotFoundData = "没有找到数据文件";

        public static string GetDetails(string Reason) {
            switch (Reason) {
                case NotDirectory:
                    return "请选择文件夹而不是文件，再试一次";
                case NotFoundProj:
                    return "请确保是未加密的工程目录";
                case NotFoundIni:
                    return "请确保是未加密的工程目录";
                case NotFoundDll:
                    return "请将 dll 文件放入 ini 文件指定的位置，再试一次";
                case ErrorLoadDll:
                    return $"错误代码 {Marshal.GetLastWin32Error()}";
                case NotFoundData:
                    return "请确保是未加密的工程目录";
                case RGSSError:
                    return "你可能修改了数据库格式，你可以在工程目录放一个 rgss_search_event.rb 来代替现脚本";
                default:
                    return "错误：没有错误";
            }
        }
    }

    public class RGSS
    {
        private const string Text = @"# encoding: utf-8

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
  File.open(F, 'w') { |f| f.puts out }
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
            if params[0] == 1
              v << params[1]
            end
          end
        end
        s = s.uniq
        s = s.empty? ? '0' : encode(s.join(' '))
        v = v.uniq
        v = v.empty? ? '0' : encode(v.join(' '))
        name = e.name.empty? ? '0' : encode(e.name)
        out << ['E', mapid, e.id, pageid, e.x, e.y, s, v, name].join(' ')
      end
    end
  end
  File.open(F, 'w') { |f| f.puts out }
end
";
        public static RGSS_VERSION LastRgssVersion = RGSS_VERSION.Error;
        public static string Reason = ErrorReason.NoError;

        public static void ShowReason() {
            MessageBox.Show(ErrorReason.GetDetails(Reason), Reason);
        }

        public static Dictionary<string, RGSS_VERSION> ProjectExtensions = new Dictionary<string, RGSS_VERSION> {
            ["Game.rvproj2"] = RGSS_VERSION.VA,
            ["Game.rxproj"] = RGSS_VERSION.XP,
            ["Game.rvproj"] = RGSS_VERSION.VX
        };

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileString(
            string Section, string Key, string Default, StringBuilder RetVal, int Size, string FilePath);

        private static string ReadIni(string Section, string Key, string FilePath = "./Game.ini", int Size = 256) {
            StringBuilder temp = new StringBuilder(Size);
            GetPrivateProfileString(Section, Key, "", temp, Size, FilePath);
            return temp.ToString();
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string name);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string name);

        private delegate void RGSSInitialize();
        private delegate void RGSSFinalize();
        private delegate int RGSSEval(string text);

        public static bool IsValidProject(string path) {
            Reason = ErrorReason.NoError;
            LastRgssVersion = RGSS_VERSION.Error;
            if (!Directory.Exists(path)) {
                Reason = ErrorReason.NotDirectory;
                return false;
            }
            string AppName = GetAppName(path);
            if (LastRgssVersion == RGSS_VERSION.Error) {
                Reason = ErrorReason.NotFoundProj;
                return false;
            }
            string IniFilePath = Path.Combine(path, $"{AppName}.ini");
            if (!File.Exists(IniFilePath)) {
                Reason = ErrorReason.NotFoundIni;
                LastRgssVersion = RGSS_VERSION.Error;
                return false;
            }
            string DllFile = ReadIni("Game", "Library", IniFilePath);
            string DllFilePath = Path.Combine(path, DllFile);
            if (!File.Exists(DllFilePath)) {
                Reason = ErrorReason.NotFoundDll;
                LastRgssVersion = RGSS_VERSION.Error;
                return false;
            }
            IntPtr DllHandle = LoadLibrary(DllFilePath);
            if (DllHandle == IntPtr.Zero) {
                Reason = ErrorReason.ErrorLoadDll;
                LastRgssVersion = RGSS_VERSION.Error;
                return false;
            }
            IntPtr addr = GetProcAddress(DllHandle, GetInitFuncName(LastRgssVersion));
            if (addr == IntPtr.Zero) {
                Reason = ErrorReason.ErrorLoadDll;
                LastRgssVersion = RGSS_VERSION.Error;
                DllHandle = IntPtr.Zero;
                return false;
            }
            string DataFolder = Path.Combine(path, "Data");
            string MapInfosFile = Path.Combine(DataFolder, $"MapInfos.{GetDataFileExtension(LastRgssVersion)}");
            string SystemFile = Path.Combine(DataFolder, $"System.{GetDataFileExtension(LastRgssVersion)}");
            if (!(File.Exists(MapInfosFile) && File.Exists(SystemFile))) {
                Reason = ErrorReason.NotFoundData;
                LastRgssVersion = RGSS_VERSION.Error;
                return false;
            }
            return true;
        }

        private static string GetAppName(string path) {
            foreach (KeyValuePair<string, RGSS_VERSION> kv in ProjectExtensions) {
                if (File.Exists(Path.Combine(path, kv.Key))) {
                    LastRgssVersion = kv.Value;
                    return Path.GetFileNameWithoutExtension(kv.Key);
                }
            }
            LastRgssVersion = RGSS_VERSION.Error;
            return String.Empty;
        }

        private static string GetInitFuncName(RGSS_VERSION Version) {
            switch (Version) {
                case RGSS_VERSION.VA:
                    return "RGSSInitialize3";
                case RGSS_VERSION.VX:
                    return "RGSSInitialize2";
                default:
                    return "RGSSInitialize";
            }
        }

        private static string GetDataFileExtension(RGSS_VERSION Version) {
            switch (Version) {
                case RGSS_VERSION.VA:
                    return "rvdata2";
                case RGSS_VERSION.VX:
                    return "rvdata";
                default:
                    return "rxdata";
            }
        }

        public string ProjectPath;
        public RGSS_VERSION Version;
        private IntPtr hModule = IntPtr.Zero;
        public Dictionary<int, string> MapNames = new Dictionary<int, string>();
        public Dictionary<int, string> Switches = new Dictionary<int, string>();
        public Dictionary<int, string> Variables = new Dictionary<int, string>();

        public struct EventPage
        {
            public int MapId, EventId, PageNumber, X, Y;
            public int[] Switches, Variables;
            public string Name;
        }

        public string GetMapDisplayText(EventPage page) {
            if (MapNames.TryGetValue(page.MapId, out string MapName)) {
                return $"{page.MapId:D3}:{MapName}";
            } else {
                return $"{page.MapId:D3}:";
            }
        }

        public string GetEventDisplayText(EventPage page) => $"{page.EventId:D3}:{page.Name}";

        public string GetPageDisplayText(EventPage page) => page.PageNumber.ToString();

        public string GetLocationDisplayText(EventPage page) => $"({page.X},{page.Y})";

        public List<EventPage> EventPages = new List<EventPage>();

        public RGSS(string path) {
            ProjectPath = path;
            Version = LastRgssVersion;
            Refresh();
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        public void Refresh() {
            string AppName = GetAppName(ProjectPath);
            Directory.SetCurrentDirectory(ProjectPath);

            string DllFile = ReadIni("Game", "Library");
            if (hModule == IntPtr.Zero) hModule = LoadLibrary(DllFile);
            IntPtr addrInit = GetProcAddress(hModule, GetInitFuncName(Version));
            RGSSInitialize Init = (RGSSInitialize)Marshal.GetDelegateForFunctionPointer(addrInit, typeof(RGSSInitialize));
            Init();

            IntPtr addrEval = GetProcAddress(hModule, "RGSSEval");
            RGSSEval Eval = (RGSSEval)Marshal.GetDelegateForFunctionPointer(addrEval, typeof(RGSSEval));

            if (File.Exists("rgss_search_event.rb")) {
                Eval(File.ReadAllText("rgss_search_event.rb"));
            } else if (Eval(Text) == 6) {
                Reason = ErrorReason.RGSSError;
                ShowReason();
            }

            IntPtr addrFin = GetProcAddress(hModule, "RGSSFinalize");
            RGSSFinalize Fin = (RGSSFinalize)Marshal.GetDelegateForFunctionPointer(addrFin, typeof(RGSSFinalize));
            Fin();

            EventPages.Clear();
            if (!File.Exists("rgss_search_events.txt")) return;
            foreach (string line in File.ReadLines("rgss_search_events.txt")) {
                string[] tokens = line.Split(null);
                if (tokens.Length == 0) continue;
                switch (tokens[0]) {
                    case "M": {
                        if (tokens.Length != 3) break;
                        if (Int32.TryParse(tokens[1], out int MapId)) {
                            try {
                                byte[] bytes = Convert.FromBase64String(tokens[2]);
                                string MapName = Encoding.UTF8.GetString(bytes);
                                MapNames.Add(MapId, MapName);
                            } catch { }
                        }
                        break;
                    }
                    case "S": {
                        if (tokens.Length != 3) break;
                        if (Int32.TryParse(tokens[1], out int SwitchId)) {
                            try {
                                byte[] bytes = Convert.FromBase64String(tokens[2]);
                                string SwitchName = Encoding.UTF8.GetString(bytes);
                                Switches.Add(SwitchId, SwitchName);
                            } catch { }
                        }
                        break;
                    }
                    case "V": {
                        if (tokens.Length != 3) break;
                        if (Int32.TryParse(tokens[1], out int VariableId)) {
                            try {
                                byte[] bytes = Convert.FromBase64String(tokens[2]);
                                string VariableName = Encoding.UTF8.GetString(bytes);
                                Variables.Add(VariableId, VariableName);
                            } catch { }
                        }
                        break;
                    }
                    case "E": {
                        if (tokens.Length != 9) break;
                        EventPage page = new EventPage();
                        if (!Int32.TryParse(tokens[1], out int MapId)) break;
                        page.MapId = MapId;
                        if (!Int32.TryParse(tokens[2], out int EventId)) break;
                        page.EventId = EventId;
                        if (!Int32.TryParse(tokens[3], out int PageNumber)) break;
                        page.PageNumber = PageNumber;
                        if (!Int32.TryParse(tokens[4], out int X)) break;
                        page.X = X;
                        if (!Int32.TryParse(tokens[5], out int Y)) break;
                        page.Y = Y;
                        try {
                            List<int> Switches = new List<int>();
                            byte[] bytes = Convert.FromBase64String(tokens[6]);
                            string switches = Encoding.UTF8.GetString(bytes);
                            foreach (string s in switches.Split(null)) {
                                if (!Int32.TryParse(s, out int SwitchId)) continue;
                                Switches.Add(SwitchId);
                            }
                            page.Switches = Switches.ToArray();
                        } catch {
                            page.Switches = new int[0];
                        }
                        try {
                            List<int> Variables = new List<int>();
                            byte[] bytes = Convert.FromBase64String(tokens[7]);
                            string variables = Encoding.UTF8.GetString(bytes);
                            foreach (string s in variables.Split(null)) {
                                if (!Int32.TryParse(s, out int VariableId)) continue;
                                Variables.Add(VariableId);
                            }
                            page.Variables = Variables.ToArray();
                        } catch {
                            page.Variables = new int[0];
                        }
                        try {
                            byte[] bytes = Convert.FromBase64String(tokens[8]);
                            string name = Encoding.UTF8.GetString(bytes);
                            page.Name = name;
                        } catch { }
                        EventPages.Add(page);
                        break;
                    }
                }
            }
        }
    }
}
