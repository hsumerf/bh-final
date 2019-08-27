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
            {"ب", "bee.mp3"},
            {"پ","pee.mp3"},
            {"ت","tee.mp3"},
            {"ھ","hee.mp3"},
            {"د", "dal.mp3"},
            {"ر","ree.mp3"},
            {"س","seen.mp3"},
            {"ش", "sheen.mp3"},
            {"ل","laam.mp3"},
            {"م","meem.mp3"},
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
            audioFileReader = new AudioFileReader(result);
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
