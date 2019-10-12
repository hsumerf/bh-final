using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BrailleUrdu
{
    abstract class Translator
    {

        public abstract string Translate(string val);
        public abstract string RTranslate(string val);
    }
}
