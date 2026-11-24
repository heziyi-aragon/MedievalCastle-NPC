using System;
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
        private const float TalkDistance = 1.6f;
        private const float SwitchMargin = .16f;
        internal const string LanguageKey = "CastleNPC.Language";
        [SerializeField] private Transform trainingTarget;

        private readonly CastleNpcCharacter[] actors = new CastleNpcCharacter[3];
        private readonly Collider[][] shapes = new Collider[3][];
        private readonly RaycastHit[] sightHits = new RaycastHit[32];
        private NpcDialogueService service;
        private NpcPlayerState state;
        private Transform viewer;
        private Font font;
        private CancellationTokenSource cancellation;
        private NpcTurnResult result;
        private int role = -1, nearRole = -1;
        private bool chinese, guardWelcomed, exploredAfterGuard, failed;
        private float speakingUntil;
        private string sentText = "", typed = "";

        internal bool MenuOpen { get; private set; }
        internal bool InConversation => role >= 0;
        internal int SelectedRole => role;
        internal bool Chinese => chinese;
        internal CastleNpcCharacter Nearby => nearRole >= 0 ? actors[nearRole] : null;
        internal NpcTurnResult LastTurn => result;
        private bool Pending => cancellation != null;
        private bool Failed => failed;
        private string T(string english, string zh) => chinese ? zh : english;
        private string RoleName(int id) => NpcDemoScenario.RoleName(id, chinese);
        private string Prompt(int index) => NpcDemoScenario.Prompt(role, index, chinese);

        private void Awake()
        {
            service = GetComponent<NpcDialogueService>();
            service.Mode = NpcProviderMode.DeepSeek;
            state = NpcDemoScenario.MakeState();
            chinese = PlayerPrefs.GetString(LanguageKey, "en") == "zh";
            foreach (var actor in GetComponentsInChildren<CastleNpcCharacter>(true))
            {
                int index = Array.IndexOf(NpcDemoScenario.Roles, actor.RoleId);
                if (index < 0) continue;
                actors[index] = actor;
                shapes[index] = actor.GetComponentsInChildren<Collider>(true);
            }
            font = CreateDisplayFont();
        }

        internal static Font CreateDisplayFont()
        {
            return Font.CreateDynamicFontFromOSFont(new[] {
                "Microsoft YaHei UI", "Microsoft YaHei", "微软雅黑", "DengXian", "SimHei",
                "Noto Sans CJK SC", "PingFang SC", "Arial"
            }, 24);
        }

        private void Update()
        {
            if (speakingUntil > 0 && Time.unscaledTime >= speakingUntil)
            {
                speakingUntil = 0;
                if (InConversation && actors[role]) actors[role].EndConversation();
            }
            UpdateDesktop();
            UpdateHeadset();
        }

        internal void SetLanguage(bool useChinese)
        {
            if (chinese == useChinese) return;
            chinese = useChinese;
            PlayerPrefs.SetString(LanguageKey, chinese ? "zh" : "en");
            PlayerPrefs.Save();
            replyPage = 0;
        }

        internal void ToggleMenu()
        {
            MenuOpen = !MenuOpen;
            CastlePlayerMotor.MovementLocked = MenuOpen || InConversation;
        }

        // Measure reach around the player's upper body, independently of where the headset points.
        private float VisibleDistance(int id, Transform observer, Vector3 body, float limit)
        {
            var actor = actors[id];
            if (!actor || !actor.isActiveAndEnabled) return float.PositiveInfinity;
            float nearest = float.PositiveInfinity;
            foreach (var shape in shapes[id])
            {
                if (!shape || !shape.enabled || !shape.gameObject.activeInHierarchy) continue;
                var point = shape.ClosestPoint(body);
                var delta = point - body;
                float distance = new Vector2(delta.x, delta.z).magnitude;
                if (distance > limit || distance >= nearest || Mathf.Abs(delta.y) > 1.1f) continue;
                float length = delta.magnitude;
                if (length > .01f)
                {
                    int count = Physics.RaycastNonAlloc(body, delta / length, sightHits, length, ~0, QueryTriggerInteraction.Ignore);
                    bool blocked = count == sightHits.Length;
                    for (int i = 0; i < count && !blocked; i++)
                    {
                        var tr = sightHits[i].transform;
                        blocked = !tr.IsChildOf(actor.transform) && !observer.IsChildOf(tr);
                    }
                    if (blocked) continue;
                }
                nearest = distance;
            }
            return nearest;
        }

        internal void UpdateNearby(Transform observer)
        {
            if (!observer) { nearRole = -1; return; }
            Vector3 body = observer.position - Vector3.up * .35f;
            if (InConversation)
            {
                if (VisibleDistance(role, observer, body, TalkDistance + .5f) < float.PositiveInfinity)
                    return;
                EndTalk();
            }
            int prior = nearRole, nearest = -1;
            float best = float.PositiveInfinity, priorDistance = float.PositiveInfinity;
            for (int i = 0; i < actors.Length; i++)
            {
                float distance = VisibleDistance(i, observer, body, TalkDistance);
                if (i == prior) priorDistance = distance;
                if (distance < best) { nearest = i; best = distance; }
            }
            nearRole = priorDistance < float.PositiveInfinity && priorDistance <= best + SwitchMargin ? prior : nearest;
        }

        internal string ExploreText(bool headset)
        {
            string next = T("Visit the merchant and the knight by the training ground.",
                "去和商人聊聊，再到训练场旁找骑士。");
            if (Nearby)
            {
                string name = RoleName(nearRole);
                string action = (headset ? "A" : "E") + (chinese ? " · 与" + name + "交谈" : " · Talk to " + name);
                return guardWelcomed && !exploredAfterGuard && nearRole == 0 ? action + "\n" + next : action;
            }
            if (!guardWelcomed)
                return T("Walk up to the guard", "走到守卫身边") + "\n" +
                    (headset ? T("Left stick to move · B for settings", "左摇杆移动 · B 打开设置")
                             : T("WASD to move · Mouse to look · Esc for settings", "WASD 移动 · 鼠标转向 · Esc 打开设置"));
            return exploredAfterGuard ? "" : next;
        }

        internal void BeginNearby(Transform observer)
        {
            if (MenuOpen || InConversation || !observer) return;
            UpdateNearby(observer);
            if (!Nearby) return;
            role = nearRole;
            viewer = observer;
            actors[role].Greet(viewer);
            if (guardWelcomed && role != 0) exploredAfterGuard = true;
            CastlePlayerMotor.MovementLocked = true;
            ResetDialoguePresentation();
        }

        internal void EndTalk()
        {
            var prior = cancellation;
            cancellation = null;
            prior?.Cancel();
            if (InConversation && actors[role]) actors[role].EndConversation();
            role = -1;
            viewer = null;
            speakingUntil = 0;
            sentText = typed = "";
            failed = false;
            result = null;
            CastlePlayerMotor.MovementLocked = MenuOpen;
            ResetDialoguePresentation();
        }

        internal void Submit(string text)
        {
            if (MenuOpen || !InConversation || Pending || string.IsNullOrWhiteSpace(text)) return;
            sentText = text.Trim();
            SendSaved();
        }

        internal void Retry()
        {
            if (!MenuOpen && InConversation && Failed && !Pending) SendSaved();
        }

        private async void SendSaved()
        {
            var request = new CancellationTokenSource();
            cancellation = request;
            result = null;
            failed = false;
            replyPage = 0;
            speakingUntil = 0;
            var actor = actors[role];
            actor.Listen(viewer);
            try
            {
                var reply = await service.SendTurnAsync(actor.RoleId, sentText, state, chinese ? "zh" : "en", request.Token);
                if (request != cancellation || request.IsCancellationRequested) return;
                result = reply;
                failed = !reply.Success;
                if (!failed)
                {
                    if (role == 0) guardWelcomed = true;
                    Vector3? target = trainingTarget ? (Vector3?)(trainingTarget.position + Vector3.up) : null;
                    actor.Speak(reply.Decision.Action, viewer, target);
                    speakingUntil = Time.unscaledTime + Mathf.Clamp(reply.Reply.Length * .05f, 1.3f, 5f);
                }
            }
            catch (Exception)
            {
                if (request == cancellation) failed = true;
            }
            finally
            {
                if (request == cancellation)
                {
                    cancellation = null;
                    if (failed && actor) actor.EndConversation();
                }
                request.Dispose();
            }
        }

        private void OnDisable()
        {
            MenuOpen = false;
            EndTalk();
            if (headsetCanvas) headsetCanvas.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (headsetCanvas) Destroy(headsetCanvas.gameObject);
            if (desktopSkin) Destroy(desktopSkin);
            if (font) Destroy(font);
        }
    }
}
