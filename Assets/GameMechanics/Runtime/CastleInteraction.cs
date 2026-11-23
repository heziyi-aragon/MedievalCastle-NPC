using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MedievalCastle.Mechanics
{
    public sealed class CastleInteraction : MonoBehaviour
    {
        public CastlePlayer player;
        public GateView gate;
        public NpcActor[] actors;
        public Transform[] trainingPoints;
        public NpcDecisionProvider decisionProvider;
        public int startingGold = 30;
        public float talkRange = 3;
        public float trainingRadius = 1.25f;
        public float requestTimeoutSeconds = 15;

        private CastleGame game;
        private CastleHud hud;
        private NpcActor speaker;
        private CancellationTokenSource pending;
        private string previousResult;

        private void Start()
        {
            if (player == null || gate == null || decisionProvider == null || actors == null ||
                trainingPoints == null || trainingPoints.Length == 0)
            {
                Debug.LogError("Assign the castle player, NPCs, gate, training points and NPC provider.", this);
                enabled = false;
                return;
            }

            game = new CastleGame(startingGold, trainingPoints.Length);
            hud = new CastleHud(transform, Send, CloseDialogue);
            Refresh();
        }

        private void Update()
        {
            if (speaker != null)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) CloseDialogue();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape)) UnlockCursor();
            if (Input.GetMouseButtonDown(0)) LockCursor();

            if (game.Quest == QuestStage.InProgress)
            {
                Transform point = trainingPoints[game.TrainingProgress];
                if (point != null && WithinRange(point.position, trainingRadius) &&
                    game.VisitTrainingPoint(game.TrainingProgress))
                {
                    Refresh();
                }
            }

            NpcActor candidate = null;
            float nearest = talkRange * talkRange;
            foreach (NpcActor actor in actors)
            {
                if (actor == null) continue;
                float distance = (player.transform.position - actor.transform.position).sqrMagnitude;
                if (distance < nearest)
                {
                    nearest = distance;
                    candidate = actor;
                }
            }

            hud.SetInteraction(candidate == null ? "" : $"E   Talk to {candidate.displayName}");
            if (candidate != null && Input.GetKeyDown(KeyCode.E)) OpenDialogue(candidate);
        }

        private bool WithinRange(Vector3 point, float distance) =>
            (player.transform.position - point).sqrMagnitude <= distance * distance;

        private void OpenDialogue(NpcActor actor)
        {
            speaker = actor;
            player.MovementLocked = true;
            UnlockCursor();
            hud.Open(actor);
        }

        private void CloseDialogue()
        {
            CancelPending();
            speaker = null;
            hud.Close();
            player.MovementLocked = false;
            LockCursor();
        }

        private async void Send(string message)
        {
            if (speaker == null || pending != null || string.IsNullOrWhiteSpace(message)) return;
            message = message.Trim();
            var actor = speaker;
            var request = new NpcRequest
            {
                requestId = Guid.NewGuid().ToString("N"),
                npcId = actor.npcId,
                role = actor.role.ToString(),
                message = message,
                player = game.Snapshot(),
                previousResult = previousResult
            };

            using (var cancellation = new CancellationTokenSource())
            {
                pending = cancellation;
                hud.OnSent(message);
                try
                {
                    Task<NpcDecision> answer = decisionProvider.DecideAsync(request, cancellation.Token);
                    Task timeout = Task.Delay(TimeSpan.FromSeconds(Mathf.Max(1, requestTimeoutSeconds)), cancellation.Token);
                    if (await Task.WhenAny(answer, timeout) != answer)
                    {
                        ObserveLateFailure(answer);
                        if (pending == cancellation)
                        {
                            cancellation.Cancel();
                            hud.Append("System", "No reply arrived. Try sending again.");
                        }
                        return;
                    }

                    NpcDecision decision = await answer;
                    if (pending != cancellation || speaker != actor) return;
                    if (decision?.action == null || string.IsNullOrWhiteSpace(decision.action.type) ||
                        (decision.action.type == "NONE" && string.IsNullOrWhiteSpace(decision.reply)))
                        throw new FormatException("Incomplete NPC response.");

                    ActionResult result = game.Execute(request.requestId, actor.role, decision.action);
                    if (result.Success && !string.IsNullOrWhiteSpace(decision.reply))
                        hud.Append(actor.displayName, decision.reply);
                    if (!string.IsNullOrEmpty(result.Message))
                    {
                        previousResult = result.Message;
                        hud.Append("Game", result.Message);
                    }
                    Refresh();
                }
                catch (Exception ex)
                {
                    if (pending == cancellation)
                    {
                        Debug.LogWarning($"NPC request failed ({ex.GetType().Name}).", this);
                        hud.Append("System", "The NPC could not respond. Please try again.");
                    }
                }
                finally
                {
                    if (pending == cancellation)
                    {
                        pending = null;
                        cancellation.Cancel();
                        hud.SetWaiting(false);
                    }
                }
            }
        }

        private static async void ObserveLateFailure(Task task)
        {
            try { await task; }
            catch { /* The associated request has already ended. */ }
        }

        private void CancelPending()
        {
            CancellationTokenSource previous = pending;
            pending = null;
            previous?.Cancel();
        }

        private void Refresh()
        {
            gate.IsOpen = game.GateOpen;
            hud.Refresh(game.Snapshot());
        }

        private void OnDestroy()
        {
            CancelPending();
            if (speaker != null && player != null) player.MovementLocked = false;
            if (hud != null) UnlockCursor();
        }

        private static void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
