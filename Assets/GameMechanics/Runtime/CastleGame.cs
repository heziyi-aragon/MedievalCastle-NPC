using System;
using System.Collections.Generic;
using System.Linq;

namespace MedievalCastle.Mechanics
{
    public enum NpcRole { Guard, Merchant, Knight }
    public enum QuestStage { Available, InProgress, Complete, Rewarded }

    [Serializable]
    public sealed class NpcAction
    {
        public string type;
        public string itemId;
        public int quantity;
    }

    [Serializable]
    public sealed class NpcDecision
    {
        public string reply;
        public NpcAction action;
    }

    [Serializable]
    public sealed class InventoryEntry
    {
        public string itemId;
        public int quantity;
    }

    [Serializable]
    public sealed class PlayerSnapshot
    {
        public int gold;
        public int reputation;
        public InventoryEntry[] inventory;
        public bool gateOpen;
        public string questStage;
        public int trainingProgress;
        public int trainingTotal;
    }

    [Serializable]
    public struct NpcTurn
    {
        public string message;
        public string reply;
        public string result;
    }

    [Serializable]
    public sealed class NpcRequest
    {
        public string requestId;
        public string npcId;
        public string role;
        public string message;
        public PlayerSnapshot player;
        public string previousResult;
        public NpcTurn[] history;
    }

    public sealed class ActionResult
    {
        public bool Success { get; }
        public string Message { get; }
        private readonly string chineseMessage;

        public ActionResult(bool success, string message, string chineseMessage = null)
        {
            Success = success;
            Message = message;
            this.chineseMessage = chineseMessage;
        }

        public string MessageFor(bool chinese) => chinese ? chineseMessage ?? Message : Message;
    }

    public sealed class CastleGame
    {
        public const string PassId = "castle_pass";
        public const string PotionId = "potion";
        public const int PassPrice = 10;
        public const int PotionPrice = 8;
        public const int RewardGold = 20;
        public const int RewardReputation = 5;

        private readonly Dictionary<string, int> inventory = new Dictionary<string, int>();
        private readonly Dictionary<string, ActionResult> processed = new Dictionary<string, ActionResult>();
        private readonly int trainingTotal;

        public int Gold { get; private set; }
        public int Reputation { get; private set; }
        public bool GateOpen { get; private set; }
        public QuestStage Quest { get; private set; }
        public int TrainingProgress { get; private set; }
        public int TrainingTotal => trainingTotal;

        public CastleGame(int initialGold, int trainingTotal)
        {
            if (initialGold < 0) throw new ArgumentOutOfRangeException(nameof(initialGold));
            if (trainingTotal < 1) throw new ArgumentOutOfRangeException(nameof(trainingTotal));
            Gold = initialGold;
            this.trainingTotal = trainingTotal;
        }

        public static string ItemName(string id) => id == PassId ? "Castle Pass" : "Herbal Tonic";

        public PlayerSnapshot Snapshot()
        {
            return new PlayerSnapshot
            {
                gold = Gold,
                reputation = Reputation,
                gateOpen = GateOpen,
                questStage = Quest.ToString(),
                trainingProgress = TrainingProgress,
                trainingTotal = trainingTotal,
                inventory = inventory.OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => new InventoryEntry { itemId = item.Key, quantity = item.Value }).ToArray()
            };
        }

        public ActionResult Execute(string requestId, NpcRole role, NpcAction action)
        {
            if (string.IsNullOrWhiteSpace(requestId)) return Failure("Missing request ID.");
            if (processed.TryGetValue(requestId, out ActionResult result)) return result;

            result = Apply(role, action);
            processed.Add(requestId, result);
            return result;
        }

        public bool VisitTrainingPoint(int index)
        {
            if (Quest != QuestStage.InProgress || index != TrainingProgress)
                return false;

            TrainingProgress++;
            if (TrainingProgress == trainingTotal) Quest = QuestStage.Complete;
            return true;
        }

        private ActionResult Apply(NpcRole role, NpcAction action)
        {
            switch (action?.type)
            {
                case "NONE": return Success("");
                case "OPEN_GATE" when role == NpcRole.Guard: return OpenGate();
                case "BUY_ITEM" when role == NpcRole.Merchant: return Buy(action.itemId, action.quantity);
                case "ACCEPT_QUEST" when role == NpcRole.Knight: return AcceptQuest();
                case "CLAIM_REWARD" when role == NpcRole.Knight: return ClaimReward();
                default: return Failure("This NPC cannot perform that action.");
            }
        }

        private ActionResult OpenGate()
        {
            if (GateOpen) return Success("The gate is already open.");
            if (!inventory.ContainsKey(PassId)) return Failure("You need a Castle Pass. Ask the merchant.");
            GateOpen = true;
            return Success("Pass accepted. The castle gate is opening.");
        }

        public ActionResult QuotePurchase(string id, int quantity, out int total)
        {
            total = 0;
            if (id != PassId && id != PotionId)
                return new ActionResult(false, "That item is not for sale.", "这件商品不在出售目录里。");
            if (quantity < 1 || quantity > 9)
                return new ActionResult(false, "Choose a quantity from 1 to 9.", "购买数量须为 1 到 9 件。");
            if (id == PassId && (quantity != 1 || inventory.ContainsKey(id)))
                return new ActionResult(false, "You can carry one Castle Pass.", "每人只能持有一张城门通行证。");

            total = (id == PassId ? PassPrice : PotionPrice) * quantity;
            if (Gold < total)
                return new ActionResult(false, $"You need {total - Gold} more gold.", $"余额不足，还差 {total - Gold} 枚铜币。");
            return Success("");
        }

        private ActionResult Buy(string id, int quantity)
        {
            var quote = QuotePurchase(id, quantity, out int total);
            if (!quote.Success) return quote;
            inventory.TryGetValue(id, out int owned);
            Gold -= total;
            inventory[id] = owned + quantity;
            return Success($"Bought {quantity} × {ItemName(id)} for {total} gold.");
        }

        private ActionResult AcceptQuest()
        {
            if (!GateOpen) return Failure("Get your Castle Pass checked by the guard before training.");
            if (Quest != QuestStage.Available) return Failure("This quest has already been accepted.");
            Quest = QuestStage.InProgress;
            return Success($"Training accepted. Visit the {trainingTotal} marked stations in order.");
        }

        private ActionResult ClaimReward()
        {
            if (Quest == QuestStage.Rewarded) return Failure("You already claimed this reward.");
            if (Quest != QuestStage.Complete) return Failure("Finish all training stations before claiming the reward.");
            if (Gold > int.MaxValue - RewardGold) return Failure("Your gold balance is full.");

            Quest = QuestStage.Rewarded;
            Gold += RewardGold;
            Reputation += RewardReputation;
            return Success($"Reward received. +{RewardGold} gold, +{RewardReputation} reputation.");
        }

        private static ActionResult Success(string message) => new ActionResult(true, message);
        private static ActionResult Failure(string message) => new ActionResult(false, message);
    }
}
