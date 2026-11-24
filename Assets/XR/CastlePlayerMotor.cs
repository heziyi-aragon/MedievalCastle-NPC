using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace MedievalCastle
{
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(50)]
    public sealed class CastlePlayerMotor : MonoBehaviour
    {
        [SerializeField] private float walkingSpeed = 1.6f;
        private const float DeadZone = .16f;
        private CharacterController body;
        private Camera view;
        private Vector3 planarSpeed;
        private float verticalSpeed, pitch;
        private bool headset, snapHeld, ownsCursor;

        public static bool MovementLocked { get; set; }
        public Transform View => view ? view.transform : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => MovementLocked = false;

        public void Configure(Camera cameraView, bool usingHeadset)
        {
            view = cameraView;
            headset = usingHeadset;
            body = GetComponent<CharacterController>();
            body.radius = .24f;
            body.height = 1.65f;
            body.center = Vector3.up * (body.height * .5f);
            body.skinWidth = .035f;
            body.minMoveDistance = 0f;
            body.slopeLimit = 45f;
            body.stepOffset = .24f;
            pitch = Mathf.DeltaAngle(0f, view.transform.localEulerAngles.x);
            snapHeld = false;
            planarSpeed = Vector3.zero;
            verticalSpeed = 0f;
        }

        private void Update()
        {
            if (!view) return;
            UpdateCursor();
            Vector2 move = Vector2.zero;
            float turn = 0f;
            if (headset)
            {
                var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                if (left.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 leftStick)) move = leftStick;
                if (right.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 rightStick)) turn = rightStick.x;
            }
            else if (Application.isFocused)
            {
                var keys = Keyboard.current;
                if (keys != null)
                    move = new Vector2((keys.dKey.isPressed ? 1f : 0f) - (keys.aKey.isPressed ? 1f : 0f),
                                       (keys.wKey.isPressed ? 1f : 0f) - (keys.sKey.isPressed ? 1f : 0f));
                var mouse = Mouse.current;
                if (!MovementLocked && ownsCursor && mouse != null)
                {
                    var delta = mouse.delta.ReadValue() * .09f;
                    transform.Rotate(0f, delta.x, 0f, Space.World);
                    pitch = Mathf.Clamp(pitch - delta.y, -75f, 75f);
                    view.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                }
            }
            Step(move, turn, Time.deltaTime);
        }

        /// <summary>Apply one sampled input frame, using the tracked eye as the locomotion reference.</summary>
        public void Step(Vector2 move, float snapAxis, float deltaTime)
        {
            if (!view || !body.enabled || deltaTime <= 0f) return;
            float dt = Mathf.Min(deltaTime, .1f);
            if (headset)
            {
                var eye = transform.InverseTransformPoint(view.transform.position);
                float height = Mathf.Clamp(eye.y + .06f, .6f, 2.35f);
                var center = new Vector3(eye.x, height * .5f, eye.z);
                if (Mathf.Abs(body.height - height) > .01f) body.height = height;
                if ((body.center - center).sqrMagnitude > .000025f) body.center = center;
            }
            if (Mathf.Abs(snapAxis) < .32f) snapHeld = false;
            else if (!snapHeld && Mathf.Abs(snapAxis) > .72f)
            {
                snapHeld = true;
                if (!MovementLocked)
                    transform.RotateAround(view.transform.position, Vector3.up, 30f * Mathf.Sign(snapAxis));
            }
            Vector3 forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < .001f) forward = transform.forward;
            forward.Normalize();
            float strength = Mathf.InverseLerp(DeadZone, 1f, move.magnitude);
            move = move.normalized * strength;
            Vector3 direction = forward * move.y + Vector3.Cross(Vector3.up, forward) * move.x;
            planarSpeed = MovementLocked ? Vector3.zero :
                Vector3.MoveTowards(planarSpeed, direction * walkingSpeed, (strength > 0f ? 5f : 9f) * dt);
            if (body.isGrounded && verticalSpeed < 0f) verticalSpeed = -1f;
            verticalSpeed = Mathf.Max(-15f, verticalSpeed + Physics.gravity.y * dt);
            body.Move((planarSpeed + Vector3.up * verticalSpeed) * dt);
        }

        private void UpdateCursor()
        {
            bool shouldLock = !headset && !MovementLocked && Application.isFocused;
            if (ownsCursor == shouldLock) return;
            ownsCursor = shouldLock;
            Cursor.lockState = ownsCursor ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !ownsCursor;
        }

        private void OnDisable()
        {
            if (!ownsCursor) return;
            ownsCursor = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
