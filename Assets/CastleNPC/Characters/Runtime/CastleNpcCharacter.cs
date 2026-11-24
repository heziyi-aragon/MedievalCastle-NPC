using UnityEngine;

namespace MedievalCastle.NPC
{
    [DisallowMultipleComponent]
    public sealed class CastleNpcCharacter : MonoBehaviour
    {
        [SerializeField] private string roleId;
        [SerializeField] private Transform chest;
        [SerializeField] private Transform head;
        [SerializeField] private Transform jaw;
        [SerializeField] private Transform gestureArm;
        [SerializeField] private Transform horseChest;
        [SerializeField] private Transform earL;
        [SerializeField] private Transform earR;

        private Quaternion restHead;
        private Quaternion restArm;
        private Vector3 restChest;
        private Vector3 restJaw;
        private Vector3 restBarrel;
        private Quaternion restEarL, restEarR;
        private Camera listener;
        private Vector2 gaze;
        private float phase;
        private float speakingWeight;
        private bool speaking;

        public string RoleId => roleId;

        public void SetSpeaking(bool value)
        {
            speaking = value;
        }

        private void Awake()
        {
            restChest = chest.localPosition;
            restHead = head.localRotation;
            restJaw = jaw.localPosition;
            restArm = gestureArm.localRotation;
            if (horseChest)
            {
                restBarrel = horseChest.localScale;
                restEarL = earL.localRotation;
                restEarR = earR.localRotation;
            }
            phase = roleId == "guard" ? 0.4f : (roleId == "merchant" ? 1.7f : 2.8f);
        }

        private void LateUpdate()
        {
            float t = Time.time + phase;
            speakingWeight = Mathf.MoveTowards(speakingWeight, speaking ? 1f : 0f, Time.deltaTime * 5f);
            float breath = Mathf.Sin(t * 1.6f);
            float syllable = Mathf.Abs(Mathf.Sin(t * 11.2f) * Mathf.Cos(t * 2.3f));
            chest.localPosition = restChest + Vector3.up * (0.003f * breath);
            float nod = roleId == "guard" ? 5f : 2f;
            float lift = roleId == "merchant" ? 8f : 3f;
            if (horseChest)
            {
                AnimateMount(t);
                UpdateRiderGaze();
                lift = 0.8f; // The reins stay in her hands.
            }
            head.localRotation = restHead * Quaternion.Euler(
                0.5f * breath + (gaze.y + nod * Mathf.Sin(t * 3.4f)) * speakingWeight,
                1.1f * Mathf.Sin(t * 0.64f) + gaze.x * speakingWeight, 0f);
            jaw.localPosition = restJaw - Vector3.up * (0.006f * syllable * speakingWeight);
            gestureArm.localRotation = restArm * Quaternion.Euler(
                -lift * (0.7f + 0.3f * Mathf.Sin(t * 2.8f)) * speakingWeight, 0f,
                lift * 0.25f * Mathf.Sin(t * 2.1f) * speakingWeight);
        }

        private void UpdateRiderGaze()
        {
            if (!speaking) return;
            if (!listener) listener = Camera.main;
            if (!listener) return;
            Vector3 direction = Quaternion.Inverse(restHead) * head.parent.InverseTransformDirection(
                (listener.transform.position - head.position).normalized);
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
            var target = new Vector2(Mathf.Clamp(yaw, -32f, 32f), Mathf.Clamp(pitch, -12f, 18f));
            gaze = Vector2.MoveTowards(gaze, target, Time.deltaTime * 100f);
        }

        private void AnimateMount(float t)
        {
            float breath = Mathf.Sin(t * 1.24f);
            horseChest.localScale = Vector3.Scale(restBarrel, new Vector3(1f + .005f * breath, 1f + .003f * breath, 1f));
            float flickL = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 1.32f)), 10f);
            float flickR = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 1.04f + 1.4f)), 12f);
            earL.localRotation = restEarL * Quaternion.Euler(4f * flickL, 0f, 7f * flickL);
            earR.localRotation = restEarR * Quaternion.Euler(5f * flickR, 0f, -8f * flickR);
        }
    }
}
