using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace MedievalCastle.Npc
{
    internal static class DeepSeekNpcSpeech
    {
        private const string Endpoint = "https://api.deepseek.com/v1/tools/tts";
        private const int SampleRate = 24000;

        [Serializable] private sealed class Payload
        {
            public string text, voice_id;
            public string format = "pcm";
        }

        // Unity creates the clip on the main thread. The caller owns the returned clip.
        internal static async Task<AudioClip> GenerateAsync(string role, string line, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string key = DeepSeekNpcProvider.LocalSetting("DEEPSEEK_API_KEY");
            if (string.IsNullOrEmpty(key)) throw new InvalidOperationException("Speech key unavailable.");
            var body = new Payload { text = line, voice_id = role == "knight" ? "stella" : "tide" };
            using (var http = new UnityWebRequest(Endpoint, "POST"))
            {
                http.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
                http.downloadHandler = new DownloadHandlerBuffer();
                http.timeout = DeepSeekNpcProvider.TimeoutSeconds;
                http.redirectLimit = 0;
                http.SetRequestHeader("Content-Type", "application/json");
                http.SetRequestHeader("Authorization", "Bearer " + key);
                var op = http.SendWebRequest();
                try
                {
                    while (!op.isDone)
                    {
                        token.ThrowIfCancellationRequested();
                        await Task.Yield();
                    }
                    token.ThrowIfCancellationRequested();
                    if (http.result != UnityWebRequest.Result.Success || http.responseCode != 200)
                        throw new InvalidOperationException("Speech request failed (HTTP " + http.responseCode + ").");
                    string type = http.GetResponseHeader("Content-Type") ?? "";
                    if (!type.Split(';')[0].Trim().Equals("audio/pcm", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Unexpected speech response format.");
                    return DecodePcm(http.downloadHandler.data);
                }
                finally { if (!op.isDone) http.Abort(); }
            }
        }

        internal static AudioClip DecodePcm(byte[] pcm)
        {
            if (pcm == null || pcm.Length < 2 || pcm.Length % 2 != 0)
                throw new FormatException("Speech response contained incomplete 16-bit samples.");
            var samples = new float[pcm.Length / 2];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = (short)(pcm[2 * i] | (pcm[2 * i + 1] << 8)) / 32768f;
            var clip = AudioClip.Create("NPC voice", samples.Length, 1, SampleRate, false);
            if (clip.SetData(samples, 0)) return clip;
            UnityEngine.Object.Destroy(clip);
            throw new InvalidOperationException("Unity could not load the speech samples.");
        }
    }
}
