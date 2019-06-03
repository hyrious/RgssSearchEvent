using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RgssSearchEvent
{
    public partial class Form1 : Form
    {
        public Form1() {
            InitializeComponent();
        }

        public RGSS Instance = null;

        private void RadioButton1_CheckedChanged(object sender, EventArgs e) {
            button1.Enabled = true;
            button2.Enabled = false;
            textBox1.Enabled = false;
        }

        public int Switch = 0, Variable = 0;
        public string EventName = String.Empty;

        private void Button1_Click(object sender, EventArgs e) {
            if (Instance == null) return;
            using (Form2 FormSelectSwitch = new Form2(this)) {
                if (FormSelectSwitch.ShowDialog() == DialogResult.OK) {
                    SetSwitch(FormSelectSwitch.Switch);
                }
            }
        }

        private void SetSwitch(int value) {
            if (value == 0) return;
            Switch = value;
            if (Instance.Switches.TryGetValue(Switch, out string Name)) {
                button1.Text = $"{Switch:D4}:{Name}";
            } else {
                button1.Text = $"{Switch:D4}";
            }
        }

        private void RadioButton2_CheckedChanged(object sender, EventArgs e) {
            button1.Enabled = false;
            button2.Enabled = true;
            textBox1.Enabled = false;
        }

        private void Button2_Click(object sender, EventArgs e) {
            if (Instance == null) return;
            using (Form3 FormSelectVar = new Form3(this)) {
                if (FormSelectVar.ShowDialog() == DialogResult.OK) {
                    SetVariable(FormSelectVar.Variable);
                }
            }
        }

        private void SetVariable(int value) {
            if (value == 0) return;
            Variable = value;
            if (Instance.Variables.TryGetValue(Variable, out string Name)) {
                button2.Text = $"{Variable:D4}:{Name}";
            } else {
                button2.Text = $"{Variable:D4}";
            }
        }

        private void RadioButton3_CheckedChanged(object sender, EventArgs e) {
            button1.Enabled = false;
            button2.Enabled = false;
            textBox1.Enabled = true;
        }

        private void Button3_Click(object sender, EventArgs e) {
            if (Instance == null) return;
            listView1.Items.Clear();
            if (button1.Enabled) {
                foreach (RGSS.EventPage page in Instance.EventPages) {
                    if (page.Switches.Contains(Switch)) {
                        ListViewItem item = new ListViewItem(Instance.GetMapDisplayText(page));
                        item.SubItems.Add(Instance.GetEventDisplayText(page));
                        item.SubItems.Add(Instance.GetPageDisplayText(page));
                        item.SubItems.Add(Instance.GetLocationDisplayText(page));
                        listView1.Items.Add(item);
                    }
                }
            } else if (button2.Enabled) {
                foreach (RGSS.EventPage page in Instance.EventPages) {
                    if (page.Variables.Contains(Variable)) {
                        ListViewItem item = new ListViewItem(Instance.GetMapDisplayText(page));
                        item.SubItems.Add(Instance.GetEventDisplayText(page));
                        item.SubItems.Add(Instance.GetPageDisplayText(page));
                        item.SubItems.Add(Instance.GetLocationDisplayText(page));
                        listView1.Items.Add(item);
                    }
                }
            } else if (textBox1.Enabled) {
                foreach (RGSS.EventPage page in Instance.EventPages) {
                    if (page.Name.Contains(EventName)) {
                        ListViewItem item = new ListViewItem(Instance.GetMapDisplayText(page));
                        item.SubItems.Add(Instance.GetEventDisplayText(page));
                        item.SubItems.Add(Instance.GetPageDisplayText(page));
                        item.SubItems.Add(Instance.GetLocationDisplayText(page));
                        listView1.Items.Add(item);
                    }
                }
            }
        }

        private void Button4_Click(object sender, EventArgs e) {
            contextMenuStrip1.SuspendLayout();
            contextMenuStrip1.Items.Clear();
            RefreshPossibleProjects();
            foreach (string path in PossibleProjects) {
                ToolStripItem item = contextMenuStrip1.Items.Add(path);
                if (path != "-") item.Click += SelectPossible;
            }
            contextMenuStrip1.Items.Add("手动选择").Click += SelectManually;
            contextMenuStrip1.ResumeLayout();
            contextMenuStrip1.Show(button4, new Point(0, button4.Height));
        }

        private void SelectPossible(object sender, EventArgs e) {
            ToolStripItem item = (ToolStripItem)sender;
            ChangeProject(item.Text);
        }

        private void SelectManually(object sender, EventArgs e) {
            DialogResult result = folderBrowserDialog1.ShowDialog();
            if (result == DialogResult.OK) ChangeProject(folderBrowserDialog1.SelectedPath);
        }

        private void ChangeProject(string path) {
            if (!RGSS.IsValidProject(path)) {
                Instance = null;
                RGSS.ShowReason();
                return;
            }
            Instance = new RGSS(path);
            Text = $"{OriginalText} - {Path.GetFileNameWithoutExtension(Instance.ProjectPath)}";
            listView1.Items.Clear();
            foreach (RGSS.EventPage page in Instance.EventPages) {
                ListViewItem item = new ListViewItem(Instance.GetMapDisplayText(page));
                item.SubItems.Add(Instance.GetEventDisplayText(page));
                item.SubItems.Add(Instance.GetPageDisplayText(page));
                item.SubItems.Add(Instance.GetLocationDisplayText(page));
                listView1.Items.Add(item);
            }
            if (Instance.Switches.ContainsKey(1))
                SetSwitch(1);
            if (Instance.Variables.ContainsKey(1))
                SetVariable(1);
        }

        string OriginalText = String.Empty;

        private void Form1_Load(object sender, EventArgs e) {
            OriginalText = Text;
            RefreshPossibleProjects();
            if (PossibleProjects.Count > 0) {
                ChangeProject(PossibleProjects[0]);
            }
        }

        private List<string> PossibleProjects = new List<string>();
        private static readonly string[] ImageNames = { "RPGVXAce", "RPGXP", "RPGVX" };

        private void RefreshPossibleProjects() {
            PossibleProjects.Clear();
            bool changed = false;
            changed |= TryAddProject(Environment.CurrentDirectory);
            if (changed) PossibleProjects.Add("-");
            changed = false;
            foreach (string image in ImageNames) {
                foreach (string path in GetProcessWorkingDirectoriesByImageName(image)) {
                    changed |= TryAddProject(path);
                }
            }
            if (changed) PossibleProjects.Add("-");
            // TODO: default project folders in registry
        }

        private bool TryAddProject(string path) {
            if (path.EndsWith(@"\")) {
                path = path.Substring(0, path.Length - 1);
            }
            if (RGSS.IsValidProject(path) && !PossibleProjects.Contains(path)) {
                PossibleProjects.Add(path);
                return true;
            }
            return false;
        }

        #region GetCwdFromPid

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtQueryInformationProcess(
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

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(
            int processAccess,
            bool bInheritHandle,
            int processId
        );

        private static IntPtr OpenProcess(Process proc) {
            return OpenProcess(0x001F0FFF, false, proc.Id);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hHandle);

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
        private struct UNICODE_STRING : IDisposable
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
        private static extern bool ReadProcessMemory(
           IntPtr hProcess,
           IntPtr lpBaseAddress,
           IntPtr lpBuffer,
           int nSize,
           IntPtr lpNumberOfBytesRead
        );

        [DllImport("kernel32.dll")]
        private static extern void RtlZeroMemory(IntPtr dst, int length);

        private void TextBox1_TextChanged(object sender, EventArgs e) {
            EventName = textBox1.Text;
        }

        private static List<string> GetProcessWorkingDirectoriesByImageName(string image) {
            List<string> result = new List<string>();
            Process[] ps = Process.GetProcessesByName(image);
            foreach (Process p in ps) {
                IntPtr handle = OpenProcess(p);
                IntPtr pPbi = Marshal.AllocHGlobal(PROCESS_BASIC_INFORMATION.Size);
                IntPtr outLong = Marshal.AllocHGlobal(sizeof(long));

                int ret = NtQueryInformationProcess(handle, 0,
                    pPbi, (uint)PROCESS_BASIC_INFORMATION.Size, outLong);

                if (ret != 0) continue;

                PROCESS_BASIC_INFORMATION pbi = Marshal.PtrToStructure<PROCESS_BASIC_INFORMATION>(pPbi);

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
                if (!String.IsNullOrEmpty(cwd)) result.Add(cwd);

                CloseHandle(handle);
            }
            return result;
        }

        #endregion
    }
}
