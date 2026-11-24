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
            for (int i = 0; i < 420 && !arrived(); i++)
            {
                StepToward(goal);
                if (i % 8 == 7) yield return null;
            }
            Assert.That(arrived(), Is.True,
                $"Walk to {destination} blocked at {Eye.transform.position}; goal {goal}, near {Panel.Nearby?.RoleId}.");
        }

        protected IEnumerator Approach(string role)
        {
            var actor = Actor(role);
            var shape = role == "knight"
                ? actor.transform.Find("Horse barrel").GetComponent<CapsuleCollider>()
                : actor.GetComponent<CapsuleCollider>();
            yield return WalkTo(shape.bounds.center, () => Panel.Nearby == actor, role);
        }

        protected IEnumerator ReturnToSpawn()
        {
            yield return WalkTo(Spawn.position, () => Flat(Spawn.position - Eye.transform.position).magnitude < .14f, "spawn");
            Assert.That(Panel.Nearby, Is.Null);
        }

        protected void Begin(string role)
        {
            Panel.BeginNearby(Eye.transform);
            Assert.That(Panel.InConversation, Is.True, role);
            Assert.That(Panel.SelectedRole, Is.EqualTo(Array.IndexOf(NpcDemoScenario.Roles, role)));
        }

        protected static IEnumerator Observe(float seconds, Action sample)
        {
            float until = Time.unscaledTime + seconds;
            do { yield return null; sample(); } while (Time.unscaledTime < until);
        }
    }
}
