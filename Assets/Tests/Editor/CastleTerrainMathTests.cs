using NUnit.Framework;

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
    }
}
