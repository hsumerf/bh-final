using System;
using System.Collections.Generic;
using System.Linq;

using System.Text;
using System.Threading.Tasks;

namespace BrailleUrdu
{
    class UrduKeyboard
    {
       
        Dictionary<char, char> keysDictonary = new Dictionary<char, char>();
        public bool isKeyFound = false;
        public bool enableAudio = true;

        public UrduKeyboard()
        {          
            keysDictonary.Add('a', 'ا');
            keysDictonary.Add('b', 'ب');
            keysDictonary.Add('c', 'چ');
            keysDictonary.Add('d', 'د');
            keysDictonary.Add('e', 'ع');
            keysDictonary.Add('f', 'ف');
            keysDictonary.Add('g', 'گ');
            keysDictonary.Add('h', 'ھ');
            keysDictonary.Add('i', 'ی');
            keysDictonary.Add('j', 'ج');
            keysDictonary.Add('k', 'ک');
            keysDictonary.Add('l', 'ل');
            keysDictonary.Add('m', 'م');
            keysDictonary.Add('n', 'ن');
            keysDictonary.Add('o', 'ہ');
            keysDictonary.Add('p', 'پ');
            keysDictonary.Add('q', 'ق');
            keysDictonary.Add('r', 'ر');
            keysDictonary.Add('s', 'س');
            keysDictonary.Add('t', 'ت');
            keysDictonary.Add('u', 'ٔ');
            keysDictonary.Add('v', 'ط');
            keysDictonary.Add('w', 'و');
            keysDictonary.Add('x', 'ش');
            keysDictonary.Add('y', 'ے');
            keysDictonary.Add('z', 'ز');

            keysDictonary.Add('A', 'آ');
            keysDictonary.Add('B', '0');
            keysDictonary.Add('C', 'ث');
            keysDictonary.Add('D', 'ڈ');
            keysDictonary.Add('E', '\0');
            keysDictonary.Add('F', '\0');
            keysDictonary.Add('G', 'غ');
            keysDictonary.Add('H', 'ح');
            keysDictonary.Add('I', '\0');
            keysDictonary.Add('J', 'ض');
            keysDictonary.Add('K', 'خ');
            keysDictonary.Add('L', '\0');
            keysDictonary.Add('M', '\0');
            keysDictonary.Add('N', 'ں');
            keysDictonary.Add('O', '\0');
            keysDictonary.Add('P', 'ُ');
            keysDictonary.Add('Q', '\0');
            keysDictonary.Add('R', 'ڑ');
            keysDictonary.Add('S', 'ص');
            keysDictonary.Add('T', 'ٹ');
            keysDictonary.Add('U', '\0');
            keysDictonary.Add('V', 'ظ');
            keysDictonary.Add('W', '\0');
            keysDictonary.Add('X', 'ژ');
            keysDictonary.Add('Y', '\0');           
            keysDictonary.Add('Z', 'ذ');

            keysDictonary.Add('1', '١');
            keysDictonary.Add('2', '٢');
            keysDictonary.Add('3', '٣');
            keysDictonary.Add('4', '٤');
            keysDictonary.Add('5', '٥');
            keysDictonary.Add('6', '٦');
            keysDictonary.Add('7', '٧');
            keysDictonary.Add('8', '٨');
            keysDictonary.Add('9', '٩');
            keysDictonary.Add('0', '٠');

            keysDictonary.Add('.', '۔');
            keysDictonary.Add(',', '،');
            keysDictonary.Add('<', 'ِ'); //urdu zeer           
        }

        public char ConvertKey(char key)
        {
            char val;           
            isKeyFound = keysDictonary.TryGetValue(key, out val);
            if (isKeyFound && enableAudio)
            {

                Narrator.Narrate(val.ToString());
            }
            return val;
        }

    }
}
