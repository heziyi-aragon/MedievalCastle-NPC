using System;
using System.Collections.Generic;
using UnityEngine;

namespace MedievalCastle.Npc
{
    // Contains only game dialogue data and the safe HTTP body. Never add headers,
    // credentials, provider response envelopes or account information to this record.
    [Serializable]
    public sealed class NpcTurnTrace
    {
        public string turnId = "";
        public string npcId = "";
        public string model = "";
        public string language = "";
        public string playerText = "";
        public bool liveProvider;
        public bool offline;
        public long elapsedMilliseconds;
        public string requestJson = "";
        public string contextJson = "";
        public string beforeStateJson = "";
        public string afterStateJson = "";
        public string rawJson = "";
        public bool validationAttempted;
        public bool validationAccepted;
        public string validationError = "";
        public string proposedAction = "";
        public string executionStatus = "Not executed";
        public string executionOutcome = "";
        public string stateDiff = "Not executed";
        public string error = "";

        public void CompleteExecution(string stateAfterJson, string status, string outcome)
        {
            afterStateJson = stateAfterJson ?? "";
            executionStatus = status ?? "";
            executionOutcome = outcome ?? "";
            stateDiff = BuildStateDiff(beforeStateJson, afterStateJson);
        }

        public string ToSafeJson() => JsonUtility.ToJson(this, true);

        private static string BuildStateDiff(string beforeJson, string afterJson)
        {
            if (string.IsNullOrEmpty(beforeJson) || string.IsNullOrEmpty(afterJson)) return "Not executed";
            try
            {
                var before = JsonUtility.FromJson<NpcPlayerState>(beforeJson);
                var after = JsonUtility.FromJson<NpcPlayerState>(afterJson);
                if (before == null || after == null) return "State diff unavailable";
                var lines = new List<string>();
                if (before.coins != after.coins) lines.Add($"coins: {before.coins} → {after.coins}");
                if (before.reputation != after.reputation)
                    lines.Add($"reputation: {before.reputation} → {after.reputation}");
                if (before.hasPass != after.hasPass) lines.Add($"hasPass: {Display(before.hasPass)} → {Display(after.hasPass)}");
                if (before.passVerified != after.passVerified)
                    lines.Add($"gateOpen: {Display(before.passVerified)} → {Display(after.passVerified)}");
                if (before.trainingStatus != after.trainingStatus)
                    lines.Add($"trainingStatus: {before.trainingStatus} → {after.trainingStatus}");
                if (before.trainingProgress != after.trainingProgress)
                    lines.Add($"trainingProgress: {before.trainingProgress} → {after.trainingProgress}");
                if (before.trainingTarget != after.trainingTarget)
                    lines.Add($"trainingTarget: {before.trainingTarget} → {after.trainingTarget}");
                string beforeInventory = Inventory(before), afterInventory = Inventory(after);
                if (beforeInventory != afterInventory) lines.Add($"inventory: {beforeInventory} → {afterInventory}");
                return lines.Count == 0 ? "No player state change" : string.Join("\n", lines.ToArray());
            }
            catch (ArgumentException)
            {
                return "State diff unavailable";
            }
        }

        private static string Display(bool value) => value ? "true" : "false";

        private static string Inventory(NpcPlayerState state)
        {
            if (state.inventory == null || state.inventory.Length == 0) return "(empty)";
            var items = new List<string>();
            foreach (var item in state.inventory)
                if (item != null && item.quantity != 0) items.Add(item.itemId + " ×" + item.quantity);
            return items.Count == 0 ? "(empty)" : string.Join(", ", items.ToArray());
        }
    }
}
