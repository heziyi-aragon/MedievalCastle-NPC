using MedievalCastle.Npc;
using UnityEngine;

namespace MedievalCastle.NPC
{
    [DisallowMultipleComponent]
    public sealed class CastleNpcCharacter : MonoBehaviour
    {
        [SerializeField] private string roleId;
        [SerializeField] private Transform chest, head, jaw, gestureArm, offArm;
        [SerializeField] private Transform horseChest, earL, earR, horseTail;

        private Quaternion restHead, restChestRotation, restArm, restOffArm, restEarL, restEarR, restTail;
        private Vector3 restChest, restJaw, restBarrel;
        private Transform listener;
        private Vector3? trainingTarget;
        private Transform trainingFocus;
        private Transform guardFigure;
        private Quaternion restFigure;
        private float figureYaw, turnVelocity, mouthOpening;
        private bool animatedByAudio;
        private float phase, speakingWeight, attention, replyStart;
        private float greetingStart = -10f;
        private bool speaking, engaged;
        private NpcAction action;

        public string RoleId => roleId;
        public Transform VoiceAnchor => jaw;

        private void Awake()
        {
            restChest = chest.localPosition;
            restChestRotation = chest.localRotation;
            restHead = head.localRotation;
            restJaw = jaw.localPosition;
            restArm = gestureArm.localRotation;
            if (roleId == "guard")
            {
                // The model's Figure carries both legs and torso, beneath the stationary interaction root.
                guardFigure = chest.parent;
                restFigure = guardFigure.localRotation;
            }
            if (offArm) restOffArm = offArm.localRotation;
            if (horseChest)
            {
                restBarrel = horseChest.localScale;
                restEarL = earL.localRotation;
                restEarR = earR.localRotation;
                if (horseTail) restTail = horseTail.localRotation;
            }
            phase = roleId == "guard" ? .4f : (roleId == "merchant" ? 1.7f : 2.8f);
        }

        public void Greet(Transform viewer)
        {
            listener = viewer;
            speaking = false;
            engaged = true;
            greetingStart = Time.unscaledTime;
            if (guardFigure && viewer)
            {
                float bearing = Vector3.Angle(Vector3.ProjectOnPlane(guardFigure.forward, Vector3.up),
                    Vector3.ProjectOnPlane(viewer.position - guardFigure.position, Vector3.up));
                // If approached from behind, bring the face into view before the peak of the salute.
                greetingStart += .40f * Mathf.InverseLerp(45f, 165f, bearing);
            }
            action = NpcAction.None;
        }

        public void Listen(Transform viewer)
        {
            listener = viewer;
            speaking = false;
            engaged = true;
        }

        public void Speak(NpcAction decision, Transform viewer, Vector3? trainingTarget = null)
        {
            listener = viewer;
            this.trainingTarget = trainingTarget;
            action = decision;
            replyStart = Time.unscaledTime;
            speaking = true;
            engaged = true;
        }

        public void SetTrainingFocus(Transform focus) => trainingFocus = focus;

        // Audio playback can drive the jaw independently of a greeting or a decision gesture.
        public void SetMouthOpening(float normalized)
        {
            animatedByAudio = true;
            mouthOpening = Mathf.Clamp01(normalized);
            jaw.localPosition = restJaw - Vector3.up * (.008f * mouthOpening);
        }

        // Retain the existing entry point for gameplay components that only have a speaking flag.
        public void SetSpeaking(bool value)
        {
            animatedByAudio = false;
            if (value)
            {
                var camera = Camera.main;
                Speak(NpcAction.None, listener ? listener : (camera ? camera.transform : null));
            }
            else EndConversation();
        }

        public void EndConversation()
        {
            engaged = speaking = false;
            animatedByAudio = false;
            mouthOpening = speakingWeight = 0f;
            jaw.localPosition = restJaw;
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
            float dt = Time.unscaledDeltaTime;
            attention = Mathf.MoveTowards(attention, engaged ? 1f : 0f, dt / .30f);
            float idle = 1f - Mathf.SmoothStep(0, 1, attention);
            speakingWeight = Mathf.MoveTowards(speakingWeight, speaking ? 1f : 0f, dt / .27f);
            greeting *= 1f - speakingWeight;
            nod *= 1f - speakingWeight;
            float breath = Mathf.Sin(t * 1.6f);
            float beat = .5f + .5f * Mathf.Sin(t * 3.6f);
            float voice = speakingWeight;
            Vector3 chestAngles = Vector3.zero, headAngles = Vector3.zero, armAngles = Vector3.zero;
            Vector3 heldArmAngles = Vector3.zero;
            Vector3? lookAt = listener ? (Vector3?)listener.position : null;

            if (roleId == "guard")
            {
                float shoulderYaw = TurnGuard(lookAt, dt);
                bool checking = speaking && action == NpcAction.CheckPass;
                float sinceReply = now - replyStart;
                float inspect = checking ? Mathf.SmoothStep(0, 1, sinceReply / .24f) *
                    (1f - Mathf.SmoothStep(0, 1, (sinceReply - .65f) / .38f)) : 0;
                if (lookAt.HasValue) lookAt = lookAt.Value - Vector3.up * (.46f * inspect);
                chestAngles.y = (1f - idle) * shoulderYaw;
                headAngles.x = nod * 14f + voice * 3f * beat;
                armAngles.x = -38f * greeting - voice * (checking ? 47f : 20f) * (.74f + .26f * beat);
                armAngles.z = 14f * greeting + voice * (checking ? -6f : 7f * beat);
                // The eyes lead the shoulders. A brief shield adjustment follows the second scan.
                float Scan(float seconds) => 19f * Beat(seconds, 10.7f, .4f, 1.4f, .65f, 1.55f)
                    - 24f * Beat(seconds, 10.7f, 5.1f, 1.5f, .8f, 1.35f);
                headAngles.y += idle * Scan(t);
                chestAngles.y += idle * .21f * Scan(t - .28f);
                float shield = idle * Beat(t, 10.7f, 7.3f, .72f, .5f, .95f);
                heldArmAngles = new Vector3(-4f * shield, 0, -7f * shield);
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
                float read = idle * Beat(t, 8.4f, .35f, 1.1f, 1.65f, 1.25f);
                float lift = idle * Beat(t + .28f, 8.4f, .35f, 1.15f, 1.85f, 1.15f);
                headAngles += new Vector3(18f, -16f, -3f) * read;
                chestAngles += new Vector3(3f, -2f, 0f) * read;
                heldArmAngles.x = -19f * lift - voice * (order ? 4f : 12.5f) * (.78f + .22f * beat);
                armAngles.z += idle * (1.5f + 1.5f * breath);
            }
            else if (horseChest)
            {
                bool signal = speaking && action == NpcAction.OfferTraining && trainingTarget.HasValue;
                if (signal) lookAt = trainingTarget;
                // Keep the hands inside the reins while the head leads the turn.
                float survey = idle * Beat(t, 12.2f, 1.1f, 2f, 1.3f, 2.4f);
                float yardYaw = trainingFocus ? LookAngles(trainingFocus.position).x : -29f;
                headAngles.y += survey * yardYaw * .78f + idle * 5f * Mathf.Sin(t * .52f);
                chestAngles.y = 2.1f * survey * Mathf.Sign(yardYaw);
                headAngles.x = 11f * nod + voice * (action == NpcAction.ReviewTraining ? 10f : 3f) * beat;
                AnimateMount(t, greeting, dt);
            }
            Vector2 gaze = lookAt.HasValue ? LookAngles(lookAt.Value) * (1f - idle) : Vector2.zero;
            if (horseChest) chestAngles.y += .042f * gaze.x * voice;
            // Follow from the pose currently on screen, including interrupted greetings or replies.
            // At 0.097 s per time constant a changed target travels 95% in 0.29 s.
            float follow = 1f - Mathf.Exp(-dt / .097f);
            chest.localPosition = restChest + Vector3.up * ((horseChest ? .0015f : .003f) * breath);
            chest.localRotation = Quaternion.Slerp(chest.localRotation, restChestRotation * Quaternion.Euler(chestAngles), follow);
            headAngles += new Vector3(gaze.y + .5f * breath, gaze.x + 1.1f * Mathf.Sin(t * .64f), 0);
            head.localRotation = Quaternion.Slerp(head.localRotation, restHead * Quaternion.Euler(headAngles), follow);
            float syllable = Mathf.Abs(Mathf.Sin(t * 11.2f) * Mathf.Cos(t * 2.3f));
            jaw.localPosition = restJaw - Vector3.up * (animatedByAudio ? .008f * mouthOpening : .006f * syllable * voice);
            gestureArm.localRotation = Quaternion.Slerp(gestureArm.localRotation, restArm * Quaternion.Euler(armAngles), follow);
            if (offArm) offArm.localRotation = Quaternion.Slerp(offArm.localRotation,
                restOffArm * Quaternion.Euler(heldArmAngles), follow);
        }

        private float TurnGuard(Vector3? viewer, float dt)
        {
            float desired = 0;
            if (engaged && viewer.HasValue)
            {
                var local = Quaternion.Inverse(restFigure) *
                    guardFigure.parent.InverseTransformDirection(viewer.Value - guardFigure.position);
                desired = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            }
            // The head can respond immediately while the feet complete a slower in-place turn.
            figureYaw = Mathf.SmoothDampAngle(figureYaw, desired, ref turnVelocity, .26f, 215f, dt);
            guardFigure.localRotation = restFigure * Quaternion.AngleAxis(figureYaw, Vector3.up);
            return Mathf.Clamp(.38f * Mathf.DeltaAngle(figureYaw, desired), -27f, 27f);
        }

        private Vector2 LookAngles(Vector3 point)
        {
            // The guard's Head pivot sits at the neck. Aim from the eye patches on the visor opening.
            Vector3 eye = guardFigure ? head.TransformPoint(0f, .168f, .096f) : head.position;
            var direction = Quaternion.Inverse(restHead) * head.parent.InverseTransformDirection(point - eye).normalized;
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
            return guardFigure
                ? new Vector2(Mathf.Clamp(yaw, -57f, 57f), Mathf.Clamp(pitch, -31f, 40f))
                : new Vector2(Mathf.Clamp(yaw, -35f, 35f), Mathf.Clamp(pitch, -13f, 17f));
        }

        private static float Beat(float time, float cycle, float start, float rise, float hold, float fall)
        {
            float t = Mathf.Repeat(time - start, cycle);
            return Mathf.SmoothStep(0, 1, t / rise) * (1 - Mathf.SmoothStep(0, 1, (t - rise - hold) / fall));
        }

        private void AnimateMount(float time, float greeting, float dt)
        {
            float breath = Mathf.Sin(time * 1.24f);
            horseChest.localScale = Vector3.Scale(restBarrel, new Vector3(1f + .005f * breath, 1f + .003f * breath, 1f));
            float left = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(time * 1.32f)), 10f);
            float right = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(time * 1.04f + 1.4f)), 12f);
            float follow = 1f - Mathf.Exp(-dt / .09f);
            earL.localRotation = Quaternion.Slerp(earL.localRotation,
                restEarL * Quaternion.Euler(6f * left + 15f * greeting, 9f * left, 10f * left - 5f * greeting), follow);
            earR.localRotation = Quaternion.Slerp(earR.localRotation,
                restEarR * Quaternion.Euler(7f * right + 14f * greeting, -11f * right, -9f * right + 5f * greeting), follow);
            if (horseTail)
            {
                float swish = Beat(time, 7.7f, 2.1f, .47f, .02f, .56f)
                    - .47f * Beat(time, 7.7f, 2.92f, .45f, .03f, .69f);
                // The mounted figure stands diagonally in its model space.
                var horseForward = new Vector3(.7660f, 0, .6428f);
                var horseRight = new Vector3(.6428f, 0, -.7660f);
                horseTail.localRotation = restTail * Quaternion.AngleAxis(12f * swish, horseForward)
                    * Quaternion.AngleAxis(2f * swish, horseRight);
            }
        }
    }
}
