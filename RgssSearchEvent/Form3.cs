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
    public partial class Form3 : Form
    {
        public int Variable;

        private Form1 MainForm;

        public Form3() {
            InitializeComponent();
        }

        public Form3(Form1 form1) {
            MainForm = form1;
            InitializeComponent();
        }

        private void Form3_Load(object sender, EventArgs e) {
            if (MainForm == null) return;
            if (MainForm.Instance == null) return;
            int max = 0;
            foreach (KeyValuePair<int, string> kv in MainForm.Instance.Variables) {
                if (max < kv.Key) max = kv.Key;
            }
            for (int i = 1; i <= max; ++i) {
                if (MainForm.Instance.Variables.TryGetValue(i, out string Name)) {
                    listBox1.Items.Add($"{i:D4}:{Name}");
                } else {
                    listBox1.Items.Add($"{i:D4}");
                }
            }
            if (MainForm.Variable > 0 && MainForm.Variable <= max)
                listBox1.SelectedIndex = MainForm.Variable - 1;
        }

        private void ListBox1_SelectedIndexChanged(object sender, EventArgs e) {
            Variable = listBox1.SelectedIndex + 1;
        }
    }
}
