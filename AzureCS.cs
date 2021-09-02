using System;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.Utils;
using NAudio.Wave;
using System.IO;
using System.Threading.Tasks;

namespace AntiTeleBot
{
    public static class AzureCS
    {
        private static async Task ParseVoice(string payload)
        {
            var speechConfig = SpeechConfig.FromSubscription(
                System.Environment.GetEnvironmentVariable("SpeechKey"),
                System.Environment.GetEnvironmentVariable("SpeachLocationRegion"));
            speechConfig.SpeechRecognitionLanguage = System.Environment.GetEnvironmentVariable("SpeechRecognitionLanguage");
            var waveFormat = WaveFormat.CreateMuLawFormat(8000, 1);

            byte[] payloadByteArray = Convert.FromBase64String(payload);

            var pcmFormat = new WaveFormat(8000, 16, 1);
            var mulawFormat = WaveFormat.CreateMuLawFormat(8000, 1);
            var mulawstream = new RawSourceWaveStream(new MemoryStream(payloadByteArray), mulawFormat);
            var pcmstream = new WaveFormatConversionStream(pcmFormat, mulawstream);
            byte[] bytes = new byte[pcmstream.Length];
            pcmstream.Position = 0;
            pcmstream.Read(bytes, 0, (int)pcmstream.Length);

            using var audioInputStream = AudioInputStream.CreatePushStream();
            using var audioConfig = AudioConfig.FromStreamInput(audioInputStream);
            using var recognizer = new SpeechRecognizer(speechConfig, audioConfig);
            audioInputStream.Write(bytes, bytes.Length);

            var result = await recognizer.RecognizeOnceAsync();
            Console.WriteLine($"RECOGNIZED: Text={result.Text}");
        }
    }
}