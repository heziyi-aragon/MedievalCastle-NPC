using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MedievalCastle.Npc
{
    internal sealed class NpcOfflineProvider : INpcDialogueProvider
    {
        public Task<string> GenerateAsync(NpcProviderRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = request.State;
            var text = request.PlayerText.ToLowerInvariant();
            bool zh = request.Language == "zh";
            var answer = new NpcDecision { action = "none", itemId = "", questId = "" };
            switch (request.NpcId)
            {
                case "guard":
                    if (IsDirections(text))
                        answer.reply = zh ? "商人在城门旁，骑士在庭院操练区。沿路标前行即可。"
                            : "The merchant stands by the gate and the knight waits on the training side of the courtyard.";
                    else if (ContainsAny(text, "rules", "law", "regulations", "规定", "规矩", "规则"))
                        answer.reply = zh ? "入城要核验通行证，城门的道路也须保持畅通。" : "Show your pass for inspection and keep the gate road clear.";
                    else if (!IsSpeculative(text) && ContainsAny(text, "pass", "through the gate", "enter", "entry", "通行", "通过城门", "进城", "入城", "放行"))
                    {
                        answer.action = "check_pass";
                        answer.reply = state.hasPass
                            ? (zh ? "请出示城门通行证，容我核验印记。" : "Let me inspect the seal on your gate pass.")
                            : (zh ? "我没看到你的通行证。持有效通行证才能进城。" : "I see no pass among your things. Entry requires a valid gate pass.");
                    }
                    else answer.reply = zh ? "旅人，城门由我看守。你来此有何事？" : "Traveler, I watch this gate. What brings you here?";
                    break;
                case "merchant":
                    Merchant(answer, state, text, zh);
                    break;
                case "knight":
                    if (IsDirections(text) || ContainsAny(text, "how do i", "how to", "what do i", "怎样", "怎么", "如何"))
                    {
                        answer.reply = zh ? $"依次走到庭院操练区的 {state.trainingTarget} 处标记，再回来报告。"
                            : $"Walk to the {state.trainingTarget} numbered markers in the training yard in order, then report back.";
                        break;
                    }
                    if (IsSpeculative(text))
                    {
                        answer.reply = zh ? "你准备好时，来庭院找我。" : "Come to the courtyard when you are ready.";
                        break;
                    }
                    if (!ContainsAny(text, "train", "drill", "progress", "review", "quest", "reward", "任务", "训练", "操练", "进度", "奖励", "复核", "检查", "练习"))
                    {
                        answer.reply = zh ? "准备好操练了吗？" : "Ready for the drill?";
                        break;
                    }
                    if (state.trainingStatus == "available")
                    {
                        answer.action = "offer_training";
                        answer.questId = state.questId;
                        answer.reply = zh ? $"依次走到庭院的 {state.trainingTarget} 处操练标记。"
                            : $"Walk to the {state.trainingTarget} numbered training markers in order.";
                    }
                    else if (state.trainingStatus == "active" || state.trainingStatus == "ready")
                    {
                        answer.action = "review_training";
                        answer.questId = state.questId;
                        bool complete = state.trainingProgress >= state.trainingTarget;
                        answer.reply = zh
                            ? $"已完成 {state.trainingProgress}/{state.trainingTarget} 处标记。" + (complete ? "可以确认领奖了。" : $"接着走到 {state.trainingProgress + 1} 号标记。")
                            : $"You reached {state.trainingProgress} of {state.trainingTarget} markers. " + (complete ? "You can confirm your reward." : $"Walk to marker {state.trainingProgress + 1}.");
                    }
                    else answer.reply = zh ? "这一轮操练已经领过奖励了。" : "The reward for this drill has already been claimed.";
                    break;
            }
            return Task.FromResult(JsonUtility.ToJson(answer));
        }

        private static void Merchant(NpcDecision answer, NpcPlayerState state, string text, bool zh)
        {
            var catalog = state.catalog ?? Array.Empty<NpcCatalogItem>();
            var matches = catalog.Where(i => i != null && !string.IsNullOrEmpty(i.itemId) &&
                ContainsAny(text, i.itemId.ToLowerInvariant(), Label(i, false).ToLowerInvariant(), Label(i, true))).ToArray();
            bool askingPrice = ContainsAny(text, "how much", "how many coins", "what does", "what would", "cost", "price",
                "afford", "多少钱", "价格", "价值", "贵吗", "几枚", "多少铜币");
            if (askingPrice || IsSpeculative(text) ||
                !ContainsAny(text, "buy", "purchase", "i'll take", "买", "我要", "来一", "给我"))
            {
                if (askingPrice && matches.Length == 1)
                {
                    var priced = matches[0];
                    if (TryPurchaseQuantity(text, priced, out int count) && count > 1)
                    {
                        long quote = (long)priced.price * count;
                        answer.reply = zh ? $"每件 {priced.price} 枚铜币。{count} 件{Label(priced, true)}共 {quote} 枚。"
                            : $"At {priced.price} coins each, {count} × {Label(priced, false)} would cost {quote} coins.";
                    }
                    else answer.reply = zh ? $"每件{Label(priced, true)}是 {priced.price} 枚铜币。"
                        : $"The price per {Label(priced, false)} is {priced.price} coins.";
                    return;
                }
                var list = string.Join(zh ? "、" : ", ", catalog.Where(i => i != null)
                    .Select(i => $"{Label(i, zh)} ({i.price})"));
                answer.reply = catalog.Length == 0
                    ? (zh ? "今天货架是空的，旅人。" : "My shelves are empty today, traveler.")
                    : (zh ? $"今天的货品和铜币价格是：{list}。" : $"Today’s wares and prices in coins are {list}.");
                return;
            }
            if (matches.Length != 1)
            {
                answer.reply = zh ? "你想买货目里的哪一种？" : "Which single item from my catalog would you like?";
                return;
            }
            var item = matches[0];
            if (!TryPurchaseQuantity(text, item, out int quantity))
            {
                answer.reply = zh ? "请告诉我你要买的正整数数量。" : "How many whole items would you like to buy?";
                return;
            }
            answer.action = "request_purchase";
            answer.itemId = item.itemId;
            answer.quantity = quantity;
            long total = (long)item.price * quantity;
            answer.reply = total > state.coins
                ? (zh ? $"共需 {total} 枚铜币，你身上的钱还不够。" : $"That costs {total} coins, more than you are carrying.")
                : (zh ? $"{quantity} 件{Label(item, true)}共需 {total} 枚铜币。" : $"For {quantity} × {Label(item, false)}, the price is {total} coins.");
        }

        private static string Label(NpcCatalogItem item, bool zh)
        {
            string name = zh ? item.nameZh : item.name;
            if (!string.IsNullOrWhiteSpace(name)) return name;
            if (!zh) return item.itemId;
            return item.itemId switch
            {
                "bread" => "面包",
                "lantern" => "提灯",
                _ => item.itemId
            };
        }

        private static string ItemPattern(NpcCatalogItem item, bool zh) =>
            "(?:" + Regex.Escape(Label(item, zh).ToLowerInvariant()) + "|" +
            Regex.Escape(item.itemId.ToLowerInvariant()) + ")";

        private static readonly string[] SmallNumbers = {
            "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
            "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"
        };
        private static readonly string[] Tens = {
            "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"
        };

        // Default to a single item only for an unqualified item name. Every other quantity
        // must parse completely, so "five or six" and "好多" cannot turn into orders for one.
        private static bool TryPurchaseQuantity(string text, NpcCatalogItem item, out int quantity)
        {
            quantity = 0;
            var en = Regex.Match(text, @"\b(?:buy|purchase|i['’]ll take)\s+(.+)$");
            if (en.Success)
            {
                var order = en.Groups[1].Value.Trim().TrimEnd('.', '!', '?');
                order = Regex.Replace(order, @",?\s+please$", "").TrimEnd();
                var match = Regex.Match(order, @"^(?:(.+?)\s+)?(?:(?:loaf|loaves|pieces?|units?)\s+of\s+)?"
                    + ItemPattern(item, false) + "(?:s|es)?$");
                if (!match.Success) return false;
                var amount = match.Groups[1].Value;
                if (amount == "" || amount == "a" || amount == "an" || amount == "the") { quantity = 1; return true; }
                if (int.TryParse(amount, NumberStyles.None, CultureInfo.InvariantCulture, out quantity)) return quantity > 0;
                quantity = Array.IndexOf(SmallNumbers, amount);
                if (quantity > 0) return true;
                var parts = amount.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
                int tens = parts.Length == 0 ? -1 : Array.IndexOf(Tens, parts[0]);
                if (tens < 2 || parts.Length > 2) return false;
                int units = parts.Length == 1 ? 0 : Array.IndexOf(SmallNumbers, parts[1]);
                if (units < 0 || units > 9 || (parts.Length == 2 && units == 0)) return false;
                quantity = tens * 10 + units;
                return true;
            }
            var zh = Regex.Match(text, @"(?:购买|买|我要(?:购买|买)?|给我(?:来|买)?|来)\s*(?<amount>.*?)\s*"
                + ItemPattern(item, true) + @"\s*(?:吧|谢谢)?[。！？.!?]*$");
            if (!zh.Success) return false;
            var count = Regex.Replace(zh.Groups["amount"].Value.Trim(), "[个件份盏袋块条把只包张瓶]$", "").Trim();
            if (count == "") { quantity = 1; return true; }
            if (int.TryParse(count, NumberStyles.None, CultureInfo.InvariantCulture, out quantity)) return quantity > 0;
            count = count.Replace('两', '二');
            const string digits = "零一二三四五六七八九";
            if (count.Length == 1 && count != "十")
            {
                quantity = digits.IndexOf(count[0]);
                return quantity > 0;
            }
            var pieces = count.Split('十');
            if (pieces.Length != 2 || pieces[0].Length > 1 || pieces[1].Length > 1) return false;
            int tensZh = pieces[0].Length == 0 ? 1 : digits.IndexOf(pieces[0][0]);
            int unitsZh = pieces[1].Length == 0 ? 0 : digits.IndexOf(pieces[1][0]);
            if (tensZh < 1 || unitsZh < 0) return false;
            quantity = tensZh * 10 + unitsZh;
            return true;
        }

        private static bool ContainsAny(string text, params string[] terms) => terms.Any(text.Contains);

        private static bool IsDirections(string text) => ContainsAny(text, "where", "direction", "get to", "reach ",
            "way to", "find the", "怎么去", "怎么走", "在哪里", "在哪", "如何到", "怎么到");

        private static bool IsSpeculative(string text) => ContainsAny(text, "do not", "don't", "don’t", "not ",
            "never ", "what if", "if i ", "if we ", "should i ", "would i ", "might ", "maybe", "hypothetical", "假设", "如果", "不想",
            "不要", "不买", "先不", "别买", "不打算", "或许");
    }
}
