using MedievalCastle.Npc;
using UnityEngine;

namespace MedievalCastle.NPC
{
    [DisallowMultipleComponent]
    public sealed class CastleNpcCharacter : MonoBehaviour
    {
        [SerializeField] private string roleId;
        [SerializeField] private Transform chest, head, jaw, gestureArm, offArm;
        [SerializeField] private Transform horseChest, earL, earR;

        private Quaternion restHead, restChestRotation, restArm, restOffArm, restEarL, restEarR;
        private Vector3 restChest, restJaw, restBarrel;
        private Transform listener;
        private Vector3? trainingTarget;
        private Vector2 gaze;
        private float phase, speakingWeight, replyStart;
        private float greetingStart = -10f;
        private bool speaking, listening;
        private NpcAction action;

        public string RoleId => roleId;

        private void Awake()
        {
            restChest = chest.localPosition;
            restChestRotation = chest.localRotation;
            restHead = head.localRotation;
            restJaw = jaw.localPosition;
            restArm = gestureArm.localRotation;
            if (offArm) restOffArm = offArm.localRotation;
            if (horseChest)
            {
                restBarrel = horseChest.localScale;
                restEarL = earL.localRotation;
                restEarR = earR.localRotation;
            }
            phase = roleId == "guard" ? .4f : (roleId == "merchant" ? 1.7f : 2.8f);
        }

        public void Greet(Transform viewer)
        {
            listener = viewer;
            speaking = listening = false;
            greetingStart = Time.unscaledTime;
            action = NpcAction.None;
        }

        public void Listen(Transform viewer)
        {
            listener = viewer;
            speaking = false;
            listening = true;
        }

        public void Speak(NpcAction decision, Transform viewer, Vector3? trainingTarget = null)
        {
            listener = viewer;
            this.trainingTarget = trainingTarget;
            action = decision;
            replyStart = Time.unscaledTime;
            speaking = true;
            listening = false;
        }

        // Retain the existing entry point for gameplay components that only have a speaking flag.
        public void SetSpeaking(bool value)
        {
            if (value)
            {
                var camera = Camera.main;
                Speak(NpcAction.None, listener ? listener : (camera ? camera.transform : null));
            }
            else EndConversation();
        }

        public void EndConversation()
        {
            listening = speaking = false;
            greetingStart = -10f;
        }

        private void LateUpdate()
        {
            float now = Time.unscaledTime;
            float t = now + phase;
            float progress = Mathf.Clamp01((now - greetingStart) / 1.05f);
            // Quick acknowledgement, a readable hold, then ease back to the resting pose.
            float greeting = Mathf.SmoothStep(0, 1, progress / .27f) *
                (1f - Mathf.SmoothStep(0, 1, (progress - .49f) / .51f));
            float nod = Mathf.Sin(Mathf.PI * progress) * greeting;
            speakingWeight = Mathf.MoveTowards(speakingWeight, speaking ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            greeting *= 1f - speakingWeight;
            nod *= 1f - speakingWeight;
            float breath = Mathf.Sin(t * 1.6f);
            float beat = .5f + .5f * Mathf.Sin(t * 3.6f);
            float voice = speakingWeight;
            Vector3 chestAngles = Vector3.zero, headAngles = Vector3.zero, armAngles = Vector3.zero;
            float bookLift = 0;
            Vector3? lookAt = listener ? (Vector3?)listener.position : null;
            float attention = listening || speaking ? 1f : greeting;

            if (roleId == "guard")
            {
                bool checking = action == NpcAction.CheckPass;
                headAngles.x = nod * 14f + voice * (checking ? 11f : 4f) * (.55f + .45f * beat);
                armAngles.x = -voice * (checking ? 47f : 8f) * (.82f + .18f * beat);
                armAngles.z = -voice * (checking ? 6f : 2f);
                if (checking && speaking && lookAt.HasValue) lookAt = lookAt.Value - Vector3.up * .4f;
            }
            else if (roleId == "merchant")
            {
                bool order = action == NpcAction.RequestPurchase;
                float elapsed = Mathf.Clamp01((now - replyStart) / 1.1f);
                float confirm = order ? Mathf.Sin(Mathf.PI * elapsed) : 0f;
                chestAngles.x = 6f * greeting + 2.5f * voice * beat;
                headAngles.x = 5f * nod + voice * (3f * beat + 7f * confirm);
                armAngles.x = -25f * greeting - voice * (8f + 5f * beat + 17f * confirm);
                armAngles.z = 9f * greeting + voice * (5f * beat + 7f * confirm);
                bookLift = -voice * (order ? 4f : 11f) * (.78f + .22f * beat);
            }
            else if (horseChest)
            {
                bool signal = speaking && action == NpcAction.OfferTraining && trainingTarget.HasValue;
                if (signal) lookAt = trainingTarget;
                // Keep the hands inside the reins while the head leads the turn.
                chestAngles.y = .042f * gaze.x * voice;
                headAngles.x = 11f * nod + voice * (action == NpcAction.ReviewTraining ? 10f : 3f) * beat;
                AnimateMount(t, greeting);
            }
            chest.localPosition = restChest + Vector3.up * (.003f * breath);
            chest.localRotation = restChestRotation * Quaternion.Euler(chestAngles);
            UpdateGaze(lookAt, attention);
            headAngles += new Vector3(gaze.y + .5f * breath, gaze.x + 1.1f * Mathf.Sin(t * .64f), 0);
            head.localRotation = restHead * Quaternion.Euler(headAngles);
            float syllable = Mathf.Abs(Mathf.Sin(t * 11.2f) * Mathf.Cos(t * 2.3f));
            jaw.localPosition = restJaw - Vector3.up * (.006f * syllable * voice);
            gestureArm.localRotation = restArm * Quaternion.Euler(armAngles);
            if (offArm) offArm.localRotation = restOffArm * Quaternion.Euler(bookLift, 0, 0);
        }

        private void UpdateGaze(Vector3? point, float weight)
        {
            Vector2 target = Vector2.zero;
            if (point.HasValue && weight > 0)
            {
                var direction = Quaternion.Inverse(restHead) * head.parent.InverseTransformDirection(
                    (point.Value - head.position).normalized);
                float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                float pitch = -Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
                target = new Vector2(Mathf.Clamp(yaw,-35f,35f), Mathf.Clamp(pitch,-13f,17f)) * weight;
            }
            gaze = Vector2.MoveTowards(gaze, target, Time.unscaledDeltaTime * 110f);
        }

        private void AnimateMount(float time, float greeting)
        {
            float breath = Mathf.Sin(time * 1.24f);
            horseChest.localScale = Vector3.Scale(restBarrel, new Vector3(1f + .005f * breath, 1f + .003f * breath, 1f));
            float left = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(time * 1.32f)), 10f);
            float right = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(time * 1.04f + 1.4f)), 12f);
            earL.localRotation = restEarL * Quaternion.Euler(4f * left + 15f * greeting, 0, 7f * left - 5f * greeting);
            earR.localRotation = restEarR * Quaternion.Euler(5f * right + 14f * greeting, 0, -8f * right + 5f * greeting);
        }
    }
}
