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
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using NAudio;
using NAudio.Wave;
using System.Speech.Synthesis;
using WMPLib;
using System.Drawing.Printing;

namespace BrailleUrdu
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        string DocumentName = "";
        SpeechSynthesizer synthesizer = new SpeechSynthesizer();
        HashSet<string> hashSet = new HashSet<string>();

        private void Form1_Load(object sender, EventArgs e)
        {
            UrduKeyboard ur = new UrduKeyboard();  
            var list = File.ReadAllLines(@"final.txt");
            hashSet = new HashSet<string>(list);
            installFont.RegisterFont("simbrl.ttf");
            fstBox.Font = new Font("SimBraille", 18);
            Narrator.Initialize();

        }

        UrduKeyboard UrduKeyboard = new UrduKeyboard();
        private bool brk;

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
            brk = false;

            UrduTranslator ur = new UrduTranslator();
            Formater formater = new Formater();
            fstBox.Text = formater.Format(ur.Transat(richTextBox1.Text), 40, 25);
            fstBox.Font = new Font("SimBraille", 18);

            brk = true;
        }

        private void fstBox_SelectionChanged_1(object sender, EventArgs e)
        {
            UrduTranslator urdu = new UrduTranslator();
            label1.Text = urdu.RTransat(fstBox.Lines[fstBox.Selection.Start.iLine]);
        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void fstBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                return;
            }

            if (fstBox.Lines[fstBox.Selection.Start.iLine].Length == 0 && brk == true && e.KeyCode == Keys.Back)
            {
                e.SuppressKeyPress = true;
                return;
            }

            if (fstBox.Selection.Start.iLine != fstBox.Selection.End.iLine)
            {
                e.SuppressKeyPress = true;
            }
        }


        private void fstBox_TextChanged(object sender, FastColoredTextBoxNS.TextChangedEventArgs e)
        {
            if (fstBox.Lines[fstBox.Selection.Start.iLine].Length > 40 && brk == true)
                fstBox.Undo();
        }

        private void embossToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            PrintDialog pd = new PrintDialog();
            pd.PrinterSettings = new PrinterSettings();
            if (DialogResult.OK == pd.ShowDialog(this))
            {

                RawHelper.SendStringToPrinter(pd.PrinterSettings.PrinterName, fstBox.Text);
            }
        }

        private void redToolStripMenuItem_Click(object sender, EventArgs e)
        {
            richTextBox1.SelectionColor = Color.Red;
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FileObject file = new FileObject();
            file.sightedText = richTextBox1.Text;
            file.nonSightedText = fstBox.Text;
            IFormatter formatter = new BinaryFormatter();

            if (DocumentName == "")
            {
                SaveFileDialog res = new SaveFileDialog();
                res.Filter = "Pak Braille |*.pkbr";
                if (res.ShowDialog() == DialogResult.OK)
                {
                    var filePath = res.FileName;
                    DocumentName = filePath;

                    Stream stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                    formatter.Serialize(stream, file);
                    stream.Close();
                }

            }
            else
            {
                Stream stream = new FileStream(DocumentName, FileMode.Create, FileAccess.Write);
                formatter.Serialize(stream, file);
                stream.Close();
            }
          

           
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {


            OpenFileDialog res = new OpenFileDialog();
            res.Filter = "Pak Braille |*.pkbr";

            if (res.ShowDialog() == DialogResult.OK)
            {
                var filePath = res.FileName;

                IFormatter formatter = new BinaryFormatter();
                Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                FileObject objnew = (FileObject)formatter.Deserialize(stream);
                stream.Close();

                richTextBox1.Text = objnew.sightedText;
                fstBox.Text = objnew.nonSightedText;

            }

        }
    }
}
