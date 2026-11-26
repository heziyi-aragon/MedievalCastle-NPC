using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MedievalCastle.Npc.Demo.PlayModeTests
{
    public sealed class GuardAttentionTests : CastleWalkFixture
    {
        [UnityTest, Category("SimulatedXR")]
        public IEnumerator ACanGreetFromEitherSideOrBehindTheGuardWithBothCardsVisible()
        {
            var guard = Actor("guard");
            var front = Flat(guard.transform.forward).normalized;
            var head = Joint(guard, "Head");
            Panel.enabled = false;
            var probe = new GameObject("Tracked visitor").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(probe.gameObject, Castle);
            probe.enabled = false;
            foreach (float degrees in new[] { 0f, 90f, 180f, -90f })
            {
                var side = Quaternion.AngleAxis(degrees, Vector3.up) * front;
                // Sample the approach until the production proximity prompt names the guard.
                for (int step = 0; step <= 8; step++)
                {
                    probe.transform.position = guard.transform.position + side * (1.22f - .077f * step) + Vector3.up * 1.75f;
                    probe.transform.LookAt(head.position);
                    Panel.HandleHeadsetFrame(probe, true, false, false, 0);
                    if (Panel.Nearby == guard) break;
                }
                Assert.That(Panel.Nearby, Is.SameAs(guard), "No guard prompt approaching from " + degrees);
                Panel.HandleHeadsetFrame(probe, true, true, false, 0);
                Assert.That(Panel.SelectedRole, Is.Zero, "Unable to greet guard from " + degrees);
                Assert.That(Panel.transform.Find("NPC questions").gameObject.activeSelf, Is.True);
                yield return WaitForCaption(() => Panel.HandleHeadsetFrame(probe, true, false, false, 0));
                Assert.That(Panel.transform.Find("NPC speech").gameObject.activeSelf, Is.True);
                yield return new WaitForSecondsRealtime(1.25f);
                var toEye = probe.transform.position - head.TransformPoint(0f, .168f, .096f);
                Assert.That(Vector3.Angle(head.forward, toEye), Is.LessThan(12f), "Gaze from " + degrees);
                Panel.EndTalk();
            }
        }

        [UnityTest]
        public IEnumerator GuardTurnsToTheVisitorsEyesWelcomesAndLooksBackAfterInspectingThePass()
        {
            var guard = Actor("guard");
            var head = Joint(guard, "Head");
            var hand = Joint(guard, "GestureArm");
            var figure = Joint(guard, "Legs").parent;
            var body = guard.GetComponent<CapsuleCollider>();
            var home = figure.localRotation;
            var front = Vector3.ProjectOnPlane(figure.forward, Vector3.up).normalized;
            var colliderPose = body.transform.localToWorldMatrix;
            var visitor = new GameObject("Visitor eye").transform;
            SceneManager.MoveGameObjectToScene(visitor.gameObject, Castle);
            Vector3 FaceToViewer() => (visitor.position - head.TransformPoint(0f, .168f, .096f)).normalized;
            var handRest = hand.localRotation;
            foreach (float approachAngle in new[] { 0f, 90f, -90f, 180f })
            {
                var side = Quaternion.AngleAxis(approachAngle, Vector3.up) * front;
                visitor.position = guard.transform.position + side * 1.18f + Vector3.up * 1.75f;
                guard.Greet(visitor);
                float lift = 0f;
                yield return Observe(.85f, () => lift = Mathf.Max(lift, Quaternion.Angle(handRest, hand.localRotation)));
                Assert.That(lift, Is.GreaterThan(23f), "Welcome should lift the empty hand");
                guard.Listen(visitor);
                yield return new WaitForSecondsRealtime(1.15f);
                Assert.That(Vector3.Angle(FaceToViewer(), head.forward), Is.LessThan(9f), "Arrival bearing " + approachAngle);
                Assert.That(Vector3.Angle(Flat(figure.forward), side), Is.LessThan(8f), "Feet should follow the gaze");
                Assert.That(body.transform.localToWorldMatrix, Is.EqualTo(colliderPose), "Keep the interaction root stationary");
            }
            guard.Speak(NpcAction.CheckPass, visitor);
            yield return new WaitForSecondsRealtime(.57f);
            Assert.That(Vector3.Dot(head.forward - FaceToViewer(), Vector3.down), Is.GreaterThan(.17f),
                "Briefly look to the visitor's hand");
            yield return new WaitForSecondsRealtime(.99f);
            Assert.That(Vector3.Angle(head.forward, FaceToViewer()), Is.LessThan(9f), "Reestablish eye contact");
            visitor.position -= Vector3.up * .32f;
            guard.Listen(visitor);
            yield return new WaitForSecondsRealtime(.52f);
            Assert.That(Vector3.Angle(head.forward, FaceToViewer()), Is.LessThan(8f), "Track a seated visitor leaning down");
            guard.EndConversation();
            yield return new WaitForSecondsRealtime(1.75f);
            Assert.That(Quaternion.Angle(figure.localRotation, home), Is.LessThan(2f));
        }
    }
}
