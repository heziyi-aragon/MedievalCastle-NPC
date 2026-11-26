using System.Collections.Generic;
using UnityEngine;

namespace MedievalCastle.Npc.Demo
{
    public sealed partial class NpcDemoPanel
    {
        private const int TraceHistoryLimit = 8;
        private readonly List<NpcTurnTrace> turnTraces = new List<NpcTurnTrace>();
        private int selectedTrace = -1;
        private bool traceVisible, traceShowFullRequest;
        private Vector2 traceScroll;
        private GUIStyle traceTextStyle, traceCodeStyle, traceSectionStyle;
        private string traceCopyStatus = "";

        internal IReadOnlyList<NpcTurnTrace> Traces => turnTraces;

        private void RecordTrace(NpcTurnResult turn)
        {
            if (turn?.Trace == null) return;
            turnTraces.Insert(0, turn.Trace);
            if (turnTraces.Count > TraceHistoryLimit) turnTraces.RemoveAt(turnTraces.Count - 1);
            selectedTrace = 0;
            traceCopyStatus = "";
        }

        private void CompleteTraceExecution(NpcTurnResult turn, string outcome)
        {
            if (turn?.Trace == null) return;
            string status = gameplay.PendingPurchase?.TurnId == turn.TurnId
                ? "Pending player confirmation; no purchase executed yet"
                : turn.Decision.Action == NpcAction.None
                    ? "Dialogue only; no world action proposed"
                    : "Unity execution completed";
            turn.Trace.CompleteExecution(JsonUtility.ToJson(gameplay.Snapshot()), status, outcome);
        }

        private void CompletePurchaseTrace(string turnId, bool confirmed, string outcome)
        {
            if (string.IsNullOrEmpty(turnId)) return;
            foreach (var trace in turnTraces)
            {
                if (trace.turnId != turnId) continue;
                trace.CompleteExecution(JsonUtility.ToJson(gameplay.Snapshot()),
                    confirmed ? "Purchase confirmed; Unity execution completed" : "Purchase canceled by player", outcome);
                return;
            }
        }

        private void DrawTrace(float width, float height)
        {
            EnsureTraceStyles();
            float panelWidth = Mathf.Min(610f, width * .48f);
            var bounds = new Rect(width - panelWidth - 12f, 12f, panelWidth, Mathf.Max(160f, height - 24f));
            GUI.Window(741103, bounds, DrawTraceContents, "AI Trace · F1");
        }

        private void EnsureTraceStyles()
        {
            if (traceTextStyle != null) return;
            traceTextStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true,
                richText = false
            };
            traceCodeStyle = new GUIStyle(GUI.skin.textArea)
            {
                fontSize = 12,
                wordWrap = true,
                richText = false,
                fixedHeight = 0f
            };
            traceSectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                richText = false
            };
        }

        private void DrawTraceContents(int windowId)
        {
            if (turnTraces.Count == 0)
            {
                GUILayout.Label("No dialogue turns yet. Talk to an NPC to capture the next request.", traceTextStyle);
                return;
            }

            selectedTrace = Mathf.Clamp(selectedTrace, 0, turnTraces.Count - 1);
            var trace = turnTraces[selectedTrace];
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Newer") && selectedTrace > 0)
            {
                selectedTrace--;
                traceCopyStatus = "";
            }
            GUILayout.Label($"{selectedTrace + 1} / {turnTraces.Count}", traceTextStyle, GUILayout.Width(55f));
            if (GUILayout.Button("Older") && selectedTrace < turnTraces.Count - 1)
            {
                selectedTrace++;
                traceCopyStatus = "";
            }
            GUILayout.EndHorizontal();

            string source = trace.liveProvider ? "LIVE DeepSeek" : trace.offline ? "OFFLINE" : "TEST PROVIDER";
            GUILayout.Label($"{source} · {trace.npcId} · {trace.model}", traceSectionStyle);
            GUILayout.Label($"Turn: {(string.IsNullOrEmpty(trace.turnId) ? "(not assigned)" : trace.turnId)}\n" +
                $"Elapsed: {trace.elapsedMilliseconds} ms", traceTextStyle);

            traceScroll = GUILayout.BeginScrollView(traceScroll);
            TraceSection("Player input");
            GUILayout.Label(trace.playerText, traceTextStyle, GUILayout.ExpandWidth(true));

            TraceSection("Game context sent to the LLM");
            GUILayout.Label(trace.contextJson, traceCodeStyle, GUILayout.ExpandWidth(true));
            traceShowFullRequest = GUILayout.Toggle(traceShowFullRequest,
                "Show full safe HTTP body (headers and credentials are never captured)");
            if (traceShowFullRequest)
                GUILayout.Label(trace.requestJson, traceCodeStyle, GUILayout.ExpandWidth(true));

            TraceSection("Raw LLM JSON");
            GUILayout.Label(string.IsNullOrEmpty(trace.rawJson) ? "(no model response)" : trace.rawJson,
                traceCodeStyle, GUILayout.ExpandWidth(true));

            TraceSection("Unity validation");
            string validation = !trace.validationAttempted
                ? "Not reached: no validated model decision"
                : trace.validationAccepted
                    ? "Accepted\nProposed action: " + trace.proposedAction
                    : "Rejected\n" + trace.validationError;
            GUILayout.Label(validation, traceTextStyle, GUILayout.ExpandWidth(true));

            TraceSection("Unity execution");
            string execution = trace.executionStatus;
            if (!string.IsNullOrEmpty(trace.executionOutcome)) execution += "\n" + trace.executionOutcome;
            GUILayout.Label(execution, traceTextStyle, GUILayout.ExpandWidth(true));

            TraceSection("Player state diff");
            GUILayout.Label(trace.stateDiff, traceCodeStyle, GUILayout.ExpandWidth(true));
            if (!string.IsNullOrEmpty(trace.error))
            {
                TraceSection("Error");
                GUILayout.Label(trace.error, traceTextStyle, GUILayout.ExpandWidth(true));
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy safe JSON"))
            {
                GUIUtility.systemCopyBuffer = trace.ToSafeJson();
                traceCopyStatus = "Copied";
            }
            GUILayout.Label(traceCopyStatus, traceTextStyle, GUILayout.Width(55f));
            if (GUILayout.Button("Clear history"))
            {
                turnTraces.Clear();
                selectedTrace = -1;
                traceCopyStatus = "";
            }
            GUILayout.EndHorizontal();
        }

        private void TraceSection(string title)
        {
            GUILayout.Space(7f);
            GUILayout.Label(title, traceSectionStyle, GUILayout.ExpandWidth(true));
        }
    }
}
