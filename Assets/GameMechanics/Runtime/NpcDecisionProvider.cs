using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MedievalCastle.Mechanics
{
    public abstract class NpcDecisionProvider : MonoBehaviour
    {
        public abstract Task<NpcDecision> DecideAsync(NpcRequest request, CancellationToken cancellationToken);

        public static NpcDecision DecodeJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("NPC response is empty.", nameof(json));
            NpcDecision response = JsonUtility.FromJson<NpcDecision>(json);
            if (response?.action == null || string.IsNullOrWhiteSpace(response.action.type) ||
                (response.action.type == "NONE" && string.IsNullOrWhiteSpace(response.reply)))
            {
                throw new FormatException("NPC response is missing a valid action or reply.");
            }
            return response;
        }
    }
}
