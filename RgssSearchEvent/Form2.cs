using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RgssSearchEvent
{
    public partial class Form2 : Form
    {
        public int Switch;

        public Form2() {
            InitializeComponent();
        }

        public Form1 MainForm;

        public Form2(Form1 form1) {
            MainForm = form1;
            InitializeComponent();
        }

        private void Form2_Load(object sender, EventArgs e) {
            if (MainForm == null) return;
            if (MainForm.Instance == null) return;
            int max = 0;
            foreach (KeyValuePair<int, string> kv in MainForm.Instance.Switches) {
                if (max < kv.Key) max = kv.Key;
            }
            for(int i = 1; i <= max; ++i) {
                if (MainForm.Instance.Switches.TryGetValue(i, out string Name)) {
                    listBox2.Items.Add($"{i:D4}:{Name}");
                } else {
                    listBox2.Items.Add($"{i:D4}");
                }
            }
            if (MainForm.Switch > 0 && MainForm.Switch <= max)
                listBox2.SelectedIndex = MainForm.Switch - 1;
        }

        private void ListBox2_SelectedIndexChanged(object sender, EventArgs e) {
            Switch = listBox2.SelectedIndex + 1;
        }
    }
}
