using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio;
using NAudio.Wave;
using System.Speech.Synthesis;
using WMPLib;

namespace BrailleUrdu
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        SpeechSynthesizer synthesizer = new SpeechSynthesizer();
        HashSet<string> hashSet = new HashSet<string>();

        private void Form1_Load(object sender, EventArgs e)
        {
            UrduKeyboard ur = new UrduKeyboard();

            var list = File.ReadAllLines(@"final.txt");
            hashSet = new HashSet<string>(list);
            installFont.RegisterFont("E:\\Brushield italic.ttf");
            Narrator.Initialize();
            // Narrator.Narrate("ababc");

        }

        UrduKeyboard UrduKeyboard = new UrduKeyboard();
       
        private void richTextBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (ModifierKeys == Keys.Control)
                return;

            richTextBox1.SelectedText =  UrduKeyboard.ConvertKey(e.KeyChar).ToString();
            e.Handled = UrduKeyboard.isKeyFound;
         
        }

        void alphateFeedback()
        {
            try
            {
                string alphabate = richTextBox1.Text.Substring(richTextBox1.SelectionStart, 1);
                Narrator.Narrate(alphabate);
            }
            catch (Exception)
            {
            }
           
           
        }

      

        private void richTextBox1_KeyDown(object sender, KeyEventArgs e)
        {
             if (e.KeyCode == Keys.Down)
            {
                e.SuppressKeyPress = true;
                int lastSpace = richTextBox1.Text.LastIndexOf(' ', richTextBox1.SelectionStart - 1);
                int secondSpace = richTextBox1.SelectionStart;
                string lastWord = richTextBox1.Text.Substring(lastSpace + 1, secondSpace - 1 - lastSpace);

                Narrator.Narrate(lastWord);

            }

            if (e.KeyCode == Keys.Space)
            {


                int lastSpace = richTextBox1.Text.LastIndexOf(' ', richTextBox1.SelectionStart - 1);
                int secondSpace = richTextBox1.SelectionStart;
                string lastWord = richTextBox1.Text.Substring(lastSpace + 1, secondSpace - 1 - lastSpace);
                label1.Focus();
                richTextBox1.Select(lastSpace + 1, richTextBox1.SelectionStart);          

                if (!hashSet.Contains(lastWord))
                {
                    Narrator.Beep();
                    richTextBox1.SelectionColor = Color.Red;
                }
                else
                {
                    Narrator.Narrate(" ");
                    richTextBox1.SelectionColor = Color.Black;
                }

                richTextBox1.Select(richTextBox1.SelectionStart + richTextBox1.SelectionLength + 1, 0);
                richTextBox1.Focus();


            }

            else if (e.KeyCode == Keys.Back)
            {
                try
                {
                    Narrator.Narrate(richTextBox1.Text.Substring(richTextBox1.SelectionStart - 1, 1));
                }
                catch (Exception)
                {
                }           
             
            }

        }

        private void richTextBox1_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left | e.KeyCode == Keys.Right)
            {
                alphateFeedback();
            }          

        }



        private void fstBox_PaintLine(object sender, FastColoredTextBoxNS.PaintLineEventArgs e)
        {
            if (e.LineIndex % 25 == 0)
            {
                e.Graphics.DrawLine(new Pen(Color.Black), e.LineRect.X, e.LineRect.Y, 600, e.LineRect.Y);
            }
        }

        private void translateToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            UrduTranslator ur = new UrduTranslator();
            Formater formater = new Formater();
            fstBox.Text = formater.Format(ur.Transat(richTextBox1.Text), 40, 25);
            fstBox.Font = new Font("SimBraille", 18);
        }

        private void fstBox_SelectionChanged_1(object sender, EventArgs e)
        {
            UrduTranslator urdu = new UrduTranslator();
            label1.Text = urdu.RTransat(fstBox.Lines[fstBox.Selection.Start.iLine]);
        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
