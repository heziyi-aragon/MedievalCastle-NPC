using MedievalCastle.Mechanics;

namespace MedievalCastle.Npc.Demo
{
    public static class NpcDemoScenario
    {
        internal const string DrillId = "courtyard_drill";
        public static readonly string[] Roles = { "guard", "merchant", "knight" };
        public static readonly string[] Statuses = { "available", "active", "ready", "rewarded" };
        private static readonly string[] English = {
            "May I pass through the gate?", "How do I reach the courtyard?", "What are the castle rules?",
            "Buy one Castle Pass.", "What are you selling?", "Buy one herbal tonic.",
            "Start courtyard training.", "Please review my training progress.", "How do I complete the drill?"
        };
        private static readonly string[] Chinese = {
            "我可以通过城门吗？", "怎么去庭院？", "城堡有哪些规定？",
            "买一张城门通行证。", "你卖些什么？", "买一瓶草药药水。",
            "开始庭院操练。", "请检查我的训练进度。", "怎样完成训练？"
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

        internal static NpcGameplaySession CreateGameplay() => new NpcGameplaySession(new CastleGame(30, 3));

        public static NpcPlayerState MakeState() => CreateGameplay().Snapshot();
    }
}
