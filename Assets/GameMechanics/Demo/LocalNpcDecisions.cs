using System;
using System.Threading;
using System.Threading.Tasks;

namespace MedievalCastle.Mechanics
{
    public sealed class LocalNpcDecisions : NpcDecisionProvider
    {
        public override async Task<NpcDecision> DecideAsync(NpcRequest request, CancellationToken cancellationToken)
        {
            await Task.Delay(220, cancellationToken);
            string message = request.message.ToLowerInvariant();
            switch (request.role)
            {
                case nameof(NpcRole.Guard):
                    if (Has(message, "open", "enter", "pass", "通行", "开门", "进去"))
                        return Response("Let me inspect your pass.", "OPEN_GATE");
                    return Response("The gate leads to the training courtyard. The merchant sells passes.");

                case nameof(NpcRole.Merchant):
                    if (Has(message, "buy", "purchase", "购买", "买"))
                    {
                        if (Has(message, "pass", "通行证"))
                            return Response(null, "BUY_ITEM", CastleGame.PassId, 1);
                        if (Has(message, "tonic", "potion", "药水"))
                            return Response(null, "BUY_ITEM", CastleGame.PotionId, 1);
                    }
                    return Response($"Castle Pass: {CastleGame.PassPrice} gold. Herbal Tonic: {CastleGame.PotionPrice} gold.");

                case nameof(NpcRole.Knight):
                    if (Has(message, "claim", "reward", "领奖", "奖励"))
                        return Response(null, "CLAIM_REWARD");
                    if (Has(message, "accept", "start", "begin", "接受", "开始"))
                        return Response("The marked stations are in the courtyard.", "ACCEPT_QUEST");
                    if (request.player.questStage == nameof(QuestStage.InProgress))
                        return Response($"You've visited {request.player.trainingProgress} of {request.player.trainingTotal} stations.");
                    return Response(request.player.questStage == nameof(QuestStage.Complete)
                        ? "Your training is complete. Ask me to claim your reward."
                        : "Visit the three marked training stations in order. Say “Accept training” when ready.");

                default: return Response("I have no instructions for you.");
            }
        }

        private static bool Has(string text, params string[] words) =>
            Array.Exists(words, word => text.IndexOf(word, StringComparison.Ordinal) >= 0);

        private static NpcDecision Response(string reply, string type = "NONE", string item = null, int quantity = 0) =>
            new NpcDecision { reply = reply, action = new NpcAction { type = type, itemId = item, quantity = quantity } };
    }
}
