using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BrailleUrdu
{
    public static class Narrator
    {
        static Dictionary<string, string> dictionary = new Dictionary<string, string>()
        {
            {"ا","alif.mp3"},
            {"آ","alif mad.mp3"},
            {"ب", "bee.mp3"},
            {"پ","pee.mp3"},
            {"ت","tee.mp3"},
            {"ٹ","ttee.mp3"},
            {"ث","say.mp3"},
            {"ج", "jeem.mp3"},
            {"چ","chay.mp3"},
            {"ح","haa.mp3"},
            {"خ", "khay.mp3"},
            {"د","daal.mp3"},
            {"ڈ","ddaal.mp3"},
            {"ذ", "zaal.mp3"},
            {"ر","ree.mp3"},
            {"ڑ", "rree.mp3"},
            {"ز","zee.mp3"},
            {"ژ","tee.mp3"},
            {"س","seen.mp3"},
            {"ش", "sheen.mp3"},
            {"ص","suat.mp3"},
            {"ض","seen.mp3"},
            {"ط", "toe.mp3"},
            {"ظ","zoe.mp3"},
            {"ع","aen.mp3"},
            {"غ", "gaen.mp3"},
            {"ف","fee.mp3"},
            {"ق", "qaaf.mp3"},
            {"ک","kaaf.mp3"},
            {"گ","gaaf.mp3"},
            {"ل","laam.mp3"},           
            {"م","meem.mp3"},
            {"ن","noon.mp3"},
            {"ں", "gunna.mp3"},
            {"و","wao.mp3"},
            {"ہ","hay.mp3"},
            {"ھ", "chashmi.mp3"},
            {"ء","hamza.mp3"},
            {"ی","yay.mp3"},
            {"ے", "bari yay.mp3"},
            {" ", "space.mp3"},
        };

        static int currentAlphabate = 0;
        static bool ContinousSpeak = false;
        static  IWavePlayer waveOutDevice = new WaveOut();
        static AudioFileReader audioFileReader;
        static string textTospeech = "";

        public static void Initialize()
        {
            waveOutDevice.PlaybackStopped += WaveOutDevice_PlaybackStopped;
        }

        public static void Narrate(string text)
        {
            ContinousSpeak = true;
            currentAlphabate = 0;
            textTospeech = text;      
            SpeakAlphabate(textTospeech[currentAlphabate].ToString());
            currentAlphabate += 1;
        }

        private static void SpeakAlphabate(string alpha)
        {
            string result = "";
            dictionary.TryGetValue(alpha, out result);
            try
            {
                audioFileReader = new AudioFileReader(@"ur\"+result);
            }
            catch (Exception)
            {

            }          
            waveOutDevice.Init(audioFileReader);
            waveOutDevice.Play();
        }

        public static void Beep()
        {
            ContinousSpeak = false;
            audioFileReader = new AudioFileReader("error.mp3");
            waveOutDevice.Init(audioFileReader);
            waveOutDevice.Play();
        }

        private static void WaveOutDevice_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            if (currentAlphabate != textTospeech.Length && ContinousSpeak==true)
            {
                SpeakAlphabate(textTospeech[currentAlphabate].ToString());
                currentAlphabate++;
            }
            else
            {
                ContinousSpeak = false;
            }
        }
    }
}
