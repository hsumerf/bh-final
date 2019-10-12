using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BrailleUrdu
{

     abstract class Keyboard
    {
        public Dictionary<char, char> keysDictonary = new Dictionary<char, char>();
        public bool isKeyFound = false;
        public bool enableAudio = true;

        public char convertKey(char key)
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
