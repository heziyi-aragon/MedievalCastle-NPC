using System;
using System.Threading;
using System.Threading.Tasks;

namespace MedievalCastle.Npc
{
    public enum NpcProviderMode { DeepSeek, Offline }
    public enum NpcAction { None, CheckPass, RequestPurchase, OfferTraining, ReviewTraining }

    [Serializable]
    public sealed class NpcPlayerState
    {
        public string playerId;
        public bool hasPass;
        public int coins;
        public NpcInventoryItem[] inventory = Array.Empty<NpcInventoryItem>();
        public NpcCatalogItem[] catalog = Array.Empty<NpcCatalogItem>();
        public string questId = "courtyard_drill";
        public string trainingStatus = "available";
        public int trainingProgress;
        public int trainingTarget = 3;
    }

    [Serializable]
    public sealed class NpcInventoryItem
    {
        public string itemId;
        public int quantity;
    }

    [Serializable]
    public sealed class NpcCatalogItem
    {
        public string itemId;
        public int price;
    }

    [Serializable]
    public sealed class NpcDecision
    {
        public string reply;
        public string action;
        public string itemId;
        public int quantity;
        public string questId;
        public NpcAction Action { get; internal set; }
    }

    public sealed class NpcTurnResult
    {
        public bool Success => Decision != null;
        public string Reply => Decision?.reply ?? "";
        public string TurnId { get; internal set; } = "";
        public string RawJson { get; internal set; } = "";
        public string Error { get; internal set; } = "";
        public bool IsOffline { get; internal set; }
        public NpcDecision Decision { get; internal set; }
    }

    [Serializable]
    public sealed class NpcChatMessage
    {
        public string role;
        public string content;

        public NpcChatMessage(string role, string content)
        {
            this.role = role;
            this.content = content;
        }
    }

    public sealed class NpcProviderRequest
    {
        public string NpcId { get; internal set; }
        public string PlayerText { get; internal set; }
        public string Language { get; internal set; }
        public NpcPlayerState State { get; internal set; }
        public NpcChatMessage[] Messages { get; internal set; }
        public string Model { get; internal set; }
    }

    public interface INpcDialogueProvider
    {
        Task<string> GenerateAsync(NpcProviderRequest request, CancellationToken cancellationToken);
    }
}
