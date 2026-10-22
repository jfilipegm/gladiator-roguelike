using System.Collections;
using System.Collections.Generic;
using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gladiator.Tests
{
    /// <summary>
    /// R3 (docs/tracks/R3-design.md, "Chain"): the Labyrinth Chain's Sweep pattern. The chain travels across its arc and
    /// hits what it passes, once a sweep, in order, not past its reach or behind it; the stone crits and shoves harder;
    /// the push is sideways; triggers alternate and Amount adds backswings with no wind-up; a Shield Thug facing the
    /// player takes it in full while a boss's raised shield blocks it; a whirl hits all round once; the model's Chain part
    /// is hidden while it swings; a sweep stops when the player goes down; and it allocates nothing per frame.
    ///
    /// The weapon is made here with the chain's numbers (so the tests don't wait for the setup part); the Nobody holds it
    /// alone, standing still at the TestArena spawn among rooted dummies, with no Director, no level-ups and god mode.
    /// </summary>
    public class ChainSweepTests
    {
        const string Scene = "Assets/Scenes/Dev/TestArena.unity";
        const string ChainModel = "Assets/Art/Models/Weapons/WP_LabyrinthChain.fbx";

        struct Struck { public int Index; public float Time, Damage; public bool Crit, Blocked; }

        EnemyManager _em;
        WeaponSystem _weapons;
        PlayerPartAnimator _animator;
        PlayerHealth _health;
        Vector3 _p;
        WeaponDef _chain;
        EnemyDef _dummy, _loose;
        readonly List<EnemyDef> _made = new List<EnemyDef>();
        readonly List<Struck> _hits = new List<Struck>();

        void OnHit(in HitEvent e)
        {
            if (_chain != null && e.SourceId == _chain.RuntimeId)
                _hits.Add(new Struck { Index = e.Index, Time = Time.time, Damage = e.Damage, Crit = e.Crit, Blocked = e.Blocked });
        }

        IEnumerator Boot()
        {
            DevSettings.ResetAll();
            DevSettings.GodMode = true;
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            GladiatorLoadout.NextOverride = null;
            GameSession.Clear(); // the Nobody
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            while (RunIntro.Instance != null && !RunIntro.Instance.Done) yield return null;
            var spawner = Object.FindAnyObjectByType<DebugEnemySpawner>();
            if (spawner != null) spawner.enabled = false;
            if (Director.Instance != null) Director.Instance.enabled = false; // the tests place their own enemies
            var lvl = Object.FindAnyObjectByType<LevelUpController>();
            if (lvl != null) lvl.enabled = false; // a level-up would pause the clock and the waits never end
            var player = Player.Main;
            player.OverrideSource = new ScriptedInput();
            _weapons = player.GetComponent<WeaponSystem>();
            _animator = player.GetComponent<PlayerPartAnimator>();
            _health = player.GetComponent<PlayerHealth>();
            _health.invulnerableTime = 0f;
            _weapons.RemoveAll();
            _weapons.WhirlSweeps = false;
            _em = EnemyManager.Instance;
            _em.KillAll();
            // the walls and pillars round the spawn don't push the dummies off their marks
            _em.Obstacles.ToArrays(out var circles, out var boxes, out _);
            for (int k = 0; k < circles.Length; k++) _em.Obstacles.SetCircleActive(k, false);
            for (int k = 0; k < boxes.Length; k++) _em.Obstacles.SetBoxActive(k, false);

            _chain = Chain();
            _dummy = Made(R2TestArena.Dummy("test_chain_dummy"));
            _loose = Made(R2TestArena.Dummy("test_chain_loose"));
            _loose.knockbackTaken = 1f; // shoved like a Convict, but rooted otherwise
            _em.Prewarm(_dummy, 8);
            _em.Prewarm(_loose, 4);
            yield return new WaitForSeconds(0.3f);
            PickupSystem.Instance.ClearAll();
            _p = player.transform.position;
            _hits.Clear();
            GameEvents.OnHit.Subscribe(OnHit);
        }

        [TearDown]
        public void TearDown()
        {
            if (Player.Main != null) Player.Main.SetPaused(false);
            GameEvents.OnHit.Unsubscribe(OnHit);
            GameLoop.TrackHeapPerSystem = false;
            DevSettings.ResetAll();
            TimeController.ResetForNewRun();
            GladiatorLoadout.NextOverride = null;
            GameSession.Clear();
            if (_chain != null) Object.Destroy(_chain);
            foreach (var d in _made) if (d != null) Object.Destroy(d);
            _made.Clear();
        }

        EnemyDef Made(EnemyDef d) { _made.Add(d); return d; }

        /// <summary>WP_LabyrinthChain's numbers (R3-design's setup list), the meshes from the art's FBX when it's imported
        /// (else none: nothing is drawn). It never crits at random, so only the stone's hits are crits.</summary>
        static WeaponDef Chain(int amount = 1)
        {
            var w = ScriptableObject.CreateInstance<WeaponDef>();
            w.id = "test_labyrinth_chain";
            w.displayName = "Labyrinth Chain (test)";
            w.pattern = WeaponPattern.Sweep;
            w.damage = 15f; w.critChance = -1f; w.critMultiplier = 2f; w.knockback = 7f;
            w.cooldown = 1.6f; w.swingDuration = 0.8f; w.windup = 0.32f;
            w.range = 5.2f; w.arcDegrees = 240f; w.width = 0.5f; w.acquireRange = 5.6f; w.amount = amount; w.tipLength = 1.4f;
            w.sweepTime = 0.3f; w.whirlTimeMul = 1.5f; w.sweepOutward = 0.35f; w.tipKnockbackMul = 1.6f;
            w.linkLength = 0.6f; w.chainHeight = new Vector2(1.05f, 0.55f); w.weightOffset = 0.33f; w.meshScale = 1f;
            w.modelPart = "Chain";
            foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(ChainModel))
                if (o is Mesh m) { if (m.name == "Link") w.linkMesh = m; else if (m.name == "Weight") w.mesh = m; }
            return w;
        }

        WeaponInstance Equip(WeaponDef def)
        {
            var w = _weapons.Equip(def);
            Assert.IsNotNull(w, "the chain equips");
            return w;
        }

        /// <summary>An enemy `dist` m from the player at `bearing` (degrees from +Z, positive to the right).</summary>
        int Put(EnemyDef def, float bearing, float dist)
        {
            int i = _em.Spawn(def, _p + Quaternion.Euler(0f, bearing, 0f) * Vector3.forward * dist);
            Assert.GreaterOrEqual(i, 0);
            return i;
        }

        int HitsOn(int i) { int n = 0; foreach (var h in _hits) if (h.Index == i) n++; return n; }

        Struck First(int i, int nth = 0)
        {
            int seen = 0;
            foreach (var h in _hits) if (h.Index == i && seen++ == nth) return h;
            Assert.Fail("enemy " + i + " was hit " + seen + " times, not " + (nth + 1));
            return default;
        }

        Vector3 Pos(int i) => R2TestArena.Flat(_em.GetPosition(i));
        Vector3 OutwardOf(int i) => R2TestArena.Flat(_em.GetPosition(i) - _p).normalized;
        static float YawOf(Vector3 f) => Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        static IEnumerator Until(System.Func<bool> done, float seconds) => R2TestArena.Until(done, seconds);

        /// <summary>Waits until `n` sweeps have begun and the last of them has ended.</summary>
        IEnumerator SweepsDone(WeaponInstance w, int n, float seconds = 4f)
        {
            yield return Until(() => _weapons.Sweeps >= n && w.SweepT < 0f, seconds);
            Assert.GreaterOrEqual(_weapons.Sweeps, n, "the chain swept");
            Assert.Less(w.SweepT, 0f, "and the sweep ended");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheChain_HitsWhatItPasses_OnceASweep_NotPastItsReach_OrBehind()
        {
            yield return Boot();
            var w = Equip(_chain);
            int near = Put(_dummy, 0f, 3f);       // the nearest: the aim
            int far = Put(_dummy, 30f, 6.5f);     // past 5.2 m + its radius
            int behind = Put(_dummy, 180f, 3.5f); // outside the 240° arc
            yield return SweepsDone(w, 1);
            Assert.AreEqual(1, _weapons.Sweeps);
            Assert.AreEqual(1, HitsOn(near), "the one it passed: hit once");
            Assert.AreEqual(0, HitsOn(far), "past its reach");
            Assert.AreEqual(0, HitsOn(behind), "behind him");
            Assert.AreEqual(1, _weapons.SweepHits);
            yield return SweepsDone(w, 2);
            Assert.AreEqual(2, HitsOn(near), "once a sweep");
            Assert.AreEqual(_em.GetMaxHp(far), _em.GetHp(far), 1e-3f);
            Assert.AreEqual(_em.GetMaxHp(behind), _em.GetHp(behind), 1e-3f);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator EnemiesAcrossTheArc_AreHitInSweepOrder_AQuarterSecondApart_AndTheNextTriggerComesBack()
        {
            yield return Boot();
            var w = Equip(_chain);
            Put(_dummy, 0f, 2f); // the aim
            int right = Put(_dummy, 100f, 3.4f), left = Put(_dummy, -100f, 3.4f);
            yield return SweepsDone(w, 1);
            Struck r = First(right), l = First(left);
            Assert.Less(r.Time, l.Time, "the first sweep goes right to left");
            Assert.AreEqual(0.25f, l.Time - r.Time, 0.06f, "200° of 240° in 0.3 s");
            Assert.AreEqual(-1, w.SweepDir, "the next one comes back");
            yield return SweepsDone(w, 2);
            Struck r2 = First(right, 1), l2 = First(left, 1);
            Assert.Less(l2.Time, r2.Time, "the second trigger sweeps left to right");
            Assert.AreEqual(0.25f, r2.Time - l2.Time, 0.06f);
            Assert.AreEqual(1, w.SweepDir);
            Assert.IsFalse(r.Crit || l.Crit || r2.Crit || l2.Crit, "the inner chain doesn't crit");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheStone_AlwaysCrits_AndShovesHarder_TheInnerChainDoesnt()
        {
            yield return Boot();
            var w = Equip(_chain);
            int inner = Put(_loose, 0f, 2.5f); // the aim
            int stone = Put(_loose, 60f, 4.5f); // in the stone's band: 3.8-5.2 m
            Vector3 inner0 = Pos(inner), stone0 = Pos(stone);
            yield return SweepsDone(w, 1);
            Struck s = First(stone), i = First(inner);
            Assert.IsTrue(s.Crit, "the stone always crits");
            Assert.IsFalse(i.Crit, "the inner chain doesn't");
            Assert.GreaterOrEqual(s.Damage, 2f * i.Damage - 0.01f, "a crit: ×2 (plus any Crit Damage)");
            Assert.AreEqual(1, _weapons.WeightHits);
            Assert.AreEqual(2, _weapons.SweepHits);
            yield return new WaitForSeconds(0.5f);
            float ds = (Pos(stone) - stone0).magnitude, di = (Pos(inner) - inner0).magnitude;
            Assert.Greater(di, 0.2f, "the inner chain shoves too");
            Assert.Greater(ds, di * 1.3f, "the stone shoves 1.6× harder");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ThePush_IsSideways_TheWayTheChainMoves()
        {
            yield return Boot();
            var w = Equip(_chain);
            Put(_dummy, 0f, 2f); // the aim (rooted, not shoved)
            int e = Put(_loose, 50f, 3.2f);
            Vector3 at = Pos(e), outward = OutwardOf(e);
            yield return SweepsDone(w, 1);
            Assert.AreEqual(1, HitsOn(e));
            yield return new WaitForSeconds(0.4f);
            Vector3 moved = Pos(e) - at;
            Vector3 tangent = WeaponSystem.SweepPush(outward, -1f, 0f); // the first sweep turns right to left
            float side = Vector3.Dot(moved, tangent), out_ = Vector3.Dot(moved, outward);
            Assert.Greater(side, 0.2f, "swept aside the way the chain went");
            Assert.Greater(side, Mathf.Abs(out_) * 1.5f, "more along the tangent than outward");
            Assert.Greater(out_, 0f, "and a little outward");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator WithAmountTwo_TheBackswingFollowsAtOnce_TheOtherWay_WithNoWindup()
        {
            yield return Boot();
            var def = Chain(amount: 2);
            Object.Destroy(_chain);
            _chain = def;
            var w = Equip(def);
            Put(_dummy, 0f, 2f); // the aim
            int right = Put(_dummy, 100f, 3.4f), left = Put(_dummy, -100f, 3.4f);
            int swings = _weapons.Swings;
            float t1 = -1f, t2 = -1f, end = Time.time + 4f;
            while (Time.time < end && !(_weapons.Sweeps >= 2 && w.SweepT < 0f))
            {
                if (t1 < 0f && _weapons.Sweeps >= 1) t1 = Time.time;
                if (t2 < 0f && _weapons.Sweeps >= 2) t2 = Time.time;
                yield return null;
            }
            Assert.AreEqual(2, _weapons.Sweeps, "a sweep and its backswing");
            Assert.AreEqual(swings + 1, _weapons.Swings, "from one swing");
            Assert.GreaterOrEqual(t1, 0f);
            Assert.GreaterOrEqual(t2, 0f);
            Assert.AreEqual(def.sweepTime, t2 - t1, 0.1f, "the backswing starts as the first sweep ends: no wind-up");
            var order = new List<int>();
            foreach (var h in _hits) if (h.Index == right || h.Index == left) order.Add(h.Index);
            CollectionAssert.AreEqual(new[] { right, left, left, right }, order, "right to left, then straight back");
            Assert.AreEqual(1, w.SweepDir, "the next trigger starts right to left again");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AShieldThugFacingThePlayer_TakesTheChainInFull()
        {
            yield return Boot();
            var thug = Made(Object.Instantiate(R2TestArena.Load<EnemyDef>("Assets/Data/Enemies/EN_ShieldThug.asset")));
            thug.moveSpeed = 0f; thug.contactDamage = 0f; thug.maxHp = 1000f; thug.prewarm = 0;
            int i = Put(thug, 0f, 3f); // it spawns facing the player
            yield return null;
            Assert.IsTrue(_em.IsBlocking(i, OutwardOf(i)), "it faces him: a straight-out push would be blocked");
            var w = Equip(_chain);
            yield return SweepsDone(w, 1);
            var h = First(i);
            Assert.IsFalse(h.Blocked, "the chain comes round the side of the shield");
            Assert.AreEqual(_weapons.Damage(w), h.Damage, 0.01f, "in full");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ABossWithItsShieldUp_FacingThePlayer_BlocksTheChain()
        {
            yield return Boot();
            var boss = Made(R2TestArena.Dummy("test_chain_boss", EnemyBehaviour.Scripted));
            boss.blockArc = 120f; boss.blockReduction = 0.75f;
            int i = Put(boss, 0f, 3f);
            _em.SetScripted(i, _em.GetPosition(i), YawOf(-OutwardOf(i)), false);
            _em.SetScriptedShield(i, true);
            yield return null;
            Assert.IsTrue(_em.IsBlocking(i, OutwardOf(i)));
            var w = Equip(_chain);
            yield return SweepsDone(w, 1);
            var h = First(i);
            Assert.IsTrue(h.Blocked, "a boss gets the radial push: its shield blocks as before");
            Assert.AreEqual(_weapons.Damage(w) * 0.25f, h.Damage, 0.01f);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator WhirlSweeps_HitsEveryoneRoundHimOnce_AndTheSpinComesBackToZero()
        {
            yield return Boot();
            _weapons.WhirlSweeps = true;
            var w = Equip(_chain);
            var ring = new int[8];
            for (int k = 0; k < 8; k++) ring[k] = Put(_dummy, k * 45f, k == 0 ? 2.8f : 3f); // k = 4: straight behind
            float maxSpin = 0f, begun = -1f, ended = -1f, end = Time.time + 4f;
            while (Time.time < end && !(_weapons.Sweeps >= 1 && w.SweepT < 0f))
            {
                if (begun < 0f && _weapons.Sweeps >= 1) begun = Time.time;
                maxSpin = Mathf.Max(maxSpin, Mathf.Abs(_animator.AttackSpin));
                yield return null;
            }
            ended = Time.time;
            Assert.AreEqual(1, _weapons.Sweeps);
            Assert.AreEqual(1, _weapons.Whirls, "a whirl");
            for (int k = 0; k < 8; k++) Assert.AreEqual(1, HitsOn(ring[k]), "every one round him, once: " + k * 45 + "°");
            Assert.AreEqual(_chain.sweepTime * _chain.whirlTimeMul, ended - begun, 0.08f, "0.45 s all the way round");
            Assert.Greater(maxSpin, 180f, "the model turned with the chain");
            yield return null;
            Assert.AreEqual(0f, _animator.AttackSpin, "and the spin is back to 0");
            Assert.IsFalse(_animator.AttackMirror);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheModelsChainPart_IsHiddenWhileItSweeps_AndShownAfter()
        {
            yield return Boot();
            var part = new GameObject("Chain");
            part.transform.SetParent(_animator.visualRoot, false);
            var w = Equip(_chain);
            Put(_dummy, 0f, 3f);
            yield return Until(() => w.SwingT >= 0f, 2f);
            Assert.AreEqual(0, _weapons.Sweeps, "winding up");
            Assert.IsTrue(part.activeSelf, "shown through the wind-up");
            yield return Until(() => _weapons.Sweeps >= 1, 2f);
            Assert.IsTrue(w.SweepT >= 0f);
            Assert.IsFalse(part.activeSelf, "hidden while the swung chain is drawn");
            yield return Until(() => w.SweepT < 0f, 2f);
            Assert.IsTrue(part.activeSelf, "back once it's over");
            Assert.AreSame(part.transform, w.Part);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ASweepInProgress_Stops_WhenThePlayerGoesDown()
        {
            yield return Boot();
            var part = new GameObject("Chain");
            part.transform.SetParent(_animator.visualRoot, false);
            var w = Equip(_chain);
            Put(_dummy, 0f, 2f); // the aim
            int right = Put(_dummy, 100f, 3.4f), left = Put(_dummy, -100f, 3.4f);
            yield return Until(() => HitsOn(right) > 0, 3f);
            Assert.AreEqual(1, HitsOn(right), "the sweep reached the right");
            Assert.IsTrue(w.SweepT >= 0f, "and is still going");
            _health.Kill();
            int sweeps = _weapons.Sweeps, swept = _weapons.SweepHits;
            // the death beat may slow the clock: wait in real time, past the rest of the sweep
            float end = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < end && w.SweepT >= 0f) yield return null;
            end = Time.realtimeSinceStartup + 0.4f;
            while (Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(_weapons.Halted);
            Assert.AreEqual(0, HitsOn(left), "nothing more is hit");
            Assert.AreEqual(swept, _weapons.SweepHits);
            Assert.AreEqual(sweeps, _weapons.Sweeps);
            Assert.Less(w.SweepT, 0f, "the sweep is dropped");
            Assert.IsTrue(part.activeSelf, "the model's chain is back");
            Assert.AreEqual(0f, _animator.AttackSpin);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator FiveSecondsOfSweeps_Among200Enemies_AllocateNothing()
        {
            yield return Boot();
            GameEvents.OnHit.Unsubscribe(OnHit); // the test's own log grows inside the weapons' tick: not the game's
            var def = Chain(amount: 3);
            def.cooldown = 0.4f; // the chain never rests
            Object.Destroy(_chain);
            _chain = def;
            _em.Prewarm(_dummy, 200);
            int placed = 0;
            for (float r = 1.5f; r < 6.6f && placed < 200; r += 0.8f)
            {
                int n = Mathf.FloorToInt(2f * Mathf.PI * r / 0.85f);
                for (int k = 0; k < n && placed < 200; k++, placed++) Put(_dummy, k * 360f / n + r * 17f, r);
            }
            Assert.AreEqual(200, placed);
            var w = Equip(def);
            // warm up (the pools, the per-slot table, the first whirl), then measure the gameplay loop per system
            float flip = Time.time + 0.7f, warm = Time.time + 2f;
            while (Time.time < warm)
            {
                if (Time.time >= flip) { _weapons.WhirlSweeps = !_weapons.WhirlSweeps; flip = Time.time + 0.7f; }
                yield return null;
            }
            int sweeps = _weapons.Sweeps, swept = _weapons.SweepHits, whirls = _weapons.Whirls;
            GameLoop.TrackHeapPerSystem = true;
            float end = Time.time + 5f;
            while (Time.time < end)
            {
                if (Time.time >= flip) { _weapons.WhirlSweeps = !_weapons.WhirlSweeps; flip = Time.time + 0.7f; }
                yield return null;
            }
            GameLoop.TrackHeapPerSystem = false;
            string report = GameLoop.HeapReport();
            long bytes = HeapOf(report, nameof(WeaponSystem)) + HeapOf(report, nameof(PlayerProjectiles)) + HeapOf(report, nameof(PlayerPartAnimator));
            Assert.Greater(_weapons.Sweeps - sweeps, 6, "it kept sweeping (triggers of three sweeps)");
            Assert.Greater(_weapons.Whirls - whirls, 1, "whirls among them");
            Assert.Greater(_weapons.SweepHits - swept, 500, "through the crowd");
            Assert.AreEqual(0L, bytes, "no allocation per frame in the weapons, the chain's drawing or the animator:\n" + report);
            Assert.IsNotNull(w);
        }

        /// <summary>Heap growth GameLoop recorded for the system named `name` ("Name: 123B/4fr" lines).</summary>
        static long HeapOf(string report, string name)
        {
            long sum = 0;
            foreach (var line in report.Split('\n'))
            {
                if (!line.StartsWith(name + ": ")) continue;
                string rest = line.Substring(name.Length + 2);
                int b = rest.IndexOf('B');
                if (b > 0 && long.TryParse(rest.Substring(0, b), out long v)) sum += v;
            }
            return sum;
        }
    }
}
