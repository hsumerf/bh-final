using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BrailleUrdu
{
    class UrduTranslator
    {

        Tuple<string, string>[] a = new[]
       {

        // normal contractions

        //Tuple.Create("ایک", "a"),
        //Tuple.Create("بہت", ">"),
        //Tuple.Create("پر", "B"),
        //Tuple.Create("تو", "P"),
        //Tuple.Create("ثابت", "T"),
        //Tuple.Create("جو", "["),
        //Tuple.Create("چاہیے", "?"),
        //Tuple.Create("حاصل", "J"),
        //Tuple.Create("خالی", "C"),
        //Tuple.Create("دونوں", ":"),
        //Tuple.Create("ڈاکٹر", "X"),
        //Tuple.Create("زرا", "D"),
        //Tuple.Create("رائے", "+"),
        //Tuple.Create("کو", "!"),
        //Tuple.Create("زیادہ", "R"),
        //Tuple.Create("سے", "]"),
        //Tuple.Create("شاید", "Z"),
        //Tuple.Create("صرف", "J"), //---------
        //Tuple.Create("ضایع", "S"),
        //Tuple.Create("طرح", "%"),
        //Tuple.Create("غرض", "&"),
        //Tuple.Create("فرض", "$"),
        //Tuple.Create("قبل", ")"),
        //Tuple.Create("کھے", "="),
        //Tuple.Create("اگر", "("),
        //Tuple.Create("لیکن", "<"),
        //Tuple.Create("میں", "F"),
        //Tuple.Create("نہیں", "Q"),
        //Tuple.Create("وہ", "K"),
        //Tuple.Create("ہے", "G"),
        //Tuple.Create("ہر", "L"),
        //Tuple.Create("اس", "M"),
        //Tuple.Create("بھ", ";"),
        //Tuple.Create("پھ", "W"),
        //Tuple.Create("تھ", "H"),
        //Tuple.Create("ٹھ", "8"),
        //Tuple.Create("جھ", "'"),
        //Tuple.Create("چھ", "i"),

        // reqiure chnges

        //Tuple.Create("تیر", "a"),
        //Tuple.Create("ثواب", ">"),
        //Tuple.Create("جنگ", "B"),
        //Tuple.Create("چار", "P"),
        //Tuple.Create("ہال", "T"),
        //Tuple.Create("خوش", "["),
        //Tuple.Create("دار", "?"),
        //Tuple.Create("دال", "J"),
        //Tuple.Create("زریع", "C"),
        //Tuple.Create("روز", ":"),
        //Tuple.Create("کوئی", "X"),
        //Tuple.Create("سار", "D"),
        //Tuple.Create("شاد", "+"),
        //Tuple.Create("صاحب", "!"),
        //Tuple.Create("ضررو", "R"),
        //Tuple.Create("طرف", "]"),
        //Tuple.Create("ظاہر", "Z"),
        //Tuple.Create("عام", "J"), //---------
        //Tuple.Create("غیر", "S"),
        //Tuple.Create("فرق", "%"),
        //Tuple.Create("کار", "&"),
        //Tuple.Create("گزر", "$"),
        //Tuple.Create("لیے", ")"),
        //Tuple.Create("میر", "="),
        //Tuple.Create("نظزر", "("), //---------
        //Tuple.Create("وار", "<"),
        //Tuple.Create("ہیں", "F"),
        //Tuple.Create("یقین", "Q"),
        //Tuple.Create("یوں", "K"),
        //Tuple.Create("اس میں", "G"),
        //Tuple.Create("اکثر", "L"),
        //Tuple.Create("چھوڑ", "M"),
        //Tuple.Create("ٹھور", ";"),  //---------
        //Tuple.Create("اسہتا", "W"),  //---------
        //Tuple.Create("پہنچ", "H"),
        //Tuple.Create("تعلق", "8"),
        //Tuple.Create("ثبوت", "'"),
        //Tuple.Create("چھ", "i"),
        //Tuple.Create("آئنده", "/"),
        //Tuple.Create("پرو", "E"),


        //urdu alphabates

        //Tuple.Create("١", "1"),
        //Tuple.Create("٢", "2"),
        //Tuple.Create("٣", "3"),
        //Tuple.Create("٤", "4"),
        //Tuple.Create("٥","5"),
        //Tuple.Create("٦", "6"),
        //Tuple.Create("٧", "7"),
        //Tuple.Create("٨", "8"),
        //Tuple.Create("٩", "9"),
        //Tuple.Create("٠", "0"),

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


        public string Transat(string value)
        {
            value = Regex.Replace(value, @"(\d+)", "#$1");

            var result = new StringBuilder(value);
            bool Open = false;         

            foreach (var item in a)
            {
                result.Replace(item.Item1, item.Item2);
            }

           

            //for (int i = 0; i < result.Length; i++)
            //{
            //    if (result[i] == '\"')
            //    {
            //        if (Open == false)
            //        {
            //            result[i] = '8';
            //            Open = true;
            //        }
            //        else
            //        {
            //            result[i] = '0';
            //            Open = false;
            //        }
            //    }
            // }

            return result.ToString();
        }

        public string RTransat(string value)
        {
            
            var result = new StringBuilder(value);

            foreach (var item in a)
            {
                result.Replace(item.Item2, item.Item1);
            }



            //for (int i = 0; i < result.Length; i++)
            //{
            //    if (result[i] == '\"')
            //    {
            //        if (Open == false)
            //        {
            //            result[i] = '8';
            //            Open = true;
            //        }
            //        else
            //        {
            //            result[i] = '0';
            //            Open = false;
            //        }
            //    }
            // }

            return result.ToString();
        }

    }
}
