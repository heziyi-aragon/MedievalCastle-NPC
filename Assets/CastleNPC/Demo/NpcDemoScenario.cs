using System;

namespace MedievalCastle.Npc.Demo
{
    public static class NpcDemoScenario
    {
        public static readonly string[] Roles = { "guard", "merchant", "knight" };
        public static readonly string[] Statuses = { "available", "active", "ready", "rewarded" };
        private static readonly string[] English = {
            "May I pass through the gate?", "How do I reach the courtyard?", "What are the castle rules?",
            "I would like to buy one loaf of bread.", "What does a lantern cost?", "What are you selling?",
            "Can I start the courtyard drill?", "Please review my training progress.", "How do I complete the drill?"
        };
        private static readonly string[] Chinese = {
            "我可以通过城门吗？", "怎么去庭院？", "城堡有哪些规定？",
            "我想买一个面包。", "一盏提灯多少钱？", "你卖些什么？",
            "我可以开始庭院训练吗？", "请检查我的训练进度。", "怎样完成训练？"
        };

        public static string Prompt(int role, int index, bool chinese)
        {
            return (chinese ? Chinese : English)[role * 3 + index];
        }

        public static string RoleName(int role, bool chinese)
        {
            if (chinese) return role == 0 ? "守卫" : role == 1 ? "商人" : "骑士";
            return role == 0 ? "Guard" : role == 1 ? "Merchant" : "Knight";
        }

        public static NpcPlayerState MakeState()
        {
            return new NpcPlayerState {
                playerId = "demo_player", hasPass = true, coins = 10,
                inventory = Array.Empty<NpcInventoryItem>(),
                catalog = new[] {
                    new NpcCatalogItem { itemId = "bread", price = 2 },
                    new NpcCatalogItem { itemId = "lantern", price = 5 }
                },
                questId = "courtyard_drill", trainingStatus = "available",
                trainingProgress = 0, trainingTarget = 3
            };
        }
    }
}
