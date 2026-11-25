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
                    new NpcCatalogItem { itemId = CastleGame.PassId, price = CastleGame.PassPrice, name = CastleGame.ItemName(CastleGame.PassId), nameZh = "城门通行证" },
                    new NpcCatalogItem { itemId = CastleGame.PotionId, price = CastleGame.PotionPrice, name = CastleGame.ItemName(CastleGame.PotionId), nameZh = "草药药水" }
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
                    if (proposal.itemId != CastleGame.PassId || proposal.quantity != 1)
                        return T("This order is unavailable.", "此订单暂不可执行。");
                    if (confirmedAction != proposal.Action)
                        return T("Choose Buy one Castle Pass to place the order.", "请选择「买一张城门通行证」下单。");
                    var purchase = game.Execute(turn.TurnId, NpcRole.Merchant, new CastleAction
                    {
                        type = "BUY_ITEM",
                        itemId = proposal.itemId,
                        quantity = proposal.quantity
                    });
                    if (purchase.Success)
                        return T($"Castle Pass added. Balance: {game.Gold} coins.", $"通行证已加入背包。余额：{game.Gold} 枚铜币。");
                    return T(purchase.Message, "订单未完成，请检查余额和背包。");
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
    }
}
