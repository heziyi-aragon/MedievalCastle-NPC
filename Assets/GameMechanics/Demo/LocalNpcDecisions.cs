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
            string message = request.message.Trim().ToLowerInvariant().TrimEnd('.', '!', '?', '。', '！', '？');
            if (message.StartsWith("please ", StringComparison.Ordinal)) message = message.Substring(7);
            switch (request.role)
            {
                case nameof(NpcRole.Guard):
                    if (Matches(message, "open the gate", "can you open the gate", "check my pass", "check my castle pass", "开门", "检查通行证"))
                        return Response("Let me inspect your pass.", "OPEN_GATE");
                    return Response("The merchant at the market sells Castle Passes. Bring one here for inspection.");

                case nameof(NpcRole.Merchant):
                    if (Matches(message, "buy a pass", "buy a castle pass", "buy pass", "买通行证", "购买通行证"))
                        return Response(null, "BUY_ITEM", CastleGame.PassId, 1);
                    if (Matches(message, "buy a potion", "buy potion", "buy a tonic", "买药水", "购买药水"))
                        return Response(null, "BUY_ITEM", CastleGame.PotionId, 1);
                    return Response($"Castle Pass: {CastleGame.PassPrice} gold. Herbal Tonic: {CastleGame.PotionPrice} gold.");

                case nameof(NpcRole.Knight):
                    if (Matches(message, "claim my reward", "claim reward", "领奖", "领取奖励"))
                        return Response(null, "CLAIM_REWARD");
                    if (Matches(message, "accept training", "start training", "接受训练", "开始训练"))
                        return Response("Follow the marked stations at the training yard.", "ACCEPT_QUEST");
                    if (request.player.questStage == nameof(QuestStage.InProgress))
                        return Response($"You've visited {request.player.trainingProgress} of {request.player.trainingTotal} stations.");
                    if (request.player.questStage == nameof(QuestStage.Complete))
                        return Response("Your training is complete. Say “Claim my reward” to receive it.");
                    if (request.player.questStage == nameof(QuestStage.Rewarded))
                        return Response("You've finished training and collected your reward.");
                    return Response($"Visit {request.player.trainingTotal} marked stations in order. Say “Accept training” when ready.");

                default: return Response("I have no instructions for you.");
            }
        }

        private static bool Matches(string text, params string[] choices) =>
            Array.Exists(choices, choice => string.Equals(text, choice, StringComparison.Ordinal));

        private static NpcDecision Response(string reply, string type = "NONE", string item = null, int quantity = 0) =>
            new NpcDecision { reply = reply, action = new NpcAction { type = type, itemId = item, quantity = quantity } };
    }
}
