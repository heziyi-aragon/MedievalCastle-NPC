using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MedievalCastle.Npc.Demo.PlayModeTests
{
    public sealed class NpcWorldCardsTests : CastleWalkFixture
    {
        [UnityTest, Category("Desktop")]
        public IEnumerator EachSpeakerHasAStationarySideCardAndASeparateCaptionAboveTheSilhouette()
        {
            foreach (string role in NpcDemoScenario.Roles)
            {
                var actor = Actor(role);
                yield return Approach(role);
                Panel.HandleDesktopFrame(Eye, true, false);
                yield return WaitForCaption();
                var choices = (RectTransform)Panel.transform.Find("NPC questions");
                var caption = (RectTransform)Panel.transform.Find("NPC speech");
                Canvas.ForceUpdateCanvases();
                Assert.That(choices.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
                Assert.That(caption.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
                Assert.That(choices.IsChildOf(Eye.transform) || caption.IsChildOf(Eye.transform), Is.False);
                Assert.That(caption.Find("Reply").GetComponent<Text>().text, Is.EqualTo(Panel.SpokenLine).And.Not.Empty);
                Assert.That(caption.position.y, Is.GreaterThan(actor.GetComponentsInChildren<Renderer>().Max(r => r.bounds.max.y)));
                var corners = new Vector3[4];
                foreach (var card in new[] { choices, caption })
                {
                    card.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var viewport = Eye.WorldToViewportPoint(corner);
                        Assert.That(viewport.x, Is.InRange(.03f, .97f), role + " horizontal card margin");
                        Assert.That(viewport.y, Is.InRange(.03f, .97f), role + " vertical card margin");
                    }
                }
                var relative = Flat(choices.position - Joint(actor, "Head").position);
                Assert.That(relative.magnitude, Is.GreaterThan(.45f), role + " choice panel at side");
                var extents = Vector3.Scale(choices.rect.size * .5f, choices.lossyScale);
                Assert.That(Physics.CheckBox(choices.position, new Vector3(extents.x, extents.y, .014f), choices.rotation,
                    ~0, QueryTriggerInteraction.Ignore), Is.False, role + " panel cuts a physical object");

                var anchor = choices.position;
                var top = caption.position;
                var originalRotation = Eye.transform.rotation;
                Eye.transform.Rotate(0, 28, 0, Space.World);
                Panel.HandleDesktopFrame(Eye, false, false);
                Assert.That(choices.position, Is.EqualTo(anchor));
                Assert.That(caption.position, Is.EqualTo(top));
                Eye.transform.rotation = originalRotation;
                Panel.HandleDesktopFrame(Eye, false, true);
                Assert.That(choices.gameObject.activeSelf, Is.False);
                Panel.HandleDesktopFrame(Eye, false, true);
                yield return null;
                // GraphicRaycaster ignores graphics without a rendered depth. Batch Play mode
                // has no Game View being presented; render one real frame before clicking.
                Canvas.ForceUpdateCanvases();
                if (Application.isBatchMode)
                {
                    var previous = Eye.targetTexture;
                    var target = RenderTexture.GetTemporary(Screen.width, Screen.height, 24);
                    try { Eye.targetTexture = target; Eye.Render(); }
                    finally { Eye.targetTexture = previous; RenderTexture.ReleaseTemporary(target); }
                }
                var option = (RectTransform)choices.Find("Option 1");
                var pixel = Eye.WorldToScreenPoint(option.TransformPoint(option.rect.center));
                var pointer = new PointerEventData(EventSystem.current) { position = pixel, button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>();
                choices.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
                var hit = hits.FirstOrDefault();
                var image = option.GetComponent<Image>();
                Assert.That(hit.gameObject, Is.Not.Null, $"{role} question screen={pixel} size={Screen.width}x{Screen.height} " +
                    $"depth={image.depth} registered={GraphicRegistry.GetRaycastableGraphicsForCanvas(choices.GetComponent<Canvas>()).Count} " +
                    $"rectHit={RectTransformUtility.RectangleContainsScreenPoint(option, pixel, Eye)}");
                ExecuteEvents.ExecuteHierarchy(hit.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                Assert.That(Panel.LastTurn?.Success, Is.True);
                yield return WaitForCaption();
                Assert.That(caption.Find("Reply").GetComponent<Text>().text, Does.Contain(Panel.LastTurn.Reply.Substring(0, 8)));
                Panel.EndTalk();
                Assert.That(choices.gameObject.activeSelf || caption.gameObject.activeSelf, Is.False);
                yield return ReturnToSpawn();
            }
        }

        [UnityTest]
        public IEnumerator ANewConversationPlacesChoicesOnTheClearSideOfAWall()
        {
            yield return Approach("guard");
            Panel.HandleDesktopFrame(Eye, true, false);
            var card = (RectTransform)Panel.transform.Find("NPC questions");
            var previous = card.position;
            Panel.EndTalk();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(wall, Castle);
            wall.transform.position = previous;
            wall.transform.localScale = new Vector3(.26f, .82f, .26f);
            Physics.SyncTransforms();
            Panel.HandleDesktopFrame(Eye, true, false);
            Assert.That(Panel.InConversation, Is.True);
            Assert.That(Flat(card.position - previous).magnitude, Is.GreaterThan(.45f));
            Assert.That(wall.GetComponent<Collider>().bounds.Contains(card.position), Is.False);
            Panel.EndTalk();
        }
    }
}
