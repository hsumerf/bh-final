using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BrailleUrdu
{
    public class Substring
    {

        private String text;
        private bool isParaChanged;

        public Substring()
        {
            this.text = "";
            this.isParaChanged = false;
        }

        public void setText(String text)
        {
            this.text = text;
        }

        public String getText()
        {
            return this.text;
        }

        public void setIsParaChanged(bool isParaChanged)
        {
            this.isParaChanged = isParaChanged;
        }

        public bool IsParaChange()
        {
            return this.isParaChanged;
        }

    }
}
