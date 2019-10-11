	
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Speech;
using System.Data;
using System.Windows.Forms;
using System.Speech.Synthesis;

namespace MenuStripZ
{
    

    public class MenuStripZ : System.Windows.Forms.MenuStrip
    {
        public string LastSelection;

        public MenuStripZ()
        {
            this.Renderer = new CustomProfessionalRenderer(this);
           
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            this.Items[0].BackColor = Color.White;
            base.OnPaint(e);
        }

        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);

        }

        protected override void OnMouseMove(MouseEventArgs mea)
        {
            base.OnMouseMove(mea);
            
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
           
        }
    }

    public class CustomProfessionalRenderer : ToolStripRenderer
    {
        SpeechSynthesizer synthesizer = new SpeechSynthesizer();
        MenuStripZ menu;
        string lastselection = "";
        

        public  CustomProfessionalRenderer(MenuStripZ menuu)
        {
            this.menu = menuu;
            this.lastselection = menuu.LastSelection;
            synthesizer.SelectVoiceByHints(VoiceGender.Female, VoiceAge.Teen);
            synthesizer.SetOutputToDefaultAudioDevice();


        }

        


        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.FillRectangle(new SolidBrush(Color.White), e.ToolStrip.ClientRectangle);
            base.OnRenderToolStripBackground(e);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected)
            {
                Brush b2 = new SolidBrush(Color.RoyalBlue);
                e.Graphics.FillRectangle(b2, e.Item.ContentRectangle);
                e.Item.ForeColor = Color.White;

                if (e.Item.Text != lastselection)
                {                
                    //foreach (ToolStripItem item in menu.Items)
                    //{
                    //    if (item.Text == e.Item.Text)
                    //        return;
                    //}
                    synthesizer.SpeakAsyncCancelAll();
                    if (e.Item.Text.StartsWith("&"))
                        
                    {

                        synthesizer.SpeakAsync(e.Item.Text.Substring(1, e.Item.Text.Length - 1));
                    }
                    else
                    {
                        synthesizer.SpeakAsync(e.Item.Text);

                    }
                    menu.LastSelection = e.Item.Text;
                }

            }
            else
            {
                e.Item.ForeColor = Color.Black;
            }          
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (e.ToolStrip.IsDropDown)
            e.Graphics.DrawRectangle(new Pen(Color.LightGray,2), e.ToolStrip.ClientRectangle);
            base.OnRenderToolStripBorder(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            e.Graphics.FillRectangle(Brushes.LightGray, e.Item.ContentRectangle.X+20, e.Item.ContentRectangle.Y, e.Item.ContentRectangle.Width, e.Item.ContentRectangle.Height-1);
            base.OnRenderSeparator(e);
        }         
    }
}