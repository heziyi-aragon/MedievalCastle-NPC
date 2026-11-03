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

        public static Vector4 CalculateSurfaceWeights(float slope, float plateauFalloff, float variation)
        {
            float clampedSlope = Mathf.Clamp01(slope);
            float clampedFalloff = Mathf.Clamp01(plateauFalloff);
            float clampedVariation = Mathf.Clamp01(variation);

            float effectiveSlope = clampedSlope * Mathf.Lerp(0.85f, 1.15f, clampedVariation);
            float rockFactor = SmoothStep01(Mathf.InverseLerp(0.2f, 0.62f, effectiveSlope));
            float soilFromSlope = SmoothStep01(Mathf.InverseLerp(0.08f, 0.42f, clampedSlope))
                * (1f - rockFactor) * 0.35f;
            float soilPatch = SmoothStep01(Mathf.InverseLerp(0.35f, 0.8f, clampedVariation))
                * (1f - rockFactor)
                * Mathf.Lerp(1f, 0.35f, clampedFalloff)
                * 0.95f;

            float grassWeight = Mathf.Lerp(1.35f, 0.08f, rockFactor) * (1f - soilPatch * 0.25f);
            float soilWeight = 0.04f + soilFromSlope + soilPatch;
            float rockWeight = 0.02f + rockFactor * 2.2f;
            float legacyGrassWeight = (0.04f + (1f - clampedVariation) * 0.06f) * (1f - rockFactor);
            float total = grassWeight + soilWeight + rockWeight + legacyGrassWeight;

            return new Vector4(
                grassWeight / total,
                soilWeight / total,
                rockWeight / total,
                legacyGrassWeight / total);
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
