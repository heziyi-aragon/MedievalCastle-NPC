using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using MedievalCastle.NPC;
using UnityEngine;

[assembly: InternalsVisibleTo("CastleNPC.Demo.Tests")]
[assembly: InternalsVisibleTo("CastleNPC.Demo.PlayModeTests")]

namespace MedievalCastle.Npc.Demo
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NpcDialogueService))]
    public sealed partial class NpcDemoPanel : MonoBehaviour
    {
        private readonly Dictionary<string, CastleNpcCharacter> characters = new Dictionary<string, CastleNpcCharacter>();
        private NpcDialogueService service;
        private NpcPlayerState state;
        private NpcTurnResult result;
        internal NpcTurnResult LastTurn => result;
        private CancellationTokenSource cancellation;
        private CastleNpcCharacter speaker;
        private float speakingUntil;
        private int role;
        private bool chinese, hasApiKey;
        private string typed = "";
        private string sentText = "", sentLanguage = "", sentState = "", outcome = "", error = "";
        private int sentRole;
        private Font font;
        private bool Pending => cancellation != null;
        private bool CanReport => !Pending && result != null && result.Success &&
                                  result.Decision.Action != NpcAction.None && outcome.Length == 0;
        private bool Failed => !Pending && error.Length != 0;
        private string T(string english, string zh) => chinese ? zh : english;
        private string RoleName(int id) => NpcDemoScenario.RoleName(id, chinese);
        private string Prompt(int index) => NpcDemoScenario.Prompt(role, index, chinese);
        private string ModeName => service.Mode == NpcProviderMode.Offline ? T("Offline demo", "离线演示") : T("Live · DeepSeek", "在线 · DeepSeek");

        private void Awake()
        {
            service = GetComponent<NpcDialogueService>();
            hasApiKey = service.HasApiKey;
            service.Mode = hasApiKey ? NpcProviderMode.DeepSeek : NpcProviderMode.Offline;
            state = NpcDemoScenario.MakeState();
            foreach (var actor in FindObjectsOfType<CastleNpcCharacter>(true))
                if (actor.gameObject.scene == gameObject.scene && !string.IsNullOrEmpty(actor.RoleId))
                    characters[actor.RoleId] = actor;
            font = CreateDisplayFont();
        }

        internal static Font CreateDisplayFont()
        {
            return Font.CreateDynamicFontFromOSFont(new[] {
                "Microsoft YaHei UI", "Microsoft YaHei", "微软雅黑", "DengXian", "SimHei",
                "Noto Sans CJK SC", "PingFang SC", "Arial"
            }, 24);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused && service != null) hasApiKey = service.HasApiKey;
        }

        private void Update()
        {
            if (speaker != null && Time.unscaledTime >= speakingUntil) StopSpeaking();
            UpdateHeadset();
        }

        private void OnDisable()
        {
            RestoreDesktopCamera();
            var previous = cancellation;
            cancellation = null;
            previous?.Cancel();
            StopSpeaking();
            if (headsetCanvas != null) headsetCanvas.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (headsetCanvas != null) Destroy(headsetCanvas.gameObject);
            if (desktopSkin != null) Destroy(desktopSkin);
            if (font != null) Destroy(font);
        }

        private void StopSpeaking()
        {
            if (speaker != null) speaker.SetSpeaking(false);
            speaker = null;
        }

        internal void SelectRole(int next)
        {
            if (Pending || role == next) return;
            ClearDisplayedTurn();
            role = next;
            typed = "";
            headsetChoice = 1;
            desktopScroll = Vector2.zero;
        }

        private void ToggleMode()
        {
            if (Pending) return;
            ClearDisplayedTurn();
            hasApiKey = service.HasApiKey;
            service.Mode = service.Mode == NpcProviderMode.DeepSeek ? NpcProviderMode.Offline : NpcProviderMode.DeepSeek;
        }

        private void ClearDisplayedTurn()
        {
            StopSpeaking();
            result = null;
            sentText = sentState = error = outcome = "";
            replyPage = 0;
        }

        internal void Submit(string text)
        {
            if (Pending || string.IsNullOrWhiteSpace(text)) return;
            sentText = text.Trim();
            sentRole = role;
            sentLanguage = chinese ? "zh" : "en";
            sentState = JsonUtility.ToJson(state);
            SendSaved();
        }

        private async void SendSaved()
        {
            if (Pending || sentState.Length == 0) return;
            var request = new CancellationTokenSource();
            cancellation = request;
            error = "";
            outcome = "";
            result = null;
            replyPage = 0;
            StopSpeaking();
            try
            {
                var reply = await service.SendTurnAsync(NpcDemoScenario.Roles[sentRole], sentText,
                    JsonUtility.FromJson<NpcPlayerState>(sentState), sentLanguage, request.Token);
                if (request != cancellation) return;
                result = reply;
                if (!reply.Success) error = reply.Error;
                else if (characters.TryGetValue(NpcDemoScenario.Roles[sentRole], out speaker) && speaker != null)
                {
                    speaker.SetSpeaking(true);
                    speakingUntil = Time.unscaledTime + Mathf.Clamp(reply.Reply.Length * 0.05f, 1.3f, 5f);
                }
            }
            catch (OperationCanceledException)
            {
                if (request == cancellation) error = T("Request canceled.", "请求已取消。");
            }
            catch (Exception ex)
            {
                if (request == cancellation) error = T("Request failed", "请求失败") + " (" + ex.GetType().Name + ")";
            }
            finally
            {
                if (request == cancellation) cancellation = null;
                request.Dispose();
            }
        }

        private void Report(bool accepted)
        {
            if (!CanReport) return;
            var value = accepted ? "accepted" : "rejected";
            service.ReportDecisionResult(result.TurnId, value);
            outcome = value;
        }

        private void SetStatus(int index)
        {
            state.trainingStatus = NpcDemoScenario.Statuses[index];
            switch (state.trainingStatus)
            {
                case "available": state.trainingProgress = 0; break;
                case "active": state.trainingProgress = Mathf.Clamp(state.trainingProgress, 0, state.trainingTarget - 1); break;
                default: state.trainingProgress = state.trainingTarget; break;
            }
            draftState = "";
        }
    }
}
