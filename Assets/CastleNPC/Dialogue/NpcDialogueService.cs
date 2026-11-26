using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MedievalCastle.Npc
{
    [DisallowMultipleComponent]
    public sealed class NpcDialogueService : MonoBehaviour
    {
        [SerializeField] private NpcProviderMode mode = NpcProviderMode.DeepSeek;
        [SerializeField] private string model = "deepseek-flash";
        private const int HistoryLimit = 6;
        private readonly Dictionary<(string, string), List<Exchange>> histories =
            new Dictionary<(string, string), List<Exchange>>();
        private readonly Dictionary<string, Exchange> turns = new Dictionary<string, Exchange>();
        private readonly Dictionary<(string, string), CancellationTokenSource> inFlight =
            new Dictionary<(string, string), CancellationTokenSource>();
        private INpcDialogueProvider providerOverride;

        [Serializable]
        private sealed class Exchange
        {
            public string playerText;
            public NpcDecision proposedDecision;
            public string gameOutcome = "";
            public bool offlineDemo;
            [NonSerialized] public string turnId;
        }

        [Serializable]
        private sealed class Context
        {
            public string npcId;
            public string replyLanguage;
            public NpcPlayerState playerState;
            public Exchange[] previousTurns;
            public string playerText;
        }

        public NpcProviderMode Mode
        {
            get => mode;
            set
            {
                if (mode == value) return;
                Cancel();
                mode = value;
            }
        }

        public bool IsBusy => inFlight.Count != 0;
        internal bool UsesDeepSeek => mode == NpcProviderMode.DeepSeek && providerOverride == null;
        public bool HasApiKey => !string.IsNullOrWhiteSpace(DeepSeekNpcProvider.LocalSetting("DEEPSEEK_API_KEY"));

        public void SetProviderForTesting(INpcDialogueProvider provider)
        {
            Cancel();
            providerOverride = provider;
        }

        // Call on Unity's main thread. State belongs to the game and is copied at submission.
        public async Task<NpcTurnResult> SendTurnAsync(string npcId, string playerText,
            NpcPlayerState playerState, string language, CancellationToken cancellationToken = default)
        {
            bool zh = language == "zh";
            var result = new NpcTurnResult { IsOffline = mode == NpcProviderMode.Offline };
            if ((npcId != "guard" && npcId != "merchant" && npcId != "knight") ||
                (language != "en" && !zh) || playerState == null || string.IsNullOrWhiteSpace(playerState.playerId))
            {
                result.Error = zh ? "角色、玩家或语言设置无效。" : "Invalid character, player or language.";
                return result;
            }
            if (string.IsNullOrWhiteSpace(playerText) || playerText.Length > 2000)
            {
                result.Error = zh ? "请输入 1 至 2000 个字符。" : "Enter a message of 1 to 2000 characters.";
                return result;
            }

            var key = (playerState.playerId, npcId);
            if (inFlight.ContainsKey(key))
            {
                result.Error = zh ? "这个角色正在回复，请稍候。" : "This character is already answering this player.";
                return result;
            }

            var watch = Stopwatch.StartNew();
            NpcTurnTrace trace = null;
            var pending = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            inFlight.Add(key, pending);
            pending.CancelAfter(TimeSpan.FromSeconds(DeepSeekNpcProvider.TimeoutSeconds));
            try
            {
                pending.Token.ThrowIfCancellationRequested();
                var snapshotJson = JsonUtility.ToJson(playerState);
                var state = JsonUtility.FromJson<NpcPlayerState>(snapshotJson);
                histories.TryGetValue(key, out var prior);
                var context = new Context
                {
                    npcId = npcId, replyLanguage = language, playerState = state,
                    previousTurns = prior == null ? Array.Empty<Exchange>() : prior.ToArray(),
                    playerText = playerText.Trim()
                };
                string configuredModel = DeepSeekNpcProvider.LocalSetting("DEEPSEEK_MODEL");
                var request = new NpcProviderRequest
                {
                    NpcId = npcId, PlayerText = context.playerText, Language = language, State = state,
                    Model = string.IsNullOrEmpty(configuredModel) ? model : configuredModel,
                    Messages = new[] {
                        new NpcChatMessage("system", ReadPrompt("Shared") + "\n\n" +
                            ReadPrompt(npcId == "guard" ? "Guard" : npcId == "merchant" ? "Merchant" : "Knight")),
                        new NpcChatMessage("user", JsonUtility.ToJson(context))
                    }
                };
                var provider = providerOverride ?? (mode == NpcProviderMode.Offline
                    ? (INpcDialogueProvider)new NpcOfflineProvider() : new DeepSeekNpcProvider());
                trace = new NpcTurnTrace
                {
                    npcId = npcId,
                    model = request.Model,
                    language = language,
                    playerText = context.playerText,
                    liveProvider = providerOverride == null && mode == NpcProviderMode.DeepSeek,
                    offline = result.IsOffline,
                    requestJson = DeepSeekNpcProvider.SerializeRequest(request),
                    contextJson = request.Messages[1].content,
                    beforeStateJson = snapshotJson
                };
                result.Trace = trace;
                var operation = provider.GenerateAsync(request, pending.Token);
                var stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (pending.Token.Register(() => stopped.TrySetResult(true)))
                {
                    if (await Task.WhenAny(operation, stopped.Task) != operation)
                    {
                        // A custom provider may ignore cancellation and fault after we stop listening.
                        _ = operation.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                            TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                        pending.Token.ThrowIfCancellationRequested();
                    }
                    var raw = await operation;
                    pending.Token.ThrowIfCancellationRequested();
                    result.RawJson = raw ?? "";
                    trace.rawJson = result.RawJson;
                    if (snapshotJson != JsonUtility.ToJson(playerState))
                    {
                        result.Error = zh ? "玩家状态已变化，请用最新状态重试。" : "Player state changed. Retry using the latest state.";
                        return result;
                    }
                    trace.validationAttempted = true;
                    if (!NpcDecisionValidator.TryParse(raw, npcId, state, out var decision, out var error))
                    {
                        trace.validationError = error;
                        result.Error = zh ? "模型返回了无效决策，请重试。" : error;
                        return result;
                    }
                    trace.validationAccepted = true;
                    trace.proposedAction = decision.Action.ToString();
                    var turnId = Guid.NewGuid().ToString("N");
                    var exchange = new Exchange
                    {
                        turnId = turnId, playerText = context.playerText,
                        proposedDecision = JsonUtility.FromJson<NpcDecision>(raw),
                        offlineDemo = result.IsOffline
                    };
                    if (prior == null) histories.Add(key, prior = new List<Exchange>());
                    prior.Add(exchange);
                    turns.Add(turnId, exchange);
                    trace.turnId = turnId;
                    if (prior.Count > HistoryLimit)
                    {
                        turns.Remove(prior[0].turnId);
                        prior.RemoveAt(0);
                    }
                    result.Decision = decision;
                    result.TurnId = turnId;
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                result.Error = zh ? "请求已取消或超时。" : "Request canceled or timed out.";
            }
            catch (TimeoutException)
            {
                result.Error = zh ? "请求超时，请重试。" : "Request timed out. Please retry.";
            }
            catch (NpcProviderException ex)
            {
                result.Error = ex.Message;
            }
            catch (Exception)
            {
                // Never expose request headers, upstream messages, or credentials in logs or UI.
                result.Error = zh ? "未能获取对话，请检查配置并重试。" : "Unable to obtain a reply. Check configuration and retry.";
            }
            finally
            {
                if (trace != null)
                {
                    trace.elapsedMilliseconds = watch.ElapsedMilliseconds;
                    trace.error = result.Error ?? "";
                }
                watch.Stop();
                if (inFlight.TryGetValue(key, out var current) && current == pending) inFlight.Remove(key);
                pending.Dispose();
            }
            return result;
        }

        private static string ReadPrompt(string name)
        {
            var asset = Resources.Load<TextAsset>("CastleNPC/" + name);
            if (asset == null) throw new NpcProviderException("Missing character prompt: " + name);
            return asset.text;
        }

        // Only the first outcome for this exact retained turn becomes conversation context.
        public void ReportDecisionResult(string turnId, string outcome)
        {
            if (string.IsNullOrEmpty(turnId) || string.IsNullOrWhiteSpace(outcome) ||
                !turns.TryGetValue(turnId, out var entry) || entry.gameOutcome.Length != 0 ||
                entry.proposedDecision.action == "none") return;
            entry.gameOutcome = outcome.Trim();
        }

        public void Cancel()
        {
            var pending = new List<CancellationTokenSource>(inFlight.Values);
            inFlight.Clear();
            foreach (var turn in pending) turn.Cancel();
        }

        public void ResetSession()
        {
            Cancel();
            histories.Clear();
            turns.Clear();
        }

        private void OnDisable() => Cancel();
    }
}
