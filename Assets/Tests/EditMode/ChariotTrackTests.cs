using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>
    /// R1 (docs/ARENAS.md §5, "The track (numbers)"): the chariots' path with the Imperial Arena's numbers (ChariotRace's
    /// defaults): a 288 m lap, a 606 m run from the south stall to the north stall, counter-clockwise round the spina,
    /// the straights and the east turn passed twice per chariot and the west turn once; the lane is the centre line ± 2 m,
    /// run-in and run-out included, and the spina is off it. Pure math, no scene.
    /// </summary>
    public class ChariotTrackTests
    {
        GameObject _go;
        ChariotRace _race;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("ChariotTrack");
            _race = _go.AddComponent<ChariotRace>();
            _race.BuildPath();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        static void Near(Vector3 expected, Vector3 actual, float tol, string what)
        {
            Assert.LessOrEqual(Vector3.Distance(new Vector3(expected.x, 0f, expected.z), new Vector3(actual.x, 0f, actual.z)), tol,
                what + ": expected " + expected + ", got " + actual);
        }

        [Test]
        public void TheLapIs288m_AndAChariotsRunIs606m_At22mps27Point5s_HalfALapApart()
        {
            Assert.AreEqual(2f * 100f + 2f * Mathf.PI * 14f, _race.LapLength, 0.01f);
            Assert.AreEqual(288f, _race.LapLength, 0.5f);
            Assert.AreEqual(606f, _race.PathLength, 1f, "run-in 37 + 2 laps − the last west turn + run-out 37");
            Assert.AreEqual(27.5f, _race.RunSeconds, 0.1f);
            Assert.AreEqual(6.5f, _race.LaunchGap, 0.1f, "two chariots: half a lap apart");
        }

        [Test]
        public void ThePath_StartsInTheSouthStallHeadingEast_AndEndsInTheNorthStallHeadingWest()
        {
            Vector3 p = _race.PointAt(0f, out Vector3 f);
            Near(new Vector3(-97f, 0f, -14f), p, 0.01f, "the start (inside the carceres)");
            Assert.Greater(f.x, 0.99f, "heading east along the south straight's run-in");
            p = _race.PointAt(37f, out _);
            Near(new Vector3(-60f, 0f, -14f), p, 0.01f, "the run-in joins the south straight at the west meta");
            p = _race.PointAt(_race.PathLength, out f);
            Near(new Vector3(-97f, 0f, 14f), p, 0.01f, "the end (the north stall)");
            Assert.Less(f.x, -0.99f, "leaving west along the north straight's run-out");
            Near(_race.PointAt(_race.PathLength, out _), _race.PointAt(_race.PathLength + 50f, out _), 0.001f, "clamped past the end");
        }

        [Test]
        public void TheChariots_RunCounterClockwise_ThroughTheEastApex_ThenTheWestApex()
        {
            float east = 37f + 100f + Mathf.PI * 14f * 0.5f;
            Vector3 p = _race.PointAt(east, out Vector3 f);
            Near(new Vector3(54f, 0f, 0f), p, 0.01f, "the east apex");
            Assert.Greater(f.z, 0.99f, "heading north there: a left turn, counter-clockwise from above");
            p = _race.PointAt(37f + 100f + Mathf.PI * 14f + 50f, out f);
            Near(new Vector3(-10f, 0f, 14f), p, 0.01f, "back west along the north straight");
            Assert.Less(f.x, -0.99f);
            float west = 37f + 200f + Mathf.PI * 14f * 1.5f;
            p = _race.PointAt(west, out f);
            Near(new Vector3(-74f, 0f, 0f), p, 0.01f, "the west apex");
            Assert.Less(f.z, -0.99f, "heading south there");
        }

        /// <summary>Distinct visits of one chariot's path to within 0.5 m of p (sampled every 0.25 m).</summary>
        int Visits(Vector3 p)
        {
            int visits = 0;
            bool near = false;
            for (float s = 0f; s <= _race.PathLength; s += 0.25f)
            {
                bool now = Vector3.Distance(_race.PointAt(s, out _), p) < 0.5f;
                if (now && !near) visits++;
                near = now;
            }
            return visits;
        }

        [Test]
        public void EachChariot_PassesTheStraightsAndTheEastTurnTwice_TheWestTurnOnce_TheRunInAndOutOnce()
        {
            Assert.AreEqual(2, Visits(new Vector3(0f, 0f, -14f)), "the south straight");
            Assert.AreEqual(2, Visits(new Vector3(0f, 0f, 14f)), "the north straight");
            Assert.AreEqual(2, Visits(new Vector3(54f, 0f, 0f)), "the east apex");
            Assert.AreEqual(1, Visits(new Vector3(-74f, 0f, 0f)), "the west apex: the last lap leaves by the run-out");
            Assert.AreEqual(1, Visits(new Vector3(-80f, 0f, -14f)), "the run-in");
            Assert.AreEqual(1, Visits(new Vector3(-80f, 0f, 14f)), "the run-out");
        }

        [Test]
        public void TheLane_IsTheCentreLinePlusMinus2m_RunInAndOutIncluded_AndTheSpinaIsOffIt()
        {
            Assert.AreEqual(0f, _race.LaneOffset(new Vector3(0f, 0f, -14f)), 0.01f, "the south straight's centre line");
            Assert.AreEqual(0f, _race.LaneOffset(new Vector3(54f, 0f, 0f)), 0.01f, "the east apex");
            Assert.AreEqual(0f, _race.LaneOffset(new Vector3(-80f, 0f, -14f)), 0.01f, "the run-in");
            Assert.AreEqual(0f, _race.LaneOffset(new Vector3(-80f, 0f, 14f)), 0.01f, "the run-out");
            Assert.AreEqual(2f, _race.LaneOffset(new Vector3(10f, 0f, -12f)), 0.01f, "the lane's inner edge");
            Assert.AreEqual(2f, _race.LaneOffset(new Vector3(56f, 0f, 0f)), 0.01f, "the east turn's outer edge");
            Assert.AreEqual(14f, _race.LaneOffset(new Vector3(-10f, 1.2f, 0f)), 0.01f, "the spina's top by the obelisk");
            Assert.AreEqual(14f, _race.LaneOffset(new Vector3(-60f, 0f, 0f)), 0.01f, "the west meta");
            Assert.IsTrue(_race.InLane(new Vector3(0f, 0f, -15.9f), 0f));
            Assert.IsFalse(_race.InLane(new Vector3(0f, 0f, -16.5f), 0f), "just beside the lane");
            Assert.IsFalse(_race.InLane(new Vector3(-10f, 0f, -26f), 0f), "the player's spawn");
            Assert.IsFalse(_race.InLane(new Vector3(-10f, 0f, 7.5f), 0f), "the inner strip");
            Assert.IsTrue(_race.InLane(new Vector3(-10f, 0f, 7.5f), 4.6f), "pad grows the lane");
        }

        [Test]
        public void OneLap_LeavesStraightAfterTheEastTurn_WithNoWestTurn()
        {
            _race.chariotLaps = 1;
            _race.BuildPath();
            Assert.AreEqual(37f + 288f - Mathf.PI * 14f + 37f, _race.PathLength, 0.5f);
            Assert.AreEqual(14f, _race.LaneOffset(new Vector3(-74f, 0f, 0f)), 0.01f, "the west apex isn't on a one-lap race's path");
            Near(new Vector3(-97f, 0f, 14f), _race.PointAt(_race.PathLength, out _), 0.01f, "it still ends in the north stall");
        }

        [Test]
        public void NoRace_NoDanger()
        {
            Assert.IsFalse(_race.Running);
            Assert.IsTrue(float.IsPositiveInfinity(_race.Danger(new Vector3(0f, 0f, -14f))));
        }
    }
}
