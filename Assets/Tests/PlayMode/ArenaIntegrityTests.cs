using System.Collections;
using System.Collections.Generic;
using System.Text;
using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gladiator.Tests
{
    /// <summary>
    /// P1 (arena integrity, the producer's playtest of v0.17.0: a safe perch on the Slave Pit's platforms, getting out
    /// behind them, gates that showed a way out), in all six arenas:
    /// - EdgeWalk: the real motor, with both air jumps, walks, jumps and slides straight at the boundary loop every
    ///   2.5 m round the arena, and again from every raised spot near the edge (berm slopes, terraces, platforms); the
    ///   walls must hold on their own: ArenaBounds' last-resort clamp may never fire.
    /// - WallHug (Slave Pit, Grand Arena): a convict held at the horde's edge still hits a player against the wall.
    /// - PerchAudit: every surface a player could stand on inside the loop (a 0.5 m grid of downward rays, headroom for
    ///   the capsule, NoPerch tops excluded) must have ground enemies can stand on within 2 m with |dy| &lt; 1 (their
    ///   melee's reach), using the enemies' own play area, obstacles and height grid. Clusters of 2+ samples fail;
    ///   every perch, and the widest gap between a wall-hugging player and the horde, is logged ("[Integrity]").
    /// </summary>
    public class ArenaIntegrityTests
    {
        const string Dir = "Assets/Scenes/Arenas/";
        const float Dt = 1f / 60f;
        const float Spacing = 0.5f;      // the audit's grid (m)
        const float EnemyRadius = 0.36f; // a convict
        const float MeleeDy = 1f;        // EnemyManager: melee needs |dy| < 1
        const float SearchRadius = 2f;   // how far round a spot the audit looks for the horde's ground (m)
        const float ContactReach = 0.86f; // a convict's touch: its radius + the player's 0.35 + 0.15

        Player _player;
        PlayerMotor _motor;
        ScriptedInput _input;
        ArenaBounds _bounds;
        EnemyManager _em;

        [TearDown]
        public void TearDown()
        {
            DevSettings.ResetAll();
            TimeController.ResetForNewRun();
        }

        IEnumerator Boot(string scene)
        {
            DevSettings.ResetAll();
            DevSettings.GodMode = true;
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(Dir + scene + ".unity", LoadSceneMode.Single);
            Assert.IsNotNull(op, scene + " not in the build settings");
            while (!op.isDone) yield return null;
            yield return null;
            float end = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < end && RunIntro.Instance != null && !RunIntro.Instance.Done) yield return null;
            if (Director.Instance != null) Director.Instance.enabled = false;
            var lvl = Object.FindAnyObjectByType<LevelUpController>();
            if (lvl != null) lvl.enabled = false;
            _player = Player.Main;
            _motor = _player.GetComponent<PlayerMotor>();
            _input = new ScriptedInput();
            _player.OverrideSource = _input;
            _player.GetComponent<WeaponSystem>().RemoveAll();
            _em = EnemyManager.Instance;
            _bounds = ArenaBounds.Instance;
            Assert.IsNotNull(_bounds, scene + ": no ArenaBounds (rebuild the arena from its regenerated layout)");
            Assert.IsTrue(_bounds.Valid, scene + ": an empty boundary loop");
            yield return null;
        }

        static Vector2 XZ(Vector3 v) => new Vector2(v.x, v.z);

        // ------------------------------------------------------------------ the edge walk

        [UnityTest, Timeout(240000)] public IEnumerator EdgeWalk_SlavePit() => EdgeWalk("SlavePit");
        [UnityTest, Timeout(240000)] public IEnumerator EdgeWalk_ProvincialPit() => EdgeWalk("ProvincialPit");
        [UnityTest, Timeout(240000)] public IEnumerator EdgeWalk_StoneAmphitheater() => EdgeWalk("StoneAmphitheater");
        [UnityTest, Timeout(240000)] public IEnumerator EdgeWalk_GrandArena() => EdgeWalk("GrandArena");
        [UnityTest, Timeout(240000)] public IEnumerator EdgeWalk_ImperialArena() => EdgeWalk("ImperialArena");
        [UnityTest, Timeout(240000)] public IEnumerator EdgeWalk_Colosseum() => EdgeWalk("Colosseum");

        /// <summary>Ground under (x, z) the player could start from (the top standable hit with room for the capsule).</summary>
        bool StartOn(float x, float z, out Vector3 at)
        {
            at = default;
            var hits = Physics.RaycastAll(new Vector3(x, 30f, z), Vector3.down, 60f, Layers.SolidMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => b.point.y.CompareTo(a.point.y));
            foreach (var h in hits)
            {
                if (h.normal.y < 0.64f || h.collider.TryGetComponent<NoPerch>(out _)) continue;
                if (!Headroom(h.point)) continue;
                at = h.point + Vector3.up * 0.05f;
                return true;
            }
            return false;
        }

        /// <summary>Room for the smallest capsule (the goblin's 0.28 m) standing at `foot`, and `foot` not inside a closed
        /// mesh (the floor under a hill or a stage: an upward ray meets the back of a surface facing up).</summary>
        static bool Headroom(Vector3 foot)
        {
            if (Physics.CheckCapsule(foot + Vector3.up * 0.31f, foot + Vector3.up * 1.2f, 0.28f, Layers.SolidMask, QueryTriggerInteraction.Ignore))
                return false;
            bool backfaces = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            bool inside = Physics.Raycast(foot + Vector3.up * 0.05f, Vector3.up, out var hit, 40f, Layers.SolidMask, QueryTriggerInteraction.Ignore) && hit.normal.y > 0.1f;
            Physics.queriesHitBackfaces = backfaces;
            return !inside;
        }

        /// <summary>Drives the motor (and the player's input read) tick by tick in this frame: run straight along
        /// `dir`, with `jumps` presses (ground + air) and/or a slide.</summary>
        void Drive(Vector3 from, Vector2 dir, int ticks, int jumps, bool slide)
        {
            _motor.Teleport(from);
            Physics.SyncTransforms();
            _input.Next = default;
            for (int t = 0; t < ticks; t++)
            {
                _input.Next.Move = dir;
                _input.Next.JumpHeld = jumps > 0;
                if (jumps > 0 && t % 22 == 2 && t / 22 < jumps) _input.Next.JumpPressed = true;
                _input.Next.SlideHeld = slide && t > 10;
                if (slide && t == 11) _input.Next.SlidePressed = true;
                _player.ReferenceYaw = 0f;
                _player.Tick(Dt, Dt);
                _motor.Tick(Dt, Dt);
            }
        }

        IEnumerator EdgeWalk(string scene)
        {
            yield return Boot(scene);
            var cfg = Object.Instantiate(_motor.config); // both Winged Sandals, on a copy (never the asset)
            cfg.extraJumps = 2;
            _motor.config = cfg;
            var poly = _bounds.polygon;
            int n = poly.Length, runs = 0, skipped = 0;
            var leaks = new StringBuilder();
            int clampsBefore = _bounds.Clamps;
            void Run(Vector3 from, Vector2 outward, string where)
            {
                foreach (var (jumps, slide) in new[] { (0, false), (3, false), (0, true), (3, true) })
                {
                    int before = _bounds.Clamps;
                    Drive(from, outward, 80, jumps, slide);
                    runs++;
                    var p = _player.transform.position;
                    if (_bounds.Clamps != before || !_bounds.Contains(p.x, p.z))
                        leaks.Append($"\n  {where} from {from} ({(jumps > 0 ? "jumping" : "on foot")}{(slide ? ", sliding" : "")}): clamped at {_bounds.LastLeak}");
                }
            }

            // every 2.5 m round the loop, starting 1.5 m inside it
            float wind = 0f;
            for (int i = 0, j = n - 1; i < n; j = i++) wind += poly[j].x * poly[i].y - poly[i].x * poly[j].y;
            wind = wind >= 0f ? 1f : -1f;
            for (int k = 0; k < n; k++)
            {
                Vector2 a = poly[k], b = poly[(k + 1) % n], d = b - a;
                float len = d.magnitude;
                if (len < 0.05f) continue;
                Vector2 inward = new Vector2(-d.y, d.x) / len * wind;
                int m = Mathf.Max(1, Mathf.CeilToInt(len / 2.5f));
                for (int s = 0; s < m; s++)
                {
                    Vector2 q = a + d * ((s + 0.5f) / m), start = q + inward * 1.5f;
                    if (!_bounds.Contains(start.x, start.y) || !StartOn(start.x, start.y, out var at)) { skipped++; continue; }
                    Run(at, -inward, $"edge {k}");
                }
            }

            // and from every raised standable spot within 3 m of the loop (berm slopes, terraces, platform tops)
            int raised = 0;
            foreach (var spot in Standable(out _))
            {
                if (spot.p.y < 0.5f || _bounds.DistanceToEdge(spot.p.x, spot.p.z) > 3f) continue; // every arena's floor is at y 0
                if ((raised++ % 3) != 0) continue; // every third: they come in 0.5 m rows
                Nearest(XZ(spot.p), out var q);
                Vector2 outward = (q - XZ(spot.p)).sqrMagnitude > 1e-6f ? (q - XZ(spot.p)).normalized : Vector2.up;
                Run(spot.p + Vector3.up * 0.05f, outward, "raised spot");
            }
            Debug.Log($"[Integrity] {scene} edge walk: {runs} runs ({n} loop corners, {raised} raised spots, {skipped} starts with no room), leaks: {(leaks.Length == 0 ? "none" : leaks.ToString())}");
            Assert.AreEqual(clampsBefore, _bounds.Clamps, scene + ": the walls let the player through (the clamp had to put them back):" + leaks);
        }

        void Nearest(Vector2 p, out Vector2 nearest)
        {
            var poly = _bounds.polygon;
            float best = float.MaxValue;
            nearest = p;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[j], ab = poly[i] - a;
                float s = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                Vector2 c = a + ab * s;
                if ((p - c).sqrMagnitude < best) { best = (p - c).sqrMagnitude; nearest = c; }
            }
        }

        // ------------------------------------------------------------------ the wall-hug reach

        /// <summary>The producer's "he can hit the mobs but they cant hit the player", against a wall: a convict chasing a
        /// player who stands against it is held at the horde's edge, more than its touch (0.86 m) away, and still hits
        /// them (EnemyManager.HeldReach). The Slave Pit's oval (a fence, bearing 90) and the Grand Arena's outline.</summary>
        [UnityTest, Timeout(120000)]
        public IEnumerator WallHug_AConvictHeldAtTheEdge_HitsThePlayer_SlavePit() =>
            WallHug("SlavePit", new Vector3(36.5f, 0f, 0f), Vector2.right);

        [UnityTest, Timeout(120000)]
        public IEnumerator WallHug_AConvictHeldAtTheEdge_HitsThePlayer_GrandArena() =>
            WallHug("GrandArena", new Vector3(21.5f, 0f, 60f), Vector2.right);

        IEnumerator WallHug(string scene, Vector3 from, Vector2 outward)
        {
            yield return Boot(scene);
            DevSettings.GodMode = false;
            from.y = _em.Height.Sample(from.x, from.z) + 0.05f;
            _motor.Teleport(from);
            _input.Next = default;
            _input.Next.Move = outward; // walk into the wall and stay there
            float t = 0f;
            while (t < 1.5f) { t += Time.deltaTime; yield return null; }
            _input.Next = default;
            yield return null;
            var p = _player.transform.position;
            Assert.Less(_bounds.DistanceToEdge(p.x, p.z), 0.5f, $"the player stands against the wall ({p})");
            Assert.Greater(_em.PlayerPastEdge, 0f, "past the horde's edge");

            var convict = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyDef>("Assets/Data/Enemies/EN_Convict.asset");
            int slot = _em.Spawn(convict, from);
            Assert.GreaterOrEqual(slot, 0);
            var health = _player.GetComponent<PlayerHealth>();
            float before = health.DamageTaken, gap = 0f, end = Time.time + 8f;
            bool held = false;
            while (Time.time < end && health.DamageTaken <= before)
            {
                if (_em.IsAlive(slot))
                {
                    held |= _em.IsHeldAtEdge(slot);
                    var e = _em.GetPosition(slot);
                    gap = new Vector2(e.x - p.x, e.z - p.z).magnitude;
                }
                yield return null;
            }
            if (_em.IsAlive(slot)) held |= _em.IsHeldAtEdge(slot); // the loop leaves on the hit's frame, before sampling it
            Debug.Log($"[Integrity] {scene} wall hug: the player {_em.PlayerPastEdge:0.00} m past the edge, the convict {gap:0.00} m away when it hit (held: {held})");
            Assert.IsTrue(held, "the convict ended up held at the edge");
            Assert.Greater(gap, 0.86f, "out of its old touch (radius + 0.35 + 0.15)");
            Assert.Greater(health.DamageTaken, before, "and it still hits the wall-hugging player");
        }

        // ------------------------------------------------------------------ the perch drop

        /// <summary>P3 note 20 (the producer's playtest of v0.24.0: on top of a Slave Pit cage "i can hit but not get hit
        /// back"): the real motor, dropped onto every NoPerch top inside the loop (its middle and four corners), standing
        /// still or walking back onto it, with the starter's capsule and the Murder Machine's (the widest): within 1.5 s
        /// it must be off the top (its feet under the top, or outside its footprint).</summary>
        [UnityTest, Timeout(240000)] public IEnumerator PerchDrop_SlavePit() => PerchDrop("SlavePit");
        [UnityTest, Timeout(240000)] public IEnumerator PerchDrop_ProvincialPit() => PerchDrop("ProvincialPit");
        [UnityTest, Timeout(240000)] public IEnumerator PerchDrop_StoneAmphitheater() => PerchDrop("StoneAmphitheater");
        [UnityTest, Timeout(240000)] public IEnumerator PerchDrop_GrandArena() => PerchDrop("GrandArena");
        [UnityTest, Timeout(240000)] public IEnumerator PerchDrop_ImperialArena() => PerchDrop("ImperialArena");
        [UnityTest, Timeout(240000)] public IEnumerator PerchDrop_Colosseum() => PerchDrop("Colosseum");

        IEnumerator PerchDrop(string scene)
        {
            yield return Boot(scene);
            var tops = new List<Collider>();
            foreach (var np in Object.FindObjectsByType<NoPerch>(FindObjectsInactive.Exclude))
            {
                var col = np.GetComponent<Collider>();
                if (col == null || !col.enabled) continue;
                var c = col.bounds.center;
                if (_bounds.Contains(c.x, c.z)) tops.Add(col);
            }
            Assert.Greater(tops.Count, 0, scene + ": no NoPerch tops inside the loop");
            var configs = new[]
            {
                _motor.config,
                UnityEditor.AssetDatabase.LoadAssetAtPath<MovementConfig>("Assets/Data/Config/MovementConfig_Machine.asset"),
            };
            var original = _motor.config;
            var fails = new StringBuilder();
            int drops = 0, failed = 0;
            foreach (var cfg in configs)
            {
                if (cfg == null) continue;
                _motor.config = cfg;
                var cc = _player.GetComponent<CharacterController>(); // the motor sizes it only at Awake and on slides
                cc.height = cfg.capsuleHeight;
                cc.radius = cfg.capsuleRadius;
                cc.center = new Vector3(0f, cfg.capsuleHeight * 0.5f, 0f);
                foreach (var col in tops)
                {
                    var b = col.bounds;
                    float ix = Mathf.Max(0f, b.extents.x - 0.15f), iz = Mathf.Max(0f, b.extents.z - 0.15f);
                    for (int k = 0; k < 5; k++)
                    for (int walk = 0; walk < 2; walk++)
                    {
                        var at = new Vector3(b.center.x + (k == 0 ? 0f : (k & 1) == 0 ? -ix : ix), b.max.y + 0.3f,
                                             b.center.z + (k == 0 ? 0f : k < 3 ? -iz : iz));
                        if (!_bounds.Contains(at.x, at.z)) continue;
                        _motor.Teleport(at);
                        Physics.SyncTransforms();
                        _input.Next = default;
                        for (int t = 0; t < 90; t++)
                        {
                            var p = _player.transform.position;
                            var toMiddle = new Vector2(b.center.x - p.x, b.center.z - p.z);
                            _input.Next.Move = walk == 1 && toMiddle.sqrMagnitude > 1e-4f ? toMiddle.normalized : Vector2.zero;
                            _player.ReferenceYaw = 0f;
                            _player.Tick(Dt, Dt);
                            _motor.Tick(Dt, Dt);
                        }
                        drops++;
                        var end = _player.transform.position;
                        bool over = end.x > b.min.x - cfg.capsuleRadius && end.x < b.max.x + cfg.capsuleRadius
                                 && end.z > b.min.z - cfg.capsuleRadius && end.z < b.max.z + cfg.capsuleRadius;
                        if (over && end.y > b.max.y - 0.3f)
                        {
                            failed++;
                            if (fails.Length < 1500)
                                fails.Append($"\n  {cfg.name} on {col.name} ({col.transform.parent?.name}) top {b.max.y:0.00} from {at} {(walk == 1 ? "walking back" : "still")}: ended at {end}");
                        }
                    }
                }
            }
            _motor.config = original;
            var cc0 = _player.GetComponent<CharacterController>();
            cc0.height = original.capsuleHeight;
            cc0.radius = original.capsuleRadius;
            cc0.center = new Vector3(0f, original.capsuleHeight * 0.5f, 0f);
            Debug.Log($"[Integrity] {scene} perch drop: {tops.Count} NoPerch tops, {drops} drops, {failed} stayed on top{fails}");
            Assert.AreEqual(0, failed, scene + ": stayed on a NoPerch top" + fails);
        }

        // ------------------------------------------------------------------ the perch audit

        [UnityTest, Timeout(240000)] public IEnumerator PerchAudit_SlavePit() => PerchAudit("SlavePit");
        [UnityTest, Timeout(240000)] public IEnumerator PerchAudit_ProvincialPit() => PerchAudit("ProvincialPit");
        [UnityTest, Timeout(240000)] public IEnumerator PerchAudit_StoneAmphitheater() => PerchAudit("StoneAmphitheater");
        [UnityTest, Timeout(240000)] public IEnumerator PerchAudit_GrandArena() => PerchAudit("GrandArena");
        [UnityTest, Timeout(240000)] public IEnumerator PerchAudit_ImperialArena() => PerchAudit("ImperialArena");
        [UnityTest, Timeout(240000)] public IEnumerator PerchAudit_Colosseum() => PerchAudit("Colosseum");

        struct Spot
        {
            public Vector3 p;
            public float ground; // height over the enemies' height grid there
            public int ix, iz;
            public string what;
        }

        /// <summary>Every surface a player could stand on inside the loop, on a Spacing grid.</summary>
        List<Spot> Standable(out Vector2Int size)
        {
            var poly = _bounds.polygon;
            Vector2 lo = poly[0], hi = poly[0];
            foreach (var v in poly) { lo = Vector2.Min(lo, v); hi = Vector2.Max(hi, v); }
            size = new Vector2Int(Mathf.CeilToInt((hi.x - lo.x) / Spacing) + 1, Mathf.CeilToInt((hi.y - lo.y) / Spacing) + 1);
            var spots = new List<Spot>();
            var hits = new RaycastHit[24];
            for (int iz = 0; iz < size.y; iz++)
            for (int ix = 0; ix < size.x; ix++)
            {
                float x = lo.x + ix * Spacing, z = lo.y + iz * Spacing;
                if (!_bounds.Contains(x, z) || _bounds.DistanceToEdge(x, z) < 0.25f) continue;
                int k = Physics.RaycastNonAlloc(new Vector3(x, 40f, z), Vector3.down, hits, 80f, Layers.SolidMask, QueryTriggerInteraction.Ignore);
                float grid = _em.Height.Sample(x, z);
                for (int h = 0; h < k; h++)
                {
                    var hit = hits[h];
                    if (hit.normal.y < 0.64f || hit.collider.TryGetComponent<NoPerch>(out _)) continue; // steeper than 50°
                    if (hit.point.y - grid > 6.2f) continue; // over any jump from the highest walkable
                    if (!Headroom(hit.point)) continue;
                    spots.Add(new Spot { p = hit.point, ground = hit.point.y - grid, ix = ix, iz = iz, what = hit.collider.name });
                }
            }
            return spots;
        }

        /// <summary>The nearest horde-reachable ground to `s` (XZ distance) with |dy| &lt; MeleeDy, searched in rings
        /// out to SearchRadius; +inf if none.</summary>
        float HordeDistance(Vector3 s)
        {
            if (HordeCanStand(s.x, s.z, s.y)) return 0f;
            for (float r = 0.25f; r <= SearchRadius + 1e-3f; r += 0.25f)
            {
                int dirs = Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * r / 0.25f));
                for (int k = 0; k < dirs; k++)
                {
                    float a = k * Mathf.PI * 2f / dirs;
                    if (HordeCanStand(s.x + Mathf.Cos(a) * r, s.z + Mathf.Sin(a) * r, s.y)) return r;
                }
            }
            return float.PositiveInfinity;
        }

        bool HordeCanStand(float x, float z, float y)
        {
            var q = new Vector3(x, 0f, z);
            if (_em.EdgeDistance01(q, EnemyRadius) > 1f) return false;    // outside their play area
            if (_em.Obstacles != null && _em.Obstacles.PushOut(ref q, EnemyRadius)) return false; // in a wall or a prop
            return Mathf.Abs(y - _em.Height.Sample(x, z)) < MeleeDy;
        }

        IEnumerator PerchAudit(string scene)
        {
            yield return Boot(scene);
            var spots = Standable(out var size);
            var perch = new Dictionary<long, int>(); // grid cell → spot index
            float worstHug = 0f;
            Vector3 worstHugAt = default;
            int hugOverContact = 0;
            for (int i = 0; i < spots.Count; i++)
            {
                var s = spots[i];
                float d = HordeDistance(s.p);
                if (float.IsPositiveInfinity(d)) { perch[(long)s.iz * size.x + s.ix] = i; continue; }
                if (_bounds.DistanceToEdge(s.p.x, s.p.z) < 2.5f)
                {
                    if (d > worstHug) { worstHug = d; worstHugAt = s.p; }
                    if (d > ContactReach) hugOverContact++;
                }
            }
            // clusters of perch samples (4-neighbours on the grid)
            var seen = new HashSet<long>();
            var report = new StringBuilder();
            int failing = 0, clusters = 0;
            foreach (var kv in perch)
            {
                if (seen.Contains(kv.Key)) continue;
                var stack = new Stack<long>();
                stack.Push(kv.Key);
                seen.Add(kv.Key);
                int count = 0;
                Vector3 sum = Vector3.zero;
                float yLo = float.MaxValue, yHi = float.MinValue, gHi = 0f;
                var names = new HashSet<string>();
                while (stack.Count > 0)
                {
                    long c = stack.Pop();
                    var s = spots[perch[c]];
                    count++;
                    sum += s.p;
                    yLo = Mathf.Min(yLo, s.p.y);
                    yHi = Mathf.Max(yHi, s.p.y);
                    gHi = Mathf.Max(gHi, s.ground);
                    names.Add(s.what);
                    foreach (long nb in new[] { c - 1, c + 1, c - size.x, c + size.x })
                        if (perch.ContainsKey(nb) && !seen.Contains(nb)) { seen.Add(nb); stack.Push(nb); }
                }
                clusters++;
                if (count >= 2) failing++;
                if (clusters <= 40)
                    report.Append($"\n  {(count >= 2 ? "PERCH" : "single")} {count * Spacing * Spacing:0.00} m² at {sum / count} (y {yLo:0.00}-{yHi:0.00}, up to {gHi:0.00} m over the grid) on {string.Join(", ", names)}");
            }
            Debug.Log($"[Integrity] {scene} perch audit: {spots.Count} standable samples, {perch.Count} out of the horde's reach in {clusters} clusters ({failing} of 2+ samples)." +
                      $" Wall-hug: worst gap {worstHug:0.00} m at {worstHugAt}, {hugOverContact} edge samples beyond a convict's touch ({ContactReach} m){report}");
            Assert.Greater(spots.Count, 1000, scene + ": the audit found almost nothing to stand on (is the scene built?)");
            Assert.AreEqual(0, failing, scene + ": spots the player can stand on but the horde can't reach:" + report);
        }
    }
}
