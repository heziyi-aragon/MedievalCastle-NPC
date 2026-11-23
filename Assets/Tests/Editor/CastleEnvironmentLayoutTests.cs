using NUnit.Framework;
using UnityEngine;

namespace MedievalCastle.Editor.Tests
{
    public class CastleEnvironmentLayoutTests
    {
        private static readonly Bounds CastleBounds = new Bounds(
            new Vector3(0f, 14.55f, 0.191f),
            new Vector3(49.34f, 29.1f, 49.721f));

        [Test]
        public void GateAnchorUsesFrontCenterAtGroundLevel()
        {
            Vector3 anchor = CastleEnvironmentLayout.GateAnchor(CastleBounds);

            Assert.That(anchor.x, Is.EqualTo(CastleBounds.center.x).Within(0.001f));
            Assert.That(anchor.y, Is.EqualTo(CastleBounds.min.y).Within(0.001f));
            Assert.That(anchor.z, Is.GreaterThan(CastleBounds.max.z));
        }

        [Test]
        public void FunctionalZonesStayClearOfEntranceCorridor()
        {
            Assert.That(CastleEnvironmentLayout.IsInsideEntranceCorridor(
                CastleEnvironmentLayout.MarketCenter(CastleBounds), CastleBounds), Is.False);
            Assert.That(CastleEnvironmentLayout.IsInsideEntranceCorridor(
                CastleEnvironmentLayout.TrainingCenter(CastleBounds), CastleBounds), Is.False);
        }

        [Test]
        public void CorridorIncludesCenterlineButNotItsSideZones()
        {
            Vector3 gate = CastleEnvironmentLayout.GateAnchor(CastleBounds);

            Assert.That(CastleEnvironmentLayout.IsInsideEntranceCorridor(gate + Vector3.forward * 20f, CastleBounds), Is.True);
            Assert.That(CastleEnvironmentLayout.IsInsideEntranceCorridor(gate + new Vector3(8f, 0f, 20f), CastleBounds), Is.False);
        }

        [Test]
        public void SmoothRectangleMaskHasSolidCenterAndSoftOutsideEdge()
        {
            Vector2 center = new Vector2(16f, 40f);

            Assert.That(CastleEnvironmentLayout.SmoothRectangleMask(center, center, new Vector2(8f, 7f), 4f), Is.EqualTo(1f));
            Assert.That(CastleEnvironmentLayout.SmoothRectangleMask(center + new Vector2(14f, 0f), center, new Vector2(8f, 7f), 4f), Is.EqualTo(0f));
            Assert.That(CastleEnvironmentLayout.SmoothRectangleMask(center + new Vector2(10f, 0f), center, new Vector2(8f, 7f), 4f), Is.InRange(0f, 1f));
        }

        [Test]
        public void RoadMaskKeepsCenterSolidAndFadesBeyondShoulder()
        {
            Vector2 start = new Vector2(0f, 25f);
            Vector2 end = new Vector2(0f, 66f);

            Assert.That(CastleEnvironmentLayout.RoadMask(new Vector2(0f, 45f), start, end, 2.5f, 2f), Is.EqualTo(1f));
            Assert.That(CastleEnvironmentLayout.RoadMask(new Vector2(6f, 45f), start, end, 2.5f, 2f), Is.EqualTo(0f));
            Assert.That(CastleEnvironmentLayout.RoadMask(new Vector2(3.5f, 45f), start, end, 2.5f, 2f), Is.InRange(0f, 1f));
        }
    }
}
