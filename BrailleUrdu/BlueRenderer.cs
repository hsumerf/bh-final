using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace BrailleUrdu
{
    class BlueRenderer : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground
        {
            get
            {
                return Color.White;
            }
        }

        public override Color ImageMarginGradientBegin
        {
            get
            {
                return Color.White;
            }
        }

        public override Color ImageMarginGradientMiddle
        {
            get
            {
                return Color.White;
            }
        }

        public override Color ImageMarginGradientEnd
        {
            get
            {
                return Color.White;
            }
        }

        public override Color MenuBorder
        {
            get
            {
                return Color.Gainsboro;
            }
        }

        public override Color MenuItemBorder
        {
            get
            {
                return Color.White;
            }
        }

        public override Color MenuItemSelected
        {
            get
            {
                return Color.FromArgb(255, 190, 223, 250);
            }
        }

        public override Color MenuStripGradientBegin
        {
            get
            {
                return Color.White;
            }
        }

        public override Color MenuStripGradientEnd
        {
            get
            {
                return Color.White;
            }
        }


        public override Color MenuItemSelectedGradientBegin
        {
            get
            {
                return Color.FromArgb(255, 190, 223, 250);
            }
        }

        public override Color MenuItemSelectedGradientEnd
        {
            get
            {
                return Color.FromArgb(255, 190, 223, 250);
            }
        }

        public override Color MenuItemPressedGradientBegin
        {
            get
            {
                return Color.FromArgb(255, 190, 223, 250);
            }
        }

        public override Color MenuItemPressedGradientEnd
        {
            get
            {
                return Color.FromArgb(255, 190, 223, 250);
            }
        }
       
    }
}
