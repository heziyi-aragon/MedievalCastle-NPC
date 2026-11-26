using System;
using System.Collections;
using System.Linq;
using MedievalCastle.NPC;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MedievalCastle.Npc.Demo.PlayModeTests
{
    public abstract class CastleWalkFixture
    {
        protected NpcDemoPanel Panel;
        protected NpcDialogueService Service;
        protected Camera Eye;
        protected CastlePlayerMotor Motor;
        protected Transform Spawn;
        protected Scene Castle;
        protected const string LanguageKey = "CastleNPC.Language";
        private bool hadLanguage;
        private string savedLanguage;

        [UnitySetUp]
        public IEnumerator LoadCastle()
        {
            hadLanguage = PlayerPrefs.HasKey(LanguageKey);
            savedLanguage = PlayerPrefs.GetString(LanguageKey);
            PlayerPrefs.DeleteKey(LanguageKey);
            CastlePlayerMotor.MovementLocked = false;

            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Castle_Exterior.unity", LoadSceneMode.Additive);
            Castle = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            var roots = Castle.GetRootGameObjects();
            Panel = roots.SelectMany(r => r.GetComponentsInChildren<NpcDemoPanel>()).Single();
            Spawn = Panel.transform.Find("Quest floor position");
            Eye = roots.SelectMany(r => r.GetComponentsInChildren<Camera>())
                .Single(c => c.CompareTag("MainCamera"));
            Service = Panel.GetComponent<NpcDialogueService>();
            Assert.That(Panel.Chinese, Is.False, "A first launch must default to English.");
            Assert.That(Service.Mode, Is.EqualTo(NpcProviderMode.DeepSeek), "The player-facing scene must default to live dialogue.");
            Service.Mode = NpcProviderMode.Offline;
            Panel.SetSpeechProviderForTesting((_, __, ___) => System.Threading.Tasks.Task.FromResult(TestVoice()));
            float deadline = Time.realtimeSinceStartup + 4f;
            do
            {
                yield return null;
                Motor = Eye.GetComponentInParent<CastlePlayerMotor>();
            } while (!Motor && Time.realtimeSinceStartup < deadline);
            Assert.That(Motor, Is.Not.Null, "The production first-person controller must start in Castle_Exterior.");
            Assert.That(Motor.GetComponent<CharacterController>(), Is.Not.Null);
            Assert.That(Flat(Eye.transform.position - Spawn.position).magnitude, Is.LessThan(.25f));
            Assert.That(Panel.MenuOpen || Panel.InConversation, Is.False);
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator UnloadCastle()
        {
            if (Service) Service.Cancel();
            if (Castle.IsValid() && Castle.isLoaded) yield return SceneManager.UnloadSceneAsync(Castle);
            CastlePlayerMotor.MovementLocked = false;
            if (hadLanguage) PlayerPrefs.SetString(LanguageKey, savedLanguage);
            else PlayerPrefs.DeleteKey(LanguageKey);
            PlayerPrefs.Save();
        }

        protected CastleNpcCharacter Actor(string role) =>
            Panel.GetComponentsInChildren<CastleNpcCharacter>().Single(a => a.RoleId == role);

        protected static Transform Joint(CastleNpcCharacter actor, string name) =>
            actor.GetComponentsInChildren<Transform>().Single(t => t.name == name);

        protected static Vector3 Flat(Vector3 v) => Vector3.ProjectOnPlane(v, Vector3.up);

        protected void StepToward(Vector3 target)
        {
            var delta = Flat(target - Eye.transform.position).normalized;
            var forward = Flat(Eye.transform.forward).normalized;
            var right = Vector3.Cross(Vector3.up, forward);
            Motor.Step(new Vector2(Vector3.Dot(delta, right), Vector3.Dot(delta, forward)), 0f, 1f / 60f);
            Panel.UpdateNearby(Eye.transform);
        }

        protected IEnumerator WalkTo(Vector3 goal, Func<bool> arrived, string destination)
        {
            Physics.SyncTransforms();
            Panel.UpdateNearby(Eye.transform);
            // Every step goes through the same CharacterController.Move path as desktop/XR input.
            // Allow time for acceleration, uneven ground and render frames between input samples.
            float distance = Flat(goal - Eye.transform.position).magnitude;
            int stepLimit = Mathf.CeilToInt((distance / 1.6f * 1.7f + 2f) * 60f);
            for (int i = 0; i < stepLimit && !arrived(); i++)
            {
                StepToward(goal);
                if (i % 8 == 7) yield return null;
            }
            Assert.That(arrived(), Is.True,
                $"Walk to {destination} blocked at {Eye.transform.position}; goal {goal}, near {Panel.Nearby?.RoleId}.");
        }

        protected static AudioClip TestVoice()
        {
            // A changing envelope lets tests observe the animated jaw without network calls.
            const int sampleRate = 8000;
            var samples = new float[sampleRate * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / sampleRate;
                samples[i] = .13f * (.5f + .5f * Mathf.Sin(17f * t)) * Mathf.Sin(990f * t);
            }
            var clip = AudioClip.Create("Test voice", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // From the gate, walk along the wall outside the checkpoint barrels and the yard fence.
        // Approach the flank of the horse from the open end of the fence, as a player would.
        private Vector3 GateApproach => new Vector3(Spawn.position.x, 0f, 27.25f);
        private static readonly Vector3 FenceEndApproach = new Vector3(-8.95f, 0f, 27.25f);

        private IEnumerator WalkPast(Vector3 point, string landmark) =>
            WalkTo(point, () => Flat(point - Eye.transform.position).magnitude < .12f, landmark);

        protected IEnumerator Approach(string role)
        {
            var actor = Actor(role);
            if (role == "knight")
            {
                yield return WalkPast(GateApproach, "the forecourt outside the gate");
                yield return WalkPast(FenceEndApproach, "the wall side of the training fence");
            }
            var shape = role == "knight"
                ? actor.transform.Find("Horse barrel").GetComponent<CapsuleCollider>()
                : actor.GetComponent<CapsuleCollider>();
            yield return WalkTo(shape.bounds.center, () => Panel.Nearby == actor, role);
        }

        protected IEnumerator ReturnToSpawn()
        {
            if (Flat(Eye.transform.position - Actor("knight").transform.position).magnitude < 4f)
            {
                yield return WalkPast(FenceEndApproach, "the outside of the fence on the return path");
                yield return WalkPast(GateApproach, "the forecourt on the return path");
            }
            yield return WalkTo(Spawn.position, () => Flat(Spawn.position - Eye.transform.position).magnitude < .14f, "spawn");
            Assert.That(Panel.Nearby, Is.Null);
        }

        protected void Begin(string role)
        {
            Panel.BeginNearby(Eye.transform);
            Assert.That(Panel.InConversation, Is.True, role);
            Assert.That(Panel.SelectedRole, Is.EqualTo(Array.IndexOf(NpcDemoScenario.Roles, role)));
        }

        protected IEnumerator WaitForCaption(Action renderFrame = null)
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while (Panel.SpokenLine.Length == 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if (!Panel.enabled) Panel.UpdateVoice();
                renderFrame?.Invoke();
            }
            Assert.That(Panel.SpokenLine, Is.Not.Empty, "A playing line must show its subtitle at audible onset.");
        }

        protected static IEnumerator Observe(float seconds, Action sample)
        {
            float until = Time.unscaledTime + seconds;
            do { yield return null; sample(); } while (Time.unscaledTime < until);
        }
    }
}
