using System;
using System.Runtime.CompilerServices;
using System.Threading;
using MedievalCastle.NPC;
using UnityEngine;
using GateView = MedievalCastle.Mechanics.GateView;

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
        [SerializeField] private GateView gate;

        private readonly CastleNpcCharacter[] actors = new CastleNpcCharacter[3];
        private readonly Collider[][] shapes = new Collider[3][];
        private readonly RaycastHit[] sightHits = new RaycastHit[32];
        private NpcDialogueService service;
        private NpcGameplaySession gameplay;
        private Transform viewer;
        private Font font;
        private CancellationTokenSource cancellation;
        private NpcTurnResult result;
        private int role = -1, nearRole = -1;
        private bool chinese, failed;
        private string sentText = "";
        private NpcAction confirmedAction;
        private string gameOutcome = "";

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
            gameplay = NpcDemoScenario.CreateGameplay();
            chinese = PlayerPrefs.GetString(LanguageKey, "en") == "zh";
            foreach (var actor in GetComponentsInChildren<CastleNpcCharacter>(true))
            {
                int index = Array.IndexOf(NpcDemoScenario.Roles, actor.RoleId);
                if (index < 0) continue;
                actors[index] = actor;
                if (actor.RoleId == "knight") actor.SetTrainingFocus(trainingTarget);
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
            UpdateVoice();
            UpdateDesktop();
            UpdateHeadset();
        }

        internal void SetLanguage(bool useChinese)
        {
            if (chinese == useChinese) return;
            chinese = useChinese;
            PlayerPrefs.SetString(LanguageKey, chinese ? "zh" : "en");
            PlayerPrefs.Save();
        }

        internal void ToggleMenu()
        {
            MenuOpen = !MenuOpen;
            if (MenuOpen)
            {
                menuChoice = chinese ? 1 : 0;
                seenMenuPointer = false;
            }
            else languageSheet?.Hide();
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
            if ((observer.position - obstructedAt).sqrMagnitude > .04f) obstructedRole = -1;
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
            if (!Nearby) return string.Empty;
            if (obstructedRole == nearRole) return T("Step to one side to talk.", "向旁边走一步再交谈。");
            string name = RoleName(nearRole);
            return (headset ? "A" : "E") + (chinese ? " · 与" + name + "交谈" : " · Talk to " + name);
        }

        internal void BeginNearby(Transform observer)
        {
            if (MenuOpen || InConversation || !observer) return;
            UpdateNearby(observer);
            if (!Nearby) return;
            // A second quick press on End must not cut off the same NPC's goodbye.
            if (farewellActive && Nearby == speaker && Time.unscaledTime - farewellStartedAt < .35f) return;
            var eye = observer.GetComponent<Camera>();
            if (!eye || !PlaceWorldCards(Nearby, eye)) return;
            if (farewellActive) StopVoice();
            role = nearRole;
            viewer = observer;
            actors[role].Greet(viewer);
            StartGreeting(actors[role]);
            CastlePlayerMotor.MovementLocked = true;
            ResetDialoguePresentation();
        }

        internal void EndTalk()
        {
            var prior = cancellation;
            cancellation = null;
            prior?.Cancel();
            StopVoice();
            if (InConversation && actors[role]) actors[role].EndConversation();
            role = -1;
            viewer = null;
            sentText = "";
            confirmedAction = NpcAction.None;
            gameOutcome = "";
            failed = false;
            result = null;
            CastlePlayerMotor.MovementLocked = MenuOpen;
            ResetDialoguePresentation();
        }

        internal void Submit(string text, NpcAction confirm = NpcAction.None)
        {
            if (MenuOpen || !InConversation || Pending || string.IsNullOrWhiteSpace(text)) return;
            sentText = text.Trim();
            confirmedAction = confirm;
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
            gameOutcome = "";
            replyIsChinese = chinese;
            replyPage = 0;
            StopVoice();
            var actor = actors[role];
            actor.Listen(viewer);
            try
            {
                var reply = await service.SendTurnAsync(actor.RoleId, sentText, gameplay.Snapshot(),
                    replyIsChinese ? "zh" : "en", request.Token);
                if (request != cancellation || request.IsCancellationRequested) return;
                result = reply;
                failed = !reply.Success;
                if (!failed)
                {
                    gameOutcome = gameplay.Apply(service, actor.RoleId, reply, confirmedAction, replyIsChinese);
                    if (gate) gate.IsOpen = gameplay.GateOpen;
                    StartReply(actor, reply);
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
                    if (failed && actor) actor.Listen(viewer);
                }
                request.Dispose();
            }
        }

        private void OnDisable()
        {
            languageSheet?.Hide();
            MenuOpen = false;
            EndTalk();
            if (headsetCanvas) headsetCanvas.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            ReleaseGreetings();
            languageSheet?.Destroy();
            if (headsetCanvas) Destroy(headsetCanvas.gameObject);
            if (desktopSkin) Destroy(desktopSkin);
            if (font) Destroy(font);
        }
    }
}
