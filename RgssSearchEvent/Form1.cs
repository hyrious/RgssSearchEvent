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
    public partial class Form1 : Form
    {
        public Form1() {
            InitializeComponent();
        }

        private void RadioButton1_CheckedChanged(object sender, EventArgs e) {
            button1.Enabled = true;
            button2.Enabled = false;
            textBox1.Enabled = false;
        }

        private void Button1_Click(object sender, EventArgs e) {
        }

        private void RadioButton2_CheckedChanged(object sender, EventArgs e) {
            button1.Enabled = false;
            button2.Enabled = true;
            textBox1.Enabled = false;
        }

        private void Button2_Click(object sender, EventArgs e) {

        }

        private void RadioButton3_CheckedChanged(object sender, EventArgs e) {
            button1.Enabled = false;
            button2.Enabled = false;
            textBox1.Enabled = true;
        }

        private void TextBox1_TextChanged(object sender, EventArgs e) {

        }

        private void Button3_Click(object sender, EventArgs e) {

        }
    }
}
