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
       
        private string SpliceNoteText(string text, int maxWidth)
        {
            StringBuilder sb = new StringBuilder(text);

            for (int i = 0; i < (sb.Length / maxWidth); i++)
            {
                int insertPosition = i * maxWidth;
                sb.Insert(insertPosition, Environment.NewLine);
            }

            return sb.ToString();
        }

        string orignalText = "";

        private string slpittext(string inpt, int limit)
        {
            StringBuilder sb = new StringBuilder();
            int startPos=0, lastSpace = 0;
            for (int i = 0; i < inpt.Length; i++)
            {
                if (inpt[i] == '\n')
                {
                    sb.Append(inpt.Substring(startPos, i - startPos));
                    startPos = i;                   
                }
               else if (inpt[i] == ' ')
                    lastSpace = i+1;


                if (i - startPos > limit)
                {
                    sb.Append(inpt.Substring(startPos, lastSpace - startPos));
                    sb.Append(Environment.NewLine);
                    startPos = lastSpace;
                }
                //else if (i - lastSpace > limit)
                //{
                //    sb.Append(inpt.Substring(lastSpace, i - lastSpace));
                //    sb.Append(Environment.NewLine);
                //    startPos = i;
                //    lastSpace = i;
                //}

            }
            sb.Append(inpt.Substring(startPos, inpt.Length - startPos));
            return sb.ToString();

        } 

        private String splitLines(String tInp, int n)
        {
            String ln = "";
            StringBuilder tOut = new StringBuilder();
            int x = 0, y = 0;

            while (x < tInp.Length)
            {
                if (x + n < tInp.Length)
                    ln = tInp.Substring(x, n);
                else
                    ln = tInp.Substring(x, tInp.Length - x);

                y = ln.LastIndexOf(" ");
                y = y > 0 ? y : ln.Length;

                ln = ln.Substring(0, y);
                tOut.Append('\n'+ ln);
                x = x + ln.Length;
            };

            return tOut.ToString();
        }

        HashSet<string> hashSet = new HashSet<string>();

        private void Form1_Load(object sender, EventArgs e)
        {
            //UrduKeyboard ur = new UrduKeyboard(richTextBox1);
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
                richTextBox1.Text = orignalText;
                richTextBox1.Font = new Font("Consolas", 16);
            }
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton1.Checked == true)
            {

                orignalText = richTextBox1.Text;
                UrduTranslator ur = new UrduTranslator();
                richTextBox2.Text = ur.ReplaceWithStringBuilder(richTextBox1.Text);
              //  richTextBox2.Text = slpittext(richTextBox1.Text, 40);
                richTextBox2.Font = new Font("SimBraille", 18);
            }
           
        }

        private void richTextBox1_SelectionChanged(object sender, EventArgs e)
        {
            //if (radioButton1.Checked == true)
            //{
            //    // this.WordWrap = false;
            //    int cursorPosition = richTextBox1.SelectionStart;
            //    int lineIndex = richTextBox1.GetLineFromCharIndex(cursorPosition);
            //    label1.Text = richTextBox1.Lines[lineIndex];
            //    // this.WordWrap = true;

            //}

        }

        private void menuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }

        UrduKeyboard UrduKeyboard = new UrduKeyboard();
       
        private void richTextBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (ModifierKeys == Keys.Control)
                return;

            richTextBox1.SelectedText =  UrduKeyboard.ConvertKey(e.KeyChar).ToString();
            e.Handled = UrduKeyboard.isKeyFound;

           

        }

        WindowsMediaPlayer Player = new WindowsMediaPlayer();

        void alphateFeedback()
        {
            try
            {
                string alphabate = richTextBox1.Text.Substring(richTextBox1.SelectionStart, 1);

                if (alphabate == "ا")
                {
                    WindowsMediaPlayerClass wmp = new WindowsMediaPlayerClass();
                    Player.URL = "alif";                
                    Player.controls.play();
                   
                  //  MessageBox.Show(mediaInfo.);

                }
                else if (alphabate == "ب")
                {
                    Player.URL = "bee.mp3";
                    Player.controls.play();
                    MessageBox.Show(Player.currentMedia.durationString);
                }
                else if (alphabate == "پ")
                {
                    Player.URL = "pee.mp3";
                    Player.controls.play();
                }
                else if (alphabate == "ت")
                {
                    Player.URL = "tee.mp3";
                    Player.controls.play();
                }
                else if (alphabate == "ھ")
                {
                    Player.URL = "hee.mp3";
                    Player.controls.play();
                }
                else if (alphabate == "د")
                {
                    Player.URL = "dal.mp3";
                    Player.controls.play();
                }
                else if (alphabate == "ر")
                {
                    Player.URL = "ree.mp3";
                    Player.controls.play();
                }
                else if (alphabate == "س")
                {
                    Player.URL = "seen.mp3";
                    Player.controls.play();
                }
                else if (alphabate == "ش")
                {
                    Player.URL = "sheen.mp3";
                    Player.controls.play();
                }
                else if (alphabate == "ل")
                {
                    Player.URL = "laam.mp3";
                    Player.controls.play();
                }
                else if (alphabate == "م")
                {
                    Player.URL = "meem.mp3";
                    Player.controls.play();
                }
                else if (alphabate == " ")
                {
                    Player.URL = "space.mp3";
                    Player.controls.play();
                }

            }
            catch (Exception)
            {
            }
           
           
        }

      

        private void richTextBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.A)
            {
                Player.URL = "alif";
                Player.controls.play();
            }
            else if (e.KeyCode == Keys.B)
            {
                Player.URL = "bee.mp3";
                Player.controls.play();
            }
            else if (e.KeyCode == Keys.P)
            {
                Player.URL = "pee.mp3";
                Player.controls.play();
            }
            else if (e.KeyCode == Keys.T)
            {
                Player.URL = "tee.mp3";
                Player.controls.play();
            }
            else if (e.KeyCode == Keys.H)
            {
                Player.URL = "hee.mp3";
                Player.controls.play();
            }
            else if (e.KeyCode == Keys.D)
            {
                Player.URL = "dal.mp3";
                Player.controls.play();
            }
            else if (e.KeyCode == Keys.R)
            {
                Player.URL = "ree.mp3";
                Player.controls.play();
            }
            else if (e.KeyCode == Keys.S)
            {
                Player.URL = "seen.mp3";
                Player.controls.play();
            }
            else if (e.KeyCode == Keys.X)
            {
                Player.URL = "sheen.mp3";
                Player.controls.play();
            }
            else if (e.KeyCode == Keys.L)
            {
                Player.URL = "laam.mp3";
                Player.controls.play();
            }
            else if (e.KeyCode == Keys.M)
            {
                Player.URL = "meem.mp3";
                Player.controls.play();
            }
            else if (e.KeyCode == Keys.Space)
            {
               

                int lastSpace = richTextBox1.Text.LastIndexOf(' ', richTextBox1.SelectionStart - 1);
                string lastWord = richTextBox1.Text.Substring(lastSpace + 1, richTextBox1.SelectionStart-1 -lastSpace);
                label1.Focus();
                richTextBox1.Select(lastSpace + 1, richTextBox1.SelectionStart);
                //richTextBox1.SelectionColor = Color.Red;
                //richTextBox1.Select(richTextBox1.SelectionStart + richTextBox1.SelectionLength+1, 0);
                


                if (!hashSet.Contains(lastWord))
                {
                    Player.URL = "error.mp3";
                    Player.controls.play();
                   
                    richTextBox1.SelectionColor = Color.Red;
                    richTextBox1.Select(richTextBox1.SelectionStart + richTextBox1.SelectionLength + 1, 0);
                    richTextBox1.Focus();
                    richTextBox1.SelectionColor = Color.Black;
                }
                else
                {
                    Player.URL = "space.mp3";
                    Player.controls.play();

                    richTextBox1.SelectionColor = Color.Black;
                    richTextBox1.Select(richTextBox1.SelectionStart + richTextBox1.SelectionLength + 1, 0);
                    richTextBox1.Focus();
                }
            }
            else if (e.KeyCode == Keys.Back)
            {
                Player.URL = "backscape.mp3";
                Player.controls.play();
            }
            
           

        }

        private void richTextBox1_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left | e.KeyCode == Keys.Right)
            {
                alphateFeedback();
            }
        }

        private void WaveOutDevice_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            MessageBox.Show("Done");
        }

        private void menuStrip1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
            {
                
            }
        }

        private void menuStrip1_KeyPress(object sender, KeyPressEventArgs e)
        {
            MessageBox.Show(((ToolStripMenuItem)sender).Text);

        }

        private void menuStrip1_Enter(object sender, EventArgs e)
        {
            MessageBox.Show("ADS");
        }

        private void saveAsToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            MessageBox.Show("DFg");
        }

        private void ghjghjToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            MessageBox.Show("DFg");
        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {

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
    }
}
