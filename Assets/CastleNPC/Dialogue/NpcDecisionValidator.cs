using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MedievalCastle.Npc
{
    public static class NpcDecisionValidator
    {
        public static bool TryParse(string json, string npcId, NpcPlayerState state,
            out NpcDecision decision, out string error)
        {
            decision = null;
            error = "";
            try
            {
                if (string.IsNullOrWhiteSpace(json) || json.Length > 8192)
                    throw new FormatException("Response is empty or too large.");
                var parsed = new DecisionReader(json).Read();
                if (string.IsNullOrWhiteSpace(parsed.reply))
                    throw new FormatException("The reply must contain dialogue.");
                if (state == null || (npcId != "guard" && npcId != "merchant" && npcId != "knight"))
                    throw new FormatException("Unrecognized character or missing player state.");

                switch (parsed.action)
                {
                    case "none": parsed.Action = NpcAction.None; break;
                    case "check_pass" when npcId == "guard": parsed.Action = NpcAction.CheckPass; break;
                    case "request_purchase" when npcId == "merchant":
                        if (string.IsNullOrWhiteSpace(parsed.itemId) || parsed.quantity <= 0 ||
                            state.catalog == null || !state.catalog.Any(i => i != null && i.itemId == parsed.itemId))
                            throw new FormatException("Purchase requires a catalog item and a positive quantity.");
                        parsed.Action = NpcAction.RequestPurchase;
                        break;
                    case "offer_training" when npcId == "knight" && state.trainingStatus == "available":
                        parsed.Action = NpcAction.OfferTraining;
                        break;
                    case "review_training" when npcId == "knight" &&
                        (state.trainingStatus == "active" || state.trainingStatus == "ready"):
                        parsed.Action = NpcAction.ReviewTraining;
                        break;
                    default:
                        throw new FormatException("This action is unavailable to this character in the current state.");
                }

                if (parsed.Action != NpcAction.RequestPurchase && (parsed.quantity != 0 || parsed.itemId != ""))
                    throw new FormatException("Unused item fields must be empty and quantity zero.");
                bool training = parsed.Action == NpcAction.OfferTraining || parsed.Action == NpcAction.ReviewTraining;
                if (training && (parsed.questId != "courtyard_drill" || state.questId != parsed.questId))
                    throw new FormatException("Training requires the current quest ID.");
                if (!training && parsed.questId != "")
                    throw new FormatException("Unused quest ID must be empty.");

                decision = parsed;
                return true;
            }
            catch (FormatException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // JsonUtility ignores unknown/duplicate properties and silently defaults missing fields.
        // Read this flat wire contract explicitly, including JSON escapes and integer syntax.
        private sealed class DecisionReader
        {
            private readonly string source;
            private int index;
            private char Current => index < source.Length ? source[index] : '\0';
            public DecisionReader(string source) { this.source = source; }

            public NpcDecision Read()
            {
                var fields = new HashSet<string>(StringComparer.Ordinal);
                var decision = new NpcDecision();
                Expect('{');
                do
                {
                    var name = ReadString();
                    if (!fields.Add(name)) throw Invalid();
                    Expect(':');
                    switch (name)
                    {
                        case "reply": decision.reply = ReadString(); break;
                        case "action": decision.action = ReadString(); break;
                        case "itemId": decision.itemId = ReadString(); break;
                        case "quantity": decision.quantity = ReadInteger(); break;
                        case "questId": decision.questId = ReadString(); break;
                        default: throw Invalid();
                    }
                    SkipWhitespace();
                    if (Current == '}') break;
                    Expect(',');
                } while (true);
                Expect('}');
                SkipWhitespace();
                if (index != source.Length || fields.Count != 5) throw Invalid();
                return decision;
            }

            private int ReadInteger()
            {
                SkipWhitespace();
                int start = index;
                if (Current == '-') index++;
                if (Current == '0') index++;
                else
                {
                    if (Current < '1' || Current > '9') throw Invalid();
                    while (Current >= '0' && Current <= '9') index++;
                }
                if (!int.TryParse(source.Substring(start, index - start), NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out var value)) throw Invalid();
                return value;
            }

            private string ReadString()
            {
                Expect('"');
                var value = new StringBuilder();
                while (index < source.Length)
                {
                    char c = source[index++];
                    if (c == '"') return value.ToString();
                    if (c < 0x20) throw Invalid();
                    if (c == '\\')
                    {
                        if (index >= source.Length) throw Invalid();
                        switch (c = source[index++])
                        {
                            case '\\': case '/': case '"': break;
                            case 'b': c = '\b'; break;
                            case 'f': c = '\f'; break;
                            case 'n': c = '\n'; break;
                            case 'r': c = '\r'; break;
                            case 't': c = '\t'; break;
                            case 'u':
                                if (index + 4 > source.Length ||
                                    !ushort.TryParse(source.Substring(index, 4), NumberStyles.AllowHexSpecifier,
                                        CultureInfo.InvariantCulture, out var code)) throw Invalid();
                                c = (char)code;
                                index += 4;
                                break;
                            default: throw Invalid();
                        }
                    }
                    value.Append(c);
                }
                throw Invalid();
            }

            private void Expect(char expected)
            {
                SkipWhitespace();
                if (Current != expected) throw Invalid();
                index++;
            }

            private void SkipWhitespace()
            {
                while (Current == ' ' || Current == '\t' || Current == '\r' || Current == '\n') index++;
            }

            private static FormatException Invalid() =>
                new FormatException("Expected one JSON object with exactly reply, action, itemId, quantity and questId of the required types.");
        }
    }
}
