using UnityEngine;

namespace MedievalCastle.Editor
{
    public static class CastleEnvironmentLayout
    {
        public const float EntranceCorridorHalfWidth = 3f;
        public const float EntranceCorridorLength = 48f;

        public static Vector3 GateAnchor(Bounds castleBounds)
        {
            return new Vector3(castleBounds.center.x, castleBounds.min.y, castleBounds.max.z + 0.5f);
        }

        public static Vector3 GuardCenter(Bounds castleBounds)
        {
            return GateAnchor(castleBounds) + new Vector3(0f, 0f, 4.5f);
        }

        public static Vector3 MarketCenter(Bounds castleBounds)
        {
            return GateAnchor(castleBounds) + new Vector3(15.5f, 0f, 8f);
        }

        public static Vector3 TrainingCenter(Bounds castleBounds)
        {
            return GateAnchor(castleBounds) + new Vector3(-16.5f, 0f, 9.5f);
        }

        public static bool IsInsideEntranceCorridor(Vector3 worldPosition, Bounds castleBounds)
        {
            Vector3 gate = GateAnchor(castleBounds);
            float outward = worldPosition.z - gate.z;
            return outward >= 0f && outward <= EntranceCorridorLength &&
                Mathf.Abs(worldPosition.x - gate.x) < EntranceCorridorHalfWidth;
        }

        public static float SmoothRectangleMask(Vector2 point, Vector2 center, Vector2 halfSize, float feather)
        {
            Vector2 distance = new Vector2(
                Mathf.Abs(point.x - center.x) - halfSize.x,
                Mathf.Abs(point.y - center.y) - halfSize.y);
            float outside = Mathf.Max(distance.x, distance.y);
            if (outside <= 0f)
            {
                return 1f;
            }

            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(outside / Mathf.Max(0.001f, feather)));
        }

        public static float RoadMask(Vector2 point, Vector2 start, Vector2 end, float halfWidth, float feather)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            float t = lengthSquared > 0.001f
                ? Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared)
                : 0f;
            float distance = Vector2.Distance(point, start + segment * t);
            if (distance <= halfWidth)
            {
                return 1f;
            }

            float outside = distance - halfWidth;
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(outside / Mathf.Max(0.001f, feather)));
        }
    }
}
