using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace RockBalancing.Tests
{
    public sealed class ArcadeStoneRulesTests
    {
        [TestCase(20f, false)]
        [TestCase(30f, true)]
        public void ShearedStoneChecksTheAcuteInteriorAngle(float interiorAngle, bool playable)
        {
            float radians = interiorAngle * Mathf.Deg2Rad;
            var transverse = new Vector3(-Mathf.Sin(radians), 0, Mathf.Cos(radians));
            float width = .12f * Mathf.Sin(radians);
            var planes = new List<Plane>
            {
                new(Vector3.down, Vector3.zero), new(Vector3.up, Vector3.up * (width * .45f)),
                new(Vector3.back, Vector3.zero), new(Vector3.forward, Vector3.forward * width),
                new(transverse, Vector3.zero), new(-transverse, Vector3.right * .15f)
            };

            Assert.That(ArcadeStoneHull.FromPlanes(planes).IsPlayable, Is.EqualTo(playable));
        }

        [TestCase(150f, true)]
        [TestCase(160f, false)]
        [TestCase(180f, true)]
        public void RoofStoneChecksAdjacentCapAnglesAndAllowsParallelOppositeFaces(float interiorAngle, bool playable)
        {
            float pitch = (180f - interiorAngle) * .5f;
            var crest = Vector3.up * .06f;
            var planes = new List<Plane>
            {
                new(Vector3.down, Vector3.zero), new(Vector3.left, Vector3.left * .10f),
                new(Vector3.right, Vector3.right * .10f), new(Vector3.back, Vector3.back * .06f),
                new(Vector3.forward, Vector3.forward * .06f),
                new(Quaternion.AngleAxis(pitch, Vector3.right) * Vector3.up, crest)
            };
            if (pitch > 0) planes.Add(new Plane(Quaternion.AngleAxis(-pitch, Vector3.right) * Vector3.up, crest));

            Assert.That(ArcadeStoneHull.FromPlanes(planes).IsPlayable, Is.EqualTo(playable));
        }
    }
}
