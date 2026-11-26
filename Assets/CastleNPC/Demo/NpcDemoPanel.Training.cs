using UnityEngine;
using UnityEngine.UI;

namespace MedievalCastle.Npc.Demo
{
    public sealed partial class NpcDemoPanel
    {
        private const float TrainingRadius = 1f;
        private Canvas trainingMarker;
        private Text trainingNumber;

        private void UpdateTrainingPoint(Camera eye)
        {
            int index = gameplay.NextTrainingPoint;
            if (index < 0 || index >= trainingPoints.Length || !trainingPoints[index] ||
                !eye || InConversation || MenuOpen)
            {
                if (trainingMarker) trainingMarker.gameObject.SetActive(false);
                return;
            }

            Transform point = trainingPoints[index];
            var body = eye.GetComponentInParent<CharacterController>();
            if (body)
            {
                Bounds bounds = body.bounds;
                Vector3 distance = bounds.center - Vector3.up * bounds.extents.y - point.position;
                if (Mathf.Abs(distance.y) <= .8f &&
                    distance.x * distance.x + distance.z * distance.z <= TrainingRadius * TrainingRadius &&
                    gameplay.VisitTrainingPoint(index))
                {
                    index = gameplay.NextTrainingPoint;
                    if (index < 0 || !trainingPoints[index])
                    {
                        if (trainingMarker) trainingMarker.gameObject.SetActive(false);
                        return;
                    }
                    point = trainingPoints[index];
                }
            }

            if (!trainingMarker)
            {
                trainingMarker = WorldCard("Training marker", Slate);
                ((RectTransform)trainingMarker.transform).sizeDelta = new Vector2(136, 100);
                trainingMarker.transform.localScale = Vector3.one * .0035f;
                trainingNumber = Label(trainingMarker.transform, "Marker number", 70, Parchment, 0, 0, 136, 100);
                trainingNumber.alignment = TextAnchor.MiddleCenter;
            }
            trainingMarker.gameObject.SetActive(true);
            trainingMarker.worldCamera = eye;
            Vector3 center = point.position + Vector3.up * 1.35f;
            Vector3 facing = center - eye.transform.position;
            if (facing.sqrMagnitude > .001f)
                trainingMarker.transform.SetPositionAndRotation(center, Quaternion.LookRotation(facing, Vector3.up));
            string number = (index + 1).ToString();
            if (trainingNumber.text != number) trainingNumber.text = number;
        }
    }
}
