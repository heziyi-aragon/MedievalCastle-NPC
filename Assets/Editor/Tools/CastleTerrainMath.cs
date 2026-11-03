using UnityEngine;

namespace MedievalCastle.Editor
{
    public static class CastleTerrainMath
    {
        public static float SmoothStep01(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }

        public static float PlateauFalloff(
            float offsetX,
            float offsetZ,
            float platformRadiusX,
            float platformRadiusZ,
            float hillRadiusX,
            float hillRadiusZ)
        {
            float distance = Mathf.Sqrt(offsetX * offsetX + offsetZ * offsetZ);
            if (distance <= Mathf.Epsilon)
            {
                return 1f;
            }

            float directionX = offsetX / distance;
            float directionZ = offsetZ / distance;
            float platformBoundary = EllipseRadius(directionX, directionZ, platformRadiusX, platformRadiusZ);
            if (distance <= platformBoundary)
            {
                return 1f;
            }

            float hillBoundary = EllipseRadius(directionX, directionZ, hillRadiusX, hillRadiusZ);
            if (distance >= hillBoundary)
            {
                return 0f;
            }

            float slopeProgress = Mathf.InverseLerp(platformBoundary, hillBoundary, distance);
            return 1f - SmoothStep01(slopeProgress);
        }

        private static float EllipseRadius(float directionX, float directionZ, float radiusX, float radiusZ)
        {
            float safeRadiusX = Mathf.Max(0.001f, radiusX);
            float safeRadiusZ = Mathf.Max(0.001f, radiusZ);
            float denominator = Mathf.Sqrt(
                directionX * directionX / (safeRadiusX * safeRadiusX) +
                directionZ * directionZ / (safeRadiusZ * safeRadiusZ));
            return 1f / Mathf.Max(0.0001f, denominator);
        }
    }
}
