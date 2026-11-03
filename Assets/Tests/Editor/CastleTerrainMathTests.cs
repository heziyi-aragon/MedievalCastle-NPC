using NUnit.Framework;
using UnityEngine;

namespace MedievalCastle.Editor.Tests
{
    public class CastleTerrainMathTests
    {
        [Test]
        public void SmoothStep01ClampsAndEases()
        {
            Assert.That(CastleTerrainMath.SmoothStep01(-1f), Is.EqualTo(0f));
            Assert.That(CastleTerrainMath.SmoothStep01(0.5f), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(CastleTerrainMath.SmoothStep01(2f), Is.EqualTo(1f));
        }

        [Test]
        public void PlateauFalloffIsFlatInsidePlatformAndZeroOutsideHill()
        {
            Assert.That(CastleTerrainMath.PlateauFalloff(0f, 0f, 20f, 15f, 70f, 60f), Is.EqualTo(1f));
            Assert.That(CastleTerrainMath.PlateauFalloff(19f, 0f, 20f, 15f, 70f, 60f), Is.EqualTo(1f));
            Assert.That(CastleTerrainMath.PlateauFalloff(71f, 0f, 20f, 15f, 70f, 60f), Is.EqualTo(0f));
        }

        [Test]
        public void PlateauFalloffDecreasesSmoothlyAcrossSlope()
        {
            float near = CastleTerrainMath.PlateauFalloff(25f, 0f, 20f, 15f, 70f, 60f);
            float middle = CastleTerrainMath.PlateauFalloff(45f, 0f, 20f, 15f, 70f, 60f);
            float far = CastleTerrainMath.PlateauFalloff(65f, 0f, 20f, 15f, 70f, 60f);

            Assert.That(near, Is.GreaterThan(middle));
            Assert.That(middle, Is.GreaterThan(far));
            Assert.That(far, Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void SurfaceWeightsFavorGrassOnGentleGroundAndRockOnSteepGround()
        {
            Vector4 gentle = CastleTerrainMath.CalculateSurfaceWeights(0f, 1f, 0.5f);
            Vector4 steep = CastleTerrainMath.CalculateSurfaceWeights(1f, 0.4f, 0.5f);

            Assert.That(gentle.x, Is.GreaterThan(gentle.y));
            Assert.That(gentle.x, Is.GreaterThan(gentle.z));
            Assert.That(steep.z, Is.GreaterThan(steep.x));
            Assert.That(steep.z, Is.GreaterThan(steep.y));
        }

        [Test]
        public void SurfaceWeightsAreNormalized()
        {
            Vector4 weights = CastleTerrainMath.CalculateSurfaceWeights(0.33f, 0.72f, 0.8f);
            Assert.That(weights.x + weights.y + weights.z + weights.w, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(weights.x, Is.GreaterThanOrEqualTo(0f));
            Assert.That(weights.y, Is.GreaterThanOrEqualTo(0f));
            Assert.That(weights.z, Is.GreaterThanOrEqualTo(0f));
            Assert.That(weights.w, Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void SurfaceWeightsUseVariationToBreakUniformSoilBands()
        {
            Vector4 lowVariation = CastleTerrainMath.CalculateSurfaceWeights(0.2f, 0.7f, 0.1f);
            Vector4 highVariation = CastleTerrainMath.CalculateSurfaceWeights(0.2f, 0.7f, 0.9f);

            Assert.That(highVariation.y, Is.GreaterThan(lowVariation.y + 0.1f));
        }
    }
}
