using System;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace MedievalCastle.Npc.Tests
{
    public sealed class NpcProviderTests
    {
        [Serializable] private sealed class WireRequest
        {
            public string model = null;
            public WireOption response_format = null;
            public WireOption thinking = null;
            public NpcChatMessage[] messages = null;
            public bool stream = true;
            public int max_tokens = 0;
        }
        [Serializable] private sealed class WireOption { public string type = null; }

        [Test]
        public void RequestEncodesModelRoleMessagesAndNonStreamingJsonMode()
        {
            const string message = "他说：\"hello\"。\n🛡";
            var request = new NpcProviderRequest {
                Model = "test-model", Messages = new [] {
                    new NpcChatMessage("system", "Respond in JSON."), new NpcChatMessage("user", message)
                }
            };
            var json = DeepSeekNpcProvider.SerializeRequest(request);
            Assert.That(json, Does.Contain("\"stream\":false"));
            var wire = JsonUtility.FromJson<WireRequest>(json);
            Assert.That(wire.model, Is.EqualTo("test-model"));
            Assert.That(wire.response_format.type, Is.EqualTo("json_object"));
            Assert.That(wire.thinking.type, Is.EqualTo("disabled"));
            Assert.That(wire.stream, Is.False);
            Assert.That(wire.max_tokens, Is.GreaterThan(0));
            Assert.That(wire.messages.Length, Is.EqualTo(2));
            Assert.That(wire.messages[0].role, Is.EqualTo("system"));
            Assert.That(wire.messages[1].role, Is.EqualTo("user"));
            Assert.That(wire.messages[1].content, Is.EqualTo(message));
        }

        [Test]
        public void ResponseReturnsOnlyAssistantContent()
        {
            var envelope = "{\"model\":\"example\",\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"{\\\"reply\\\":\\\"你好\\\"}\",\"reasoning_content\":\"hidden\"}}]}";
            Assert.That(DeepSeekNpcProvider.DecodeResponse(200, envelope, false), Is.EqualTo("{\"reply\":\"你好\"}"));
        }

        [TestCase("{}")]
        [TestCase("{\"choices\":[]}")]
        [TestCase("{\"choices\":[{\"finish_reason\":\"length\",\"message\":{\"content\":\"partial\"}}]}")]
        [TestCase("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"  \"}}]}")]
        [TestCase("broken envelope")]
        public void EmptyOrInterruptedCompletionFails(string envelope)
        {
            Assert.Throws<NpcProviderException>(() => DeepSeekNpcProvider.DecodeResponse(200, envelope, false));
        }

        [TestCase(401, "authentication")]
        [TestCase(402, "balance")]
        [TestCase(429, "rate limit")]
        [TestCase(500, "500")]
        public void ServiceErrorsDoNotExposeBody(long status, string expected)
        {
            var error = Assert.Throws<NpcProviderException>(() => DeepSeekNpcProvider.DecodeResponse(status, "private-account-detail", false));
            Assert.That(error.Message, Does.Contain(expected).IgnoreCase);
            Assert.That(error.Message, Does.Not.Contain("private-account-detail"));
        }

        [TestCase("I want to buy five bread.", 5)]
        [TestCase("I would like to buy one loaf of bread.", 1)]
        [TestCase("buy 23 loaves of bread", 23)]
        [TestCase("buy twenty-one bread", 21)]
        [TestCase("buy bread", 1)]
        [TestCase("买十个面包", 10)]
        [TestCase("我要买二十三个面包。", 23)]
        [TestCase("buy several bread", 0)]
        [TestCase("buy a few pieces of bread", 0)]
        [TestCase("buy two or three bread", 0)]
        [TestCase("buy -1 bread", 0)]
        [TestCase("buy 2.5 bread", 0)]
        [TestCase("我想买很多个面包。", 0)]
        [TestCase("我想买五六个面包。", 0)]
        public void OfflineMerchantRecognizesWholeQuantitiesOrClarifies(string message, int expected)
        {
            var request = new NpcProviderRequest {
                NpcId = "merchant", Language = "en", PlayerText = message,
                State = new NpcPlayerState { catalog = new[] { new NpcCatalogItem { itemId = "bread", price = 2 } } }
            };
            string json = new NpcOfflineProvider().GenerateAsync(request, CancellationToken.None).Result;
            Assert.That(NpcDecisionValidator.TryParse(json, "merchant", request.State, out var decision, out var error), Is.True, error);
            Assert.That(decision.quantity, Is.EqualTo(expected));
            Assert.That(decision.Action, Is.EqualTo(expected == 0 ? NpcAction.None : NpcAction.RequestPurchase));
        }
    }
}
