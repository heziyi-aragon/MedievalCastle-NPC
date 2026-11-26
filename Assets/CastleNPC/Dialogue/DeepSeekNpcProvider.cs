using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace MedievalCastle.Npc
{
    internal sealed class NpcProviderException : Exception
    {
        public NpcProviderException(string publicMessage) : base(publicMessage) { }
    }

    internal sealed class DeepSeekNpcProvider : INpcDialogueProvider
    {
        internal const string Endpoint = "https://api.deepseek.com/chat/completions";
        internal const int TimeoutSeconds = 30;

        [Serializable] private sealed class Option { public string type; }
        [Serializable] private sealed class RequestBody
        {
            public string model;
            public NpcChatMessage[] messages;
            public Option response_format = new Option { type = "json_object" };
            public Option thinking = new Option { type = "disabled" };
            public bool stream = false;
            public int max_tokens = 512;
        }

        [Serializable] private sealed class Envelope { public Choice[] choices = null; }
        [Serializable] private sealed class Choice
        {
            public IncomingMessage message = null;
            public string finish_reason = "";
        }
        [Serializable] private sealed class IncomingMessage { public string content = ""; }

        internal static string SerializeRequest(NpcProviderRequest request)
        {
            return JsonUtility.ToJson(new RequestBody { model = request.Model, messages = request.Messages });
        }

        // Provider HTTP bodies may contain account information. Only return assistant message
        // content on success, and expose a fixed status-based message on service errors.
        internal static string DecodeResponse(long status, string json, bool zh)
        {
            if (status < 200 || status >= 300) throw new NpcProviderException(DescribeFailure(status, zh));
            Envelope response;
            try { response = JsonUtility.FromJson<Envelope>(json); }
            catch (ArgumentException) { response = null; }
            var choice = response?.choices != null && response.choices.Length != 0 ? response.choices[0] : null;
            if (choice?.finish_reason != "stop" || string.IsNullOrWhiteSpace(choice.message?.content))
                throw new NpcProviderException(zh ? "模型未返回完整的对话。请重试。" : "The model returned an incomplete response. Please retry.");
            return choice.message.content;
        }

        internal static string LocalSetting(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (string.IsNullOrWhiteSpace(value))
                value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
#endif
            return value?.Trim();
        }

        public async Task<string> GenerateAsync(NpcProviderRequest request, CancellationToken token)
        {
            bool zh = request.Language == "zh";
            string key = LocalSetting("DEEPSEEK_API_KEY");
            if (string.IsNullOrWhiteSpace(key))
                throw new NpcProviderException(zh ? "尚未配置 DEEPSEEK_API_KEY。" : "DEEPSEEK_API_KEY is not configured.");
            token.ThrowIfCancellationRequested();
            using (var http = new UnityWebRequest(Endpoint, "POST"))
            {
                http.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(SerializeRequest(request)));
                http.downloadHandler = new DownloadHandlerBuffer();
                http.timeout = TimeoutSeconds;
                http.redirectLimit = 0;
                http.SetRequestHeader("Content-Type", "application/json");
                http.SetRequestHeader("Authorization", "Bearer " + key);
                var operation = http.SendWebRequest();
                try
                {
                    while (!operation.isDone)
                    {
                        token.ThrowIfCancellationRequested();
                        await Task.Yield();
                    }
                    token.ThrowIfCancellationRequested();
                    if (http.result == UnityWebRequest.Result.ConnectionError || http.result == UnityWebRequest.Result.DataProcessingError)
                        throw new NpcProviderException(DescribeFailure(0, zh));
                    return DecodeResponse(http.responseCode, http.downloadHandler.text, zh);
                }
                finally
                {
                    if (!operation.isDone) http.Abort();
                }
            }
        }

        private static string DescribeFailure(long status, bool zh)
        {
            switch (status)
            {
                case 401: case 403: return zh ? "DeepSeek 身份验证失败，请检查本机密钥。" : "DeepSeek authentication failed. Check your local key.";
                case 402: return zh ? "DeepSeek 账户余额不足。" : "The DeepSeek account has insufficient balance.";
                case 429: return zh ? "DeepSeek 请求过于频繁，请稍后重试。" : "DeepSeek rate limit reached. Please retry later.";
                case 0: return zh ? "无法连接 DeepSeek 或请求超时，请检查网络。" : "DeepSeek could not be reached or the request timed out.";
                default: return zh ? $"DeepSeek 请求失败 (HTTP {status})。" : $"DeepSeek request failed (HTTP {status}).";
            }
        }
    }
}
