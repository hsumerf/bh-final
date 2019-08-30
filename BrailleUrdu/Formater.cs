using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BrailleUrdu
{
    class Formater
    {

        public string Format(string text, int characterPerLine, int linesPerPage)
        {
            StringBuilder sb = new StringBuilder();
            WordSplitter ws = new WordSplitter(text);
            int i = 0;
            int charAtFirstLine = characterPerLine;
            Substring subs;
            while (true)
            {
                if (i % linesPerPage == 0)
                    subs = ws.getLine(charAtFirstLine);
                else
                    subs = ws.getLine(characterPerLine);
                if (subs == null)
                    break;

                sb.Append(subs.getText());
                sb.Append(Environment.NewLine);
                i++;
                //if (subs.IsParaChange())
                //    Console.WriteLine("Paragraph Changed");
            }
            return sb.ToString();

        }

    }
}
