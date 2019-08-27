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

            var list = File.ReadAllLines(@"C:\Users\admin\source\repos\urdu tester\urdu tester\bin\Debug\final.txt");
            hashSet = new HashSet<string>(list);
            installFont.RegisterFont("E:\\Brushield italic.ttf");
            Narrator.Initialize();
            // Narrator.Narrate("ababc");

        }

        private void button1_Click(object sender, EventArgs e)
        {
            //orignalText = richTextBox1.Text;
            UrduTranslator ur = new UrduTranslator();
            richTextBox2.Text = ur.ReplaceWithStringBuilder(richTextBox1.Text);
            //  richTextBox2.Text = slpittext(richTextBox1.Text, 40);
            richTextBox2.Font = new Font("SimBraille", 18);
            //MainScreen mn = new MainScreen();
            //mn.Show();
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton2.Checked == true)

            {
                richTextBox1.Font = new Font("Consolas", 16);
            }
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton1.Checked == true)
            {
                UrduTranslator ur = new UrduTranslator();
                richTextBox2.Text = ur.ReplaceWithStringBuilder(richTextBox1.Text);
              //  richTextBox2.Text = slpittext(richTextBox1.Text, 40);
                richTextBox2.Font = new Font("SimBraille", 18);
            }
           
        }

        UrduKeyboard UrduKeyboard = new UrduKeyboard();
       
        private void richTextBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (ModifierKeys == Keys.Modifiers)
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
                Narrator.Narrate(Text);

            }

            if (e.KeyCode == Keys.Space)
            {


                //int lastSpace = richTextBox1.Text.LastIndexOf(' ', richTextBox1.SelectionStart - 1);
                //int secondSpace = richTextBox1.Text.IndexOf(' ', richTextBox1.SelectionStart + 1);
                //string lastWord = richTextBox1.Text.Substring(lastSpace + 1, secondSpace - 1 - lastSpace);
                ////label1.Focus();
                ////richTextBox1.Select(lastSpace + 1, richTextBox1.SelectionStart);
                ////richTextBox1.SelectionColor = Color.Red;
                ////richTextBox1.Select(richTextBox1.SelectionStart + richTextBox1.SelectionLength+1, 0);



                //if (!hashSet.Contains(lastWord))
                //{
                //    Player.URL = "error.mp3";
                //    Player.controls.play();


                //}
                //else
                //{
                //    Player.URL = "space.mp3";
                //    Player.controls.play();

                   
                //}
            }

            else if (e.KeyCode == Keys.Back)
            {
                Narrator.Narrate(richTextBox1.Text.Substring(richTextBox1.SelectionStart - 1, 1));
             
            }


        }

        private void richTextBox1_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left | e.KeyCode == Keys.Right)
            {
                alphateFeedback();
            }

           

        }


        private void menuStrip1_KeyPress(object sender, KeyPressEventArgs e)
        {
            MessageBox.Show(((ToolStripMenuItem)sender).Text);

        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {

            richTextBox2.Text = richTextBox1.Rtf;
          
            int lastSpace = richTextBox1.Text.LastIndexOf(' ', richTextBox1.SelectionStart - 1);

            if (lastSpace == -1)
                lastSpace = 0;

            int secondSpace = richTextBox1.Text.IndexOf(' ', richTextBox1.SelectionStart);

            if (secondSpace == -1)
                secondSpace = richTextBox1.SelectionStart;

            string lastWord = richTextBox1.Text.Substring(lastSpace+1, secondSpace - lastSpace-1);
            Text = lastWord;

            //richTextBox1.Select(lastSpace, secondSpace - lastSpace);
            //richTextBox1.SelectionColor = Color.Red;
            //richTextBox1.Select(richTextBox1.SelectionStart + richTextBox1.SelectionLength + 1, 0);


        }

        private void button3_Click(object sender, EventArgs e)
        {
            MainScreen mainScreen = new MainScreen();
            mainScreen.Show();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            richTextBox1.SelectionAlignment = HorizontalAlignment.Center;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            richTextBox2.Text = richTextBox1.Rtf; 
        }

        private void button6_Click(object sender, EventArgs e)
        {
            richTextBox1.Rtf = richTextBox2.Text;
        }

        private void button7_Click(object sender, EventArgs e)
        {
            Narrator.Narrate(richTextBox2.Text);
        }

        private void button8_Click(object sender, EventArgs e)
        {
            Narrator.Beep();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            richTextBox1.Lines[2] = "ASDasd";
            richTextBox1.Refresh();
            //richTextBox1.Select(richTextBox1.SelectionStart, 0);
            //richTextBox1.Rtf = richTextBox2.Text;
        }
    }
}
