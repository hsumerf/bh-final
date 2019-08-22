using FastColoredTextBoxNS;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BrailleUrdu
{
    public partial class MainScreen : Form
    {
        public MainScreen()
        {
            InitializeComponent();
        }

        private void vScrollBar1_Scroll(object sender, ScrollEventArgs e)
        {
            fstBox.OnScroll(e, e.Type != ScrollEventType.ThumbTrack && e.Type != ScrollEventType.ThumbPosition);
           // fstBox.Refresh();
        }

        private void fstBox_ScrollbarsUpdated(object sender, EventArgs e)
        {
            AdjustScrollbars();
        }

        private void AdjustScrollbar(ScrollBar scrollBar, int max, int value, int clientSize)
        {
            scrollBar.LargeChange = clientSize / 3;
            scrollBar.SmallChange = clientSize / 11;
            scrollBar.Maximum = max + scrollBar.LargeChange;
            scrollBar.Visible = max > 0;
            scrollBar.Value = Math.Min(scrollBar.Maximum, value);
        }

        private void AdjustScrollbars()
        {           
            AdjustScrollbar(vScrollBar1, fstBox.VerticalScroll.Maximum, fstBox.VerticalScroll.Value, fstBox.ClientSize.Height);
        }

        private void fstBox_SelectionChanged(object sender, EventArgs e)
        {
          //  MessageBox.Show();
        }

        private void MainScreen_Load(object sender, EventArgs e)
        {
           // MessageBox.Show( fstBox.);
           //fstBox.DrawText()
        }

        int lineCount = 0;

        private string slpittext(string inpt, int limit)
        {
            StringBuilder sb = new StringBuilder();
            int startPos = 0, lastSpace = 0;


            for (int i = 0; i < inpt.Length; i++)
            {
                if (inpt[i] == '\n')
                {
                    sb.Append(inpt.Substring(startPos, i - startPos));
                    lineCount++;
                    startPos = i;
                }
                else if (inpt[i] == ' ')
                    lastSpace = i + 1;

                if (lineCount % 5 == 0)
                    limit = 35;
                else
                    limit = 39;


                if (i - startPos > limit)
                {
                    sb.Append(inpt.Substring(startPos, lastSpace - startPos));
                    if (lineCount % 5 == 0)
                    {
                        for (int g = 0; g < 39 - (lastSpace - startPos) - ((lineCount / 5)).ToString().Length ; g++)
                        {
                            sb.Append(" ");
                        }
                        sb.Append( (lineCount / 5));
                        //MessageBox.Show((lastSpace - startPos).ToString());
                    }
                    lineCount++;
                  
                    sb.Append('\n');
                 
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


        private void button1_Click(object sender, EventArgs e)
        {

            //var a = SplitToLines(fstBox.Text);
            //int lineStart = fstBox.LineInfos[fstBox.Selection.Start.iLine].GetWordWrapStringIndex(fstBox.Selection.Start.iChar);
            //string text;
            //var ab = fstBox.LineInfos[lineStart].GetWordWrapStringIndex(fstBox.Selection.Start.iChar);
            //if (ab == fstBox.LineInfos[fstBox.Selection.Start.iLine].CutOffPositions[0])
            //{

            //}
            //else
            //{
            //    text = fstBox.Text.Substring(fstBox.LineInfos[fstBox.Selection.Start.iLine].CutOffPositions[lineStart - 1], fstBox.LineInfos[fstBox.Selection.Start.iLine].CutOffPositions[lineStart] - fstBox.LineInfos[fstBox.Selection.Start.iLine].CutOffPositions[lineStart - 1]).ToString();
            //}sdfsdf

            //dsfsdf



            //var a = fstBox.LineInfos[0].CutOffPositions[0];
            //text = fstBox.Text.Substring(0, a);
            //MessageBox.Show(text);
            fstBox.Text = slpittext(fstBox.Text, 40);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (fstBox.Text.Contains(Environment.NewLine))
                MessageBox.Show("FG");
        }

        private void fstBox_PaintLine(object sender, PaintLineEventArgs e)
        {
            if (e.LineIndex % 5 == 0)
            {                
                e.Graphics.DrawLine(new Pen(Color.Black), e.LineRect.X,e.LineRect.Y,500,e.LineRect.Y);
            }
        }

        private static IEnumerable<string> Wrap(IEnumerable<string> words,
                                               int lineWidth)
        {
            var currentWidth = 0;
            foreach (var word in words)
            {
                if (currentWidth != 0)
                {
                    if (currentWidth + word.Length < lineWidth)
                    {
                        currentWidth++;
                        yield return " ";
                    }
                    else
                    {
                        currentWidth = 0;
                        yield return Environment.NewLine;
                    }
                }
                currentWidth += word.Length;
                yield return word;
            }
        }

        private static string Wrap(string text, int lineWidth)
        {
            return string.Join(string.Empty,
                               Wrap(
                                   text.Split(new char[0],
                                              StringSplitOptions
                                                  .RemoveEmptyEntries),
                                   lineWidth));
        }

        private void button3_Click(object sender, EventArgs e)
        {
            fstBox.Text = Wrap(fstBox.Text, 40);
        }
    }
}
