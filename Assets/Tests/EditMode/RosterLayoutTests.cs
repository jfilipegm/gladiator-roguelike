using Gladiator.Core;
using Gladiator.Gameplay;
using Gladiator.UI;
using NUnit.Framework;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>
    /// R9 (the select screens as a portrait grid): the grid and ladder maths (2D cursor moves, sizes, cells, fitting),
    /// the canvas a CanvasScaler gives each screen, and the layout rule that nothing ever leaves the screen or covers the
    /// diorama at 16:9, 16:10, 21:9 and 4:3 for up to 16 gladiators and 8 arenas. Plus the save's per-gladiator runs.
    /// P3: the arenas are a grid of tiles in a frame (ArenaLadder, three to a row) that fits the same way for up to 12
    /// arenas, and the roster leaves room above it for the picked tile's wreath (MainMenu.RosterScale).
    /// </summary>
    public class RosterLayoutTests
    {
        static readonly Vector2 Tile = new Vector2(148f, 190f);

        [Test]
        public void Step_MovesIn2D_AndStopsAtTheEdges()
        {
            // 9 tiles in 4 columns: rows 0-3, 4-7, 8
            Assert.AreEqual(1, GridMath.Step(0, 9, 4, 1, 0), "right");
            Assert.AreEqual(3, GridMath.Step(3, 9, 4, 1, 0), "the row's right end stops");
            Assert.AreEqual(4, GridMath.Step(4, 9, 4, -1, 0), "the row's left end stops (no wrap onto the row above)");
            Assert.AreEqual(5, GridMath.Step(1, 9, 4, 0, 1), "down a row");
            Assert.AreEqual(8, GridMath.Step(5, 9, 4, 0, 1), "down into the short last row lands on its last tile");
            Assert.AreEqual(8, GridMath.Step(8, 9, 4, 0, 1), "the bottom stops");
            Assert.AreEqual(8, GridMath.Step(8, 9, 4, 1, 0), "nothing right of the last tile");
            Assert.AreEqual(4, GridMath.Step(8, 9, 4, 0, -1), "up");
            Assert.AreEqual(2, GridMath.Step(2, 9, 4, 0, -1), "the top stops");
            Assert.AreEqual(0, GridMath.Step(0, 0, 4, 1, 0), "an empty grid");
            Assert.AreEqual(8, GridMath.Step(42, 9, 4, 0, 0), "an index past the end is clamped");
        }

        [Test]
        public void StepWrap_AndClimb()
        {
            Assert.AreEqual(8, GridMath.StepWrap(0, 9, -1));
            Assert.AreEqual(0, GridMath.StepWrap(8, 9, 1));
            Assert.AreEqual(4, GridMath.StepWrap(3, 9, 1));
            Assert.AreEqual(1, GridMath.Climb(0, 6, 1), "up the ladder");
            Assert.AreEqual(5, GridMath.Climb(5, 6, 1), "the top step stops");
            Assert.AreEqual(0, GridMath.Climb(0, 6, -1), "the bottom step stops");
        }

        [Test]
        public void SizeAndCells_AreRowMajorFromTheTopLeft()
        {
            Assert.AreEqual(new Vector2(628f, 594f), GridMath.Size(9, 4, Tile, 12f));
            Assert.AreEqual(new Vector2(308f, 190f), GridMath.Size(2, 4, Tile, 12f), "fewer tiles than columns: just those");
            Assert.AreEqual(Vector2.zero, GridMath.Size(0, 4, Tile, 12f));
            Assert.AreEqual(new Vector2(74f, -95f), GridMath.Cell(0, 4, Tile, 12f));
            Assert.AreEqual(new Vector2(234f, -297f), GridMath.Cell(5, 4, Tile, 12f), "column 1, row 1");
            // the ladder: each step higher and further right, the first at the bottom-left
            var step = new Vector2(500f, 124f);
            Assert.AreEqual(new Vector2(250f, 62f), GridMath.LadderCell(0, step, 10f, 22f));
            Assert.AreEqual(new Vector2(294f, 330f), GridMath.LadderCell(2, step, 10f, 22f));
            Assert.AreEqual(new Vector2(610f, 794f), GridMath.LadderSize(6, step, 10f, 22f));
        }

        [Test]
        public void Fit_ShrinksToTheArea_NeverGrows()
        {
            Assert.AreEqual(1f, GridMath.Fit(new Vector2(100f, 100f), new Vector2(200f, 200f)));
            Assert.AreEqual(0.5f, GridMath.Fit(new Vector2(400f, 100f), new Vector2(200f, 200f)), 1e-5f);
            Assert.AreEqual(0.25f, GridMath.Fit(new Vector2(100f, 800f), new Vector2(200f, 200f)), 1e-5f);
        }

        [Test]
        public void CanvasSize_MatchesTheScaler()
        {
            var r = MenuLayout.Reference;
            AssertNear(new Vector2(1920f, 1080f), GridMath.CanvasSize(1920f, 1080f, r, 0.5f));
            AssertNear(new Vector2(1920f, 1080f), GridMath.CanvasSize(1280f, 720f, r, 0.5f), "16:9 scales uniformly");
            AssertNear(new Vector2(1821.5f, 1138.4f), GridMath.CanvasSize(1680f, 1050f, r, 0.5f), "16:10: narrower, taller");
            AssertNear(new Vector2(2217.0f, 935.3f), GridMath.CanvasSize(2560f, 1080f, r, 0.5f), "21:9: wider, shorter");
        }

        static readonly Vector2Int[] Screens =
        {
            new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(2560, 1440), new Vector2Int(1680, 1050),
            new Vector2Int(1920, 1200), new Vector2Int(1440, 900), new Vector2Int(2560, 1080), new Vector2Int(1024, 768),
        };

        /// <summary>The diorama's gladiator stands between the blocks: at least this much of the canvas stays clear.</summary>
        const float DioramaGap = 300f;

        [Test]
        public void EveryScreen_FitsTheGridAndTheTablet_AndKeepsTheDioramaClear()
        {
            foreach (var s in Screens)
            {
                var canvas = GridMath.CanvasSize(s.x, s.y, MenuLayout.Reference, MenuLayout.Match);
                float block = MenuLayout.BlockHeight(canvas);
                float tablet = MenuLayout.TabletScale(canvas);
                Assert.LessOrEqual(MenuLayout.Tablet.y * tablet, block + 0.01f, s + ": the tablet fits under the header");
                Assert.LessOrEqual(MenuLayout.TabletLeft(canvas) + MenuLayout.Tablet.x * tablet, canvas.x - MenuLayout.Margin + 0.01f, s + ": inside the right edge");
                for (int n = 1; n <= 16; n++)
                {
                    float grid = MenuLayout.GridScale(canvas, n);
                    var size = GridMath.Size(n, MenuLayout.Columns, MenuLayout.Tile, MenuLayout.Gap) * grid;
                    Assert.LessOrEqual(size.y, block + 0.01f, s + ", " + n + " gladiators: the grid fits its height");
                    Assert.GreaterOrEqual(MenuLayout.TabletLeft(canvas) - MenuLayout.GridRight(canvas, n), DioramaGap, s + ", " + n + " gladiators: the diorama stays clear");
                    // P3: with the picked tile's wreath above it
                    float roster = MainMenu.RosterScale(canvas, n);
                    Assert.LessOrEqual(roster, grid + 1e-5f, "never larger than the plain grid");
                    var withWreath = GridMath.Size(n, MenuLayout.Columns, MenuLayout.Tile, MenuLayout.Gap) * roster;
                    Assert.LessOrEqual(withWreath.y + RosterGrid.WreathRoom, block + 0.01f, s + ", " + n + " gladiators: the grid and the wreath above it fit");
                }
                for (int n = 1; n <= 12; n++)
                {
                    float arenas = ArenaLadder.GridScale(canvas, n);
                    var size = ArenaLadder.FrameSize(n) * arenas;
                    Assert.LessOrEqual(size.y, block + 0.01f, s + ", " + n + " arenas: the grid fits its height");
                    Assert.GreaterOrEqual(MenuLayout.TabletLeft(canvas) - ArenaLadder.GridRight(canvas, n), DioramaGap - 0.01f, s + ", " + n + " arenas: the diorama stays clear");
                }
            }
        }

        [Test]
        public void At1080p_SixteenGladiatorsAndSixArenas_ShowAtFullSize()
        {
            foreach (var s in new[] { new Vector2Int(1920, 1080), new Vector2Int(1680, 1050) })
            {
                var canvas = GridMath.CanvasSize(s.x, s.y, MenuLayout.Reference, MenuLayout.Match);
                Assert.AreEqual(1f, MenuLayout.GridScale(canvas, 16), s + ": 16 tiles, no shrinking, no scrolling");
                Assert.AreEqual(1f, ArenaLadder.GridScale(canvas, 6), s + ": the six arenas' grid");
                Assert.AreEqual(1f, MainMenu.RosterScale(canvas, 12), s + ": twelve gladiators and the wreath above them");
                Assert.AreEqual(1f, MenuLayout.TabletScale(canvas), s + ": the tablet");
            }
            var hd = GridMath.CanvasSize(1920f, 1080f, MenuLayout.Reference, MenuLayout.Match);
            Assert.Less(MenuLayout.GridScale(hd, 20), 1f, "more than 16: the grid shrinks to fit (it never slides)");
        }

        [Test]
        public void TheArenaGrid_IsThreeToARow_InsideItsFrame()
        {
            Assert.AreEqual(3, ArenaLadder.Columns);
            var t = ArenaLadder.TileSize;
            float inset = ArenaLadder.Inset, gap = ArenaLadder.Gap;
            Assert.AreEqual(new Vector2(3f * t.x + 2f * gap + 2f * inset, 2f * t.y + gap + 2f * inset), ArenaLadder.FrameSize(6), "six arenas: two full rows");
            Assert.AreEqual(ArenaLadder.FrameSize(3).x, ArenaLadder.FrameSize(7).x, "more arenas are only more rows");
            Assert.AreEqual(ArenaLadder.FrameSize(6).y + t.y + gap, ArenaLadder.FrameSize(7).y);
            Assert.AreEqual(new Vector2(inset + t.x * 0.5f, -inset - t.y * 0.5f), ArenaLadder.Cell(0), "the Slave Pit top-left");
            Assert.AreEqual(new Vector2(inset + t.x * 2.5f + 2f * gap, -inset - t.y * 0.5f), ArenaLadder.Cell(2), "the third ends the row");
            Assert.AreEqual(new Vector2(inset + t.x * 0.5f, -inset - t.y * 1.5f - gap), ArenaLadder.Cell(3), "the fourth starts the next");
        }

        [Test]
        public void ARun_CountsForItsGladiator_WonOrLost()
        {
            var data = new SaveData();
            MetaProgress.Apply(new RunSummary { Outcome = RunOutcome.Died, Elapsed = 60f, GladiatorId = "pit_goblin" }, null, data);
            MetaProgress.Apply(new RunSummary { Outcome = RunOutcome.Survived, Elapsed = 480f, GladiatorId = "pit_goblin" }, null, data);
            MetaProgress.Apply(new RunSummary { Outcome = RunOutcome.Died, Elapsed = 30f, GladiatorId = "nobody" }, null, data);
            Assert.AreEqual(2, SaveData.Count(data.gladiatorRuns, "pit_goblin"));
            Assert.AreEqual(1, SaveData.Count(data.gladiatorWins, "pit_goblin"));
            Assert.AreEqual(1, SaveData.Count(data.gladiatorRuns, "nobody"));
            Assert.AreEqual(0, SaveData.Count(data.gladiatorWins, "nobody"));
        }

        [Test]
        public void AnOlderSave_SeedsItsRunsFromItsWins()
        {
            // a file from before R9: wins per gladiator, no runs per gladiator
            var old = JsonUtility.FromJson<SaveData>("{\"version\":1,\"runs\":5,\"victories\":3,\"gladiatorWins\":[{\"id\":\"nobody\",\"count\":2},{\"id\":\"blood_knight\",\"count\":1}]}");
            old.Migrate();
            Assert.AreEqual(2, SaveData.Count(old.gladiatorRuns, "nobody"), "every win was a run");
            Assert.AreEqual(1, SaveData.Count(old.gladiatorRuns, "blood_knight"));
            // a new file keeps its own counts
            var fresh = new SaveData();
            SaveData.Add(fresh.gladiatorRuns, "nobody", 4);
            SaveData.Add(fresh.gladiatorWins, "nobody", 1);
            fresh.Migrate();
            Assert.AreEqual(4, SaveData.Count(fresh.gladiatorRuns, "nobody"));
        }

        static void AssertNear(Vector2 expected, Vector2 actual, string message = null)
        {
            Assert.AreEqual(expected.x, actual.x, 0.5f, message);
            Assert.AreEqual(expected.y, actual.y, 0.5f, message);
        }
    }
}
