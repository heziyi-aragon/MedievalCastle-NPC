using UnityEngine;

namespace MedievalCastle.Mechanics
{
    public sealed class GateView : MonoBehaviour
    {
        public Vector3 localTravel = new Vector3(0, 4.5f, 0);
        public float speed = 3;
        private Vector3 closedPosition;

        public bool IsOpen { get; set; }

        private void Awake() => closedPosition = transform.localPosition;

        private void Update()
        {
            Vector3 target = IsOpen ? closedPosition + localTravel : closedPosition;
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, speed * Time.deltaTime);
        }
    }
}
