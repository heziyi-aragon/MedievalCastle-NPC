using UnityEngine;

namespace MedievalCastle.Mechanics
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class CastlePlayer : MonoBehaviour
    {
        public Camera view;
        public float moveSpeed = 4.8f;
        public bool MovementLocked { get; set; }

        private CharacterController body;
        private float verticalSpeed;
        private float pitch;

        private void Awake() => body = GetComponent<CharacterController>();

        private void Update()
        {
            if (MovementLocked) return;
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                transform.Rotate(0, Input.GetAxisRaw("Mouse X") * 2.2f, 0);
                pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * 2.2f, -80, 80);
                if (view != null) view.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            }

            Vector3 move = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
            move = transform.TransformDirection(Vector3.ClampMagnitude(move, 1)) * moveSpeed;
            verticalSpeed = body.isGrounded ? -2 : Mathf.Max(-25, verticalSpeed - 22 * Time.deltaTime);
            move.y = verticalSpeed;
            body.Move(move * Time.deltaTime);
        }
    }
}
