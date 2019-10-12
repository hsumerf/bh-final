using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BrailleUrdu
{
    class UrduTranslator : Translator
    {

        Tuple<string, string>[] a = new[]
       {

        // normal contractions
     

        Tuple.Create("آ", ">"),      
        Tuple.Create("ا", "a"),
        Tuple.Create("ب", "b"),
        Tuple.Create("پ", "p"),
        Tuple.Create("ت","t"),
        Tuple.Create("ٹ", "["),
        Tuple.Create("ث", "?"),
        Tuple.Create("ج", "j"),
        Tuple.Create("چ", "c"),
        Tuple.Create("ح", ":"),
        Tuple.Create("خ","x"),
        Tuple.Create("د", "d"),
        Tuple.Create("ڈ", "+"),
        Tuple.Create("ذ", "!"),
        Tuple.Create("ر","r"),
        Tuple.Create("ڑ", "]"),
        Tuple.Create("ز", "z"),
        Tuple.Create("ژ", "\"z"),
        Tuple.Create("س", "s"),
        Tuple.Create("ش","%"),
        Tuple.Create("ص", "&"),
        Tuple.Create("ض", "$"),
        Tuple.Create("ط",")"),
        Tuple.Create("ظ", "="),
        Tuple.Create("ع", "("),
        Tuple.Create("غ","<"),
        Tuple.Create("ف", "f"),
        Tuple.Create("ق", "q"),
        Tuple.Create("ک","k"),
        Tuple.Create("گ", "g"),
        Tuple.Create("ل", "l"),
        Tuple.Create("م","m"),
        Tuple.Create("ن", "n"),
        Tuple.Create("ں", ";"),
        Tuple.Create("و","w"),
        Tuple.Create("ؤ", "\\"),
        Tuple.Create("ہ", "h"),
        Tuple.Create("ھ", "8"),
        Tuple.Create("ء", "'"),
        Tuple.Create("ئ", "'"),
        Tuple.Create("ی", "i"),
        Tuple.Create("ے", "/"),

        //Tuple.Create(":", "3"), ?????

        Tuple.Create("(", "7"),
        Tuple.Create(")", "7"),

         Tuple.Create("”", "8"),
        Tuple.Create("“", "0"),

        Tuple.Create("،", "1"),
        Tuple.Create("۔", "4"),

        Tuple.Create("ّ", ","),

        Tuple.Create("ِ","e"), //zaeer
        Tuple.Create("ُ", "u"), //pesh


        };


        public override string Translate(string value)
        {
            value = Regex.Replace(value, @"(\d+)", "#$1");

            var result = new StringBuilder(value);
            bool Open = false;         

            foreach (var item in a)
            {
                result.Replace(item.Item1, item.Item2);
            }

            return result.ToString();
        }

        public override string  RTranslate(string value)
        {
            
            var result = new StringBuilder(value);

            foreach (var item in a)
            {
                result.Replace(item.Item2, item.Item1);
            }

            return result.ToString();
        }

    }
}
