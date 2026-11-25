using System;
using System.Linq;
using CastleAction = MedievalCastle.Mechanics.NpcAction;
using CastleGame = MedievalCastle.Mechanics.CastleGame;
using NpcRole = MedievalCastle.Mechanics.NpcRole;
using QuestStage = MedievalCastle.Mechanics.QuestStage;

namespace MedievalCastle.Npc.Demo
{
    internal sealed class NpcGameplaySession
    {
        private readonly CastleGame game;
        internal bool GateOpen => game.GateOpen;
        internal PurchaseOrder PendingPurchase { get; private set; }

        internal sealed class PurchaseOrder
        {
            internal readonly string TurnId;
            internal readonly string ItemId;
            internal readonly int Quantity;
            internal readonly int Total;

            internal PurchaseOrder(string turnId, string itemId, int quantity, int total)
            {
                TurnId = turnId;
                ItemId = itemId;
                Quantity = quantity;
                Total = total;
            }

            internal string Describe(bool chinese) => chinese
                ? $"{ChineseItemName(ItemId)} × {Quantity}\n单价 {Total / Quantity} 枚 · 总价 {Total} 枚铜币"
                : $"{CastleGame.ItemName(ItemId)} × {Quantity}\n{Total / Quantity} coins each · Total {Total} coins";
        }

        internal NpcGameplaySession(CastleGame game)
        {
            this.game = game ?? throw new ArgumentNullException(nameof(game));
        }

        internal NpcPlayerState Snapshot()
        {
            var state = game.Snapshot();
            return new NpcPlayerState
            {
                playerId = "castle_player",
                hasPass = state.inventory.Any(item => item.itemId == CastleGame.PassId && item.quantity > 0),
                passVerified = state.gateOpen,
                coins = state.gold,
                reputation = state.reputation,
                inventory = state.inventory.Select(item => new NpcInventoryItem
                {
                    itemId = item.itemId,
                    quantity = item.quantity
                }).ToArray(),
                catalog = new[]
                {
                    new NpcCatalogItem { itemId = CastleGame.PassId, price = CastleGame.PassPrice, name = CastleGame.ItemName(CastleGame.PassId), nameZh = ChineseItemName(CastleGame.PassId) },
                    new NpcCatalogItem { itemId = CastleGame.PotionId, price = CastleGame.PotionPrice, name = CastleGame.ItemName(CastleGame.PotionId), nameZh = ChineseItemName(CastleGame.PotionId) }
                },
                questId = NpcDemoScenario.DrillId,
                trainingStatus = game.Quest switch
                {
                    QuestStage.InProgress => "active",
                    QuestStage.Complete => "ready",
                    QuestStage.Rewarded => "rewarded",
                    _ => "available"
                },
                trainingProgress = state.trainingProgress,
                trainingTarget = state.trainingTotal
            };
        }

        internal string Apply(NpcDialogueService service, string npcId, NpcTurnResult turn,
            NpcAction confirmedAction, bool chinese)
        {
            if (!turn.Success) return "";
            string outcome = Resolve(npcId, turn, confirmedAction, chinese);
            service.ReportDecisionResult(turn.TurnId, outcome);
            return outcome;
        }

        internal string FinishPurchase(NpcDialogueService service, bool confirm, bool chinese)
        {
            var order = PendingPurchase;
            if (order == null) return "";
            PendingPurchase = null;

            string outcome;
            if (confirm)
            {
                var purchase = game.Execute(order.TurnId, NpcRole.Merchant, new CastleAction
                {
                    type = "BUY_ITEM",
                    itemId = order.ItemId,
                    quantity = order.Quantity
                });
                outcome = purchase.Success
                    ? (chinese ? $"已购买 {ChineseItemName(order.ItemId)} × {order.Quantity}。余额：{game.Gold} 枚铜币。"
                        : $"Bought {order.Quantity} × {CastleGame.ItemName(order.ItemId)}. Balance: {game.Gold} coins.")
                    : purchase.MessageFor(chinese);
            }
            else outcome = chinese ? "已取消订单。" : "Order canceled.";

            service.ReportDecisionResult(order.TurnId, outcome);
            return outcome;
        }

        private string Resolve(string npcId, NpcTurnResult turn, NpcAction confirmedAction, bool chinese)
        {
            string T(string english, string zh) => chinese ? zh : english;
            var proposal = turn.Decision;
            switch (proposal.Action)
            {
                case NpcAction.None:
                    return "";
                case NpcAction.CheckPass when npcId == "guard":
                    var check = game.Execute(turn.TurnId, NpcRole.Guard, new CastleAction { type = "OPEN_GATE" });
                    return check.Success ? T("Castle Pass verified.", "通行证已核验。")
                        : T(check.Message, "请先向商人购买城门通行证。");
                case NpcAction.RequestPurchase when npcId == "merchant":
                    var quote = game.QuotePurchase(proposal.itemId, proposal.quantity, out int total);
                    if (!quote.Success) return quote.MessageFor(chinese);
                    PendingPurchase = new PurchaseOrder(turn.TurnId, proposal.itemId, proposal.quantity, total);
                    return "";
                case NpcAction.OfferTraining when npcId == "knight" && proposal.questId == NpcDemoScenario.DrillId:
                    if (confirmedAction != proposal.Action)
                        return T("Choose Start courtyard training to accept.", "请选择「开始庭院操练」接下任务。");
                    var accepted = game.Execute(turn.TurnId, NpcRole.Knight, new CastleAction { type = "ACCEPT_QUEST" });
                    return accepted.Success ? T("Courtyard training accepted.", "已接下庭院操练。")
                        : T(accepted.Message, game.GateOpen ? "操练已经开始。" : "请先向守卫核验通行证。");
                case NpcAction.ReviewTraining when npcId == "knight" && proposal.questId == NpcDemoScenario.DrillId:
                    return T($"Training: {game.TrainingProgress}/{game.TrainingTotal} markers.",
                        $"操练进度：{game.TrainingProgress}/{game.TrainingTotal}。");
                default:
                    return T("This NPC cannot perform that action.", "这个角色无法执行该动作。");
            }
        }

        private static string ChineseItemName(string id) => id == CastleGame.PassId ? "城门通行证" : "草药药水";
    }
}
