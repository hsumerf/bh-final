using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BrailleUrdu
{
    public class WordSplitter
    {
        private int currentIndex;
        private String text;

        public WordSplitter(String text)
        {
            this.currentIndex = 0;
            this.text = text;
        }

        public Substring getLine(int noOfChars)
        {
            Substring substring = new Substring();
            Boolean isEnter = false;

            while (this.currentIndex < text.Length)
            {
                String word = "";
                while (this.currentIndex < text.Length)
                {
                    if (text[this.currentIndex] == '\r')
                    {
                        isEnter = true;
                        break;
                    }

                    if (text[this.currentIndex] == ' ')
                    {
                        this.currentIndex++;
                        if (text[this.currentIndex] == ' ')
                        {
                            word += ' ';
                            continue;
                        }
                        else
                            break;
                    }
                    else
                        word += text[this.currentIndex++];
                }

                if ((substring.getText().Length + word.Length + (substring.getText() == "" ? 0 : 1)) <= noOfChars)
                {
                    if (substring.getText() == "")
                        substring.setText(word);
                    else
                        substring.setText(substring.getText() + " " + word);

                    if (isEnter)
                    {
                        this.currentIndex += 2;
                        substring.setIsParaChanged(true);
                        return substring;
                    }
                }
                else
                {
                    this.currentIndex -= (word.Length + (isEnter ? 0 : 1));
                    return substring;
                }
            }

            if (substring.getText() != "")
                return substring;
            else
                return null;
        }
    }
}
