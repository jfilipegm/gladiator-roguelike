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
    /// Phase 5.16 (docs/ARENAS.md §5): the Praetorian formation. A Centurion spawns with five Praetorians on slots
    /// 1.8 m ahead of him; they keep them as he marches and wheels; they face his front, so they block from the front
    /// but not from behind or the flank; with his line he stops 2.6 m short of you; his push telegraphs a 7 m lane and
    /// hits you in it once, not 4 m beside it; a dead Praetorian leaves a gap; killing him breaks the line (Staggered,
    /// shields down 3 s) and they fight on; the Beast Handler still frees his curs.
    ///
    /// The Centurion and the Praetorian are stand-ins made here with the numbers in ARENAS.md §5 (the real EN_ assets
    /// come with the arena's setup). The TestArena's obstacles are switched off and the formation is placed on open
    /// ground, so its pillars and walls don't bend the line: these tests check the formation, not the level.
    /// </summary>
    public class FormationTests
    {
        const string Scene = "Assets/Scenes/Dev/TestArena.unity";
        static T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);

        Player _player;
        PlayerHealth _health;
        PlayerMotor _motor;
        EnemyManager _em;
        EnemyDef _praetorian, _centurion;
        readonly int[] _line = new int[16];
        readonly List<float> _damage = new List<float>();

        void OnDamaged(in DamagedEvent e) => _damage.Add(e.Damage);

        IEnumerator Boot()
        {
            DevSettings.ResetAll();
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            _player = Player.Main;
            _health = _player.GetComponent<PlayerHealth>();
            _motor = _player.GetComponent<PlayerMotor>();
            _player.OverrideSource = new ScriptedInput();
            _player.GetComponent<WeaponSystem>().enabled = false;
            _em = EnemyManager.Instance;
            var spawner = Object.FindAnyObjectByType<DebugEnemySpawner>();
            if (spawner != null) spawner.enabled = false;
            if (Director.Instance != null) Director.Instance.enabled = false; // tests place their own enemies
            var lvl = Object.FindAnyObjectByType<LevelUpController>();
            if (lvl != null) lvl.enabled = false; // kills drop XP: a level-up would pause the clock and the waits never end
            _em.KillAll();
            DevSettings.GodMode = true; // HP never runs out; damage is still recorded
            _health.invulnerableTime = 0f; // a shield's contact hit must not swallow the push
            _em.Obstacles.ToArrays(out var circles, out var boxesA, out _);
            for (int k = 0; k < circles.Length; k++) _em.Obstacles.SetCircleActive(k, false);
            for (int k = 0; k < boxesA.Length; k++) _em.Obstacles.SetBoxActive(k, false);
            MakeDefs();
            _em.Prewarm(_praetorian, 12);
            _em.Prewarm(_centurion, 3);
            yield return new WaitForSeconds(0.3f);
            PickupSystem.Instance.ClearAll();
            _damage.Clear();
            GameEvents.OnDamaged.Subscribe(OnDamaged);
        }

        [TearDown]
        public void TearDown()
        {
            GameEvents.OnDamaged.Unsubscribe(OnDamaged);
            DevSettings.ResetAll();
        }

        /// <summary>The Praetorian and the Centurion with ARENAS.md §5's numbers (speed jitter off: the tests time the march).</summary>
        void MakeDefs()
        {
            var murmillo = Load<EnemyDef>("Assets/Data/Enemies/EN_Murmillo.asset");
            var convict = Load<EnemyDef>("Assets/Data/Enemies/EN_Convict.asset");

            var p = _praetorian = ScriptableObject.CreateInstance<EnemyDef>();
            p.id = "test_praetorian";
            p.displayName = "Praetorian (test)";
            p.model = Load<GameObject>("Assets/Art/Models/Enemies/EN_Praetorian.fbx");
            if (p.model == null) p.model = murmillo.model;
            p.anim = murmillo.anim;
            p.behaviour = EnemyBehaviour.ShieldBearer;
            p.maxHp = 70f; p.moveSpeed = 3f; p.speedJitter = 0f; p.radius = 0.5f; p.acceleration = 14f;
            p.contactDamage = 10f; p.blockArc = 120f; p.blockReduction = 0.75f; p.knockbackTaken = 0.25f; p.turnRate = 180f;
            p.xp = 4f; p.goldChance = 0f; p.prewarm = 0;

            var c = _centurion = ScriptableObject.CreateInstance<EnemyDef>();
            c.id = "test_centurion";
            c.displayName = "Centurion (test)";
            c.model = Load<GameObject>("Assets/Art/Models/Enemies/EN_Centurion.fbx");
            if (c.model == null) c.model = convict.model;
            c.anim = convict.anim;
            c.behaviour = EnemyBehaviour.Lunger;
            c.maxHp = 160f; c.moveSpeed = 2.6f; c.speedJitter = 0f; c.radius = 0.42f; c.acceleration = 10f;
            c.contactDamage = 10f; c.windup = 0.7f; c.attackRange = 4.2f; c.lungeSpeed = 8f; c.lungeTime = 0.25f;
            c.attackDamage = 16f; c.attackCooldown = 3.5f; c.recover = 0.7f; c.knockbackTaken = 0.3f; c.turnRate = 80f;
            c.xp = 12f; c.goldChance = 0f; c.prewarm = 0;
            c.escort = p;
            c.escortCount = 5;
            c.formationSlots = new[] { new Vector2(-2.6f, 1.8f), new Vector2(-1.3f, 1.8f), new Vector2(0f, 1.8f), new Vector2(1.3f, 1.8f), new Vector2(2.6f, 1.8f) };
            c.formationHold = 2.6f;
            c.pushPad = 0.8f; c.pushDepth = 2.6f; c.pushKnockback = 9f;
            c.freeEscortsOnDeath = false;
            c.breakStatus = Load<StatusDef>("Assets/Data/Status/SE_Stagger.asset");
            c.breakShieldsDown = 3f;
            Assert.IsNotNull(c.breakStatus, "SE_Stagger");
        }

        Vector3 P => _player.transform.position;
        Vector3 Ground(Vector3 p) { p.y = _em.Height.Sample(p.x, p.z); return p; }
        void PutPlayer(Vector3 p) { _em.ClampInside(ref p, 1.5f); _motor.Teleport(Ground(p) + Vector3.up * 0.1f); }
        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
        static Vector3 Fwd(float yaw) => new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad));
        static Vector3 Right(Vector3 f) => new Vector3(f.z, 0f, -f.x);
        int CountDamage(float amount) { int n = 0; foreach (var d in _damage) if (Mathf.Abs(d - amount) < 0.01f) n++; return n; }

        static IEnumerator Until(System.Func<bool> done, float seconds)
        {
            float end = Time.time + seconds;
            while (!done() && Time.time < end) yield return null;
        }

        /// <summary>
        /// The open spot nearest the arena's centre where a circle of `clear` m stays inside the arena and has no cliff
        /// (the platform's sides, the hill's steep face): the obstacles are off, so that's all a line can catch on.
        /// Returns it with the direction from it toward the centre (+Z if it is the centre).
        /// </summary>
        Vector3 OpenSpot(float clear, out Vector3 toCentre)
        {
            var h = _em.Height;
            Vector3 c0 = _em.arenaCenter, best = c0;
            float bestD = float.MaxValue;
            for (float x = -18f; x <= 18f; x += 1f)
                for (float z = -18f; z <= 18f; z += 1f)
                {
                    float d = x * x + z * z;
                    var c = c0 + new Vector3(x, 0f, z);
                    if (d >= bestD || _em.EdgeDistance01(c, clear + 0.5f) > 1f || !Smooth(h, c, clear)) continue;
                    best = c;
                    bestD = d;
                }
            if (bestD == float.MaxValue) Assert.Inconclusive("no open ground " + clear + " m round in this arena");
            toCentre = Flat(c0 - best);
            toCentre = toCentre.sqrMagnitude > 1f ? toCentre.normalized : Vector3.forward;
            return Ground(best);
        }

        static bool Smooth(HeightGrid h, Vector3 c, float r)
        {
            for (float x = -r; x <= r; x += 0.5f)
                for (float z = -r; z <= r; z += 0.5f)
                {
                    if (x * x + z * z > r * r) continue;
                    float a = h.Sample(c.x + x, c.z + z);
                    if (Mathf.Abs(h.Sample(c.x + x + 0.5f, c.z + z) - a) > 0.2f || Mathf.Abs(h.Sample(c.x + x, c.z + z + 0.5f) - a) > 0.2f)
                        return false;
                }
            return true;
        }

        float SlotError(int e) => Flat(_em.GetPosition(e) - _em.SlotPosition(e)).magnitude;

        float WorstSlotError(int n)
        {
            float w = 0f;
            for (int k = 0; k < n; k++) if (_em.IsAlive(_line[k])) w = Mathf.Max(w, SlotError(_line[k]));
            return w;
        }

        int InSlot(int n, int slot)
        {
            for (int k = 0; k < n; k++) if (_em.FormationSlotOf(_line[k]) == slot) return _line[k];
            Assert.Fail("nobody in slot " + slot);
            return -1;
        }

        static bool InLane(Vector3 p, Vector3 from, Vector3 dir, float width, float depth)
        {
            Vector3 d = Flat(p - from);
            float along = Vector3.Dot(d, dir), side = Vector3.Dot(d, Right(dir));
            return along >= -0.35f && along <= depth && Mathf.Abs(side) <= width * 0.5f;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator HeSpawnsWithFiveOnTheirSlots_AndTheyKeepThemAsHeMarchesAndWheels()
        {
            yield return Boot();
            _centurion.attackRange = 0f; // no push here: the march and the wheel only
            Vector3 c = OpenSpot(8f, out var f);
            PutPlayer(c + f * 10f);
            yield return null;
            int formations = _em.FormationsSpawned, alive = _em.AliveCount;
            int leader = _em.Spawn(_centurion, c - f * 3f);
            Assert.AreEqual(alive + 6, _em.AliveCount, "the Centurion and his five");
            Assert.AreEqual(formations + 1, _em.FormationsSpawned, "a formation (bot log)");
            Assert.AreEqual(5, _em.FormedCount(leader));
            int n = _em.FindAlive(_praetorian, _line);
            Assert.AreEqual(5, n);
            var seen = new bool[5];
            for (int k = 0; k < n; k++)
            {
                int e = _line[k];
                Assert.AreEqual(leader, _em.LeaderOf(e), "his");
                Assert.IsTrue(_em.InFormation(e));
                int s = _em.FormationSlotOf(e);
                Assert.IsFalse(seen[s], "one per slot");
                seen[s] = true;
                Assert.Less(SlotError(e), 0.05f, "spawned on its slot");
                Assert.AreEqual(0f, Mathf.DeltaAngle(_em.GetYaw(leader), _em.GetYaw(e)), 0.01f, "facing his front");
            }
            Vector3 mid = Flat(_em.GetPosition(InSlot(n, 2)) - _em.GetPosition(leader));
            Assert.AreEqual(1.8f, Vector3.Dot(mid, f), 0.05f, "the line is 1.8 m ahead of him, toward you");

            // the march
            Vector3 start = _em.GetPosition(leader);
            float worst = 0f, end = Time.time + 1.5f;
            while (Time.time < end) { yield return null; worst = Mathf.Max(worst, WorstSlotError(n)); }
            Debug.Log($"[FormationTests] march: worst slot error {worst:F3} m");
            Assert.LessOrEqual(worst, 0.5f, "the line keeps its slots on the march");
            Assert.Greater(Vector3.Dot(_em.GetPosition(leader) - start, f), 2.5f, "he marches at you");

            // the wheel: you're suddenly 8 m to his right
            float yaw0 = _em.GetYaw(leader);
            PutPlayer(_em.GetPosition(leader) + Right(Fwd(yaw0)) * 8f);
            worst = 0f;
            end = Time.time + 3f;
            while (Time.time < end && Mathf.Abs(Mathf.DeltaAngle(_em.GetYaw(leader), yaw0 + 90f)) > 3f)
            {
                yield return null;
                worst = Mathf.Max(worst, WorstSlotError(n));
            }
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(_em.GetYaw(leader), yaw0 + 90f)), 3f, "he wheels to face you");
            float wheel = worst;
            worst = 0f;
            end = Time.time + 0.5f;
            while (Time.time < end) { yield return null; worst = Mathf.Max(worst, WorstSlotError(n)); }
            Debug.Log($"[FormationTests] wheel: worst slot error {wheel:F3} m while turning, {worst:F3} m after");
            Assert.LessOrEqual(wheel, 0.5f, "the line keeps its slots through a 90° wheel on the march");
            Assert.LessOrEqual(worst, 0.5f);
            for (int k = 0; k < n; k++)
                Assert.Less(Mathf.Abs(Mathf.DeltaAngle(_em.GetYaw(_line[k]), _em.GetYaw(leader))), 5f, "they face his front");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheLine_BlocksFromItsFront_NotFromBehind_NorFromTheFlank()
        {
            yield return Boot();
            _centurion.attackRange = 0f;
            Vector3 c = OpenSpot(6f, out var f);
            PutPlayer(c + f * 9f);
            yield return null;
            int leader = _em.Spawn(_centurion, c - f * 2f);
            yield return null;
            yield return null;
            int n = _em.FindAlive(_praetorian, _line);
            Vector3 front = Fwd(_em.GetYaw(leader));
            for (int k = 0; k < n; k++)
            {
                Assert.IsTrue(_em.IsBlocking(_line[k], -front), "a hit from in front of the line is blocked");
                Assert.IsFalse(_em.IsBlocking(_line[k], front), "one from behind isn't");
            }
            int mid = InSlot(n, 2);
            float hp = _em.GetHp(mid);
            _em.ApplyDamage(mid, new DamageInfo { Amount = 20f, Direction = -front });
            Assert.AreEqual(hp - 20f * (1f - _praetorian.blockReduction), _em.GetHp(mid), 1e-3f, "75 % blocked");
            hp = _em.GetHp(mid);
            _em.ApplyDamage(mid, new DamageInfo { Amount = 20f, Direction = front });
            Assert.AreEqual(hp - 20f, _em.GetHp(mid), 1e-3f, "in full from behind");

            // the flank: they face his front, not you, and he wheels at 80°/s
            PutPlayer(_em.GetPosition(leader) + Right(front) * 7f);
            yield return new WaitForSeconds(0.25f);
            Vector3 fromYou = Flat(_em.GetPosition(mid) - P).normalized; // the way your hit travels
            Assert.IsFalse(_em.IsBlocking(mid, fromYou), "from the flank: past the shield");
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(_em.GetYaw(mid), _em.GetYaw(leader))), 10f, "still facing the line's front");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ThePush_TelegraphsItsLane_HitsYouInItOnce_AndThrowsYouBack()
        {
            yield return Boot();
            Vector3 c = OpenSpot(7f, out var f);
            PutPlayer(c + f * 3f);
            yield return null;
            int leader = _em.Spawn(_centurion, c - f * 3f); // 6 m from you, facing you
            yield return Until(() => _em.GetAiState(leader) == 1, 8f);
            Assert.AreEqual(1, _em.GetAiState(leader), "the line braces once you're within 4.2 m of him");
            Assert.IsTrue(_em.PushLane(leader, out var from, out var dir, out float width, out float depth), "the push's lane");
            Assert.AreEqual(5.2f + 2f * _praetorian.radius + _centurion.pushPad, width, 0.01f, "the line's width (7 m)");
            Assert.AreEqual(_centurion.pushDepth, depth, 0.01f);
            Assert.Greater(Vector3.Dot(dir, Fwd(_em.GetYaw(leader))), 0.999f, "along his facing");
            Assert.IsTrue(InLane(P, from, dir, width, depth), "you stand in it");
            Assert.Less(Vector3.Distance(Flat(from), Flat(_em.GetPosition(leader) + dir * (1.8f + _praetorian.radius))), 0.05f,
                "it starts at the shields' front");
            yield return null;
            Assert.Greater(_em.ThreatCount, 0, "a wind-up threat (edge arrow, bot)");
            Assert.AreEqual(0, CountDamage(_centurion.attackDamage), "nothing lands during the brace");

            Vector3 before = P;
            yield return Until(() => _em.GetAiState(leader) == 3, 2f);
            Assert.AreEqual(3, _em.GetAiState(leader), "pushed, recovering");
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(1, CountDamage(_centurion.attackDamage), "one hit per push, however many shields touch you");
            Assert.Greater(Vector3.Dot(P - before, dir), 0.5f, "thrown back along the push");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ThePush_MissesYouFourMetresToTheSide_ItsFacingLockedAtTheBrace()
        {
            yield return Boot();
            Vector3 c = OpenSpot(8f, out var f);
            PutPlayer(c + f * 3f);
            yield return null;
            int leader = _em.Spawn(_centurion, c - f * 3f);
            yield return Until(() => _em.GetAiState(leader) == 1, 8f);
            Assert.AreEqual(1, _em.GetAiState(leader));
            Assert.IsTrue(_em.PushLane(leader, out var from, out var dir, out _, out float depth));
            float yaw = _em.GetYaw(leader), turned = 0f;
            PutPlayer(from + dir * (depth * 0.5f) + Right(dir) * 4f); // a sidestep: 4 m off the lane's centre line
            float end = Time.time + 2f;
            while (Time.time < end && _em.GetAiState(leader) != 3) // the brace and the shove
            {
                turned = Mathf.Max(turned, Mathf.Abs(Mathf.DeltaAngle(yaw, _em.GetYaw(leader))));
                yield return null;
            }
            Assert.AreEqual(3, _em.GetAiState(leader), "the push went off");
            Assert.Less(turned, 0.5f, "braced: he didn't turn after you");
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(0, CountDamage(_centurion.attackDamage), "the push missed you");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator KillingHim_BreaksTheLine_StaggeredWithShieldsDown_ThenTheyFightOn()
        {
            yield return Boot();
            _centurion.attackRange = 0f;
            Vector3 c = OpenSpot(6f, out var f);
            PutPlayer(c + f * 9f);
            yield return null;
            int leader = _em.Spawn(_centurion, c - f * 2f);
            yield return new WaitForSeconds(0.2f);
            int n = _em.FindAlive(_praetorian, _line);
            Assert.AreEqual(5, n);
            int freed = _em.EscortsFreed, broken = _em.LinesBroken;
            _em.ApplyDamage(leader, new DamageInfo { Amount = 1e6f, Flags = DamageFlags.Melee, Point = _em.GetPosition(leader) });
            Assert.IsFalse(_em.IsAlive(leader));
            Assert.AreEqual(5, _em.AliveOf(_praetorian), "not freed: they fight on");
            Assert.AreEqual(freed, _em.EscortsFreed, "EscortsFreed counts freed escorts only");
            Assert.AreEqual(broken + 1, _em.LinesBroken, "a broken line (bot log)");
            for (int k = 0; k < n; k++)
            {
                int e = _line[k];
                Assert.AreEqual(-1, _em.LeaderOf(e));
                Assert.IsFalse(_em.InFormation(e));
                Assert.IsTrue(_em.HasStatus(e, StatusKind.Root), "Staggered");
                Assert.IsTrue(_em.ShieldsDown(e));
                Assert.IsFalse(_em.IsBlocking(e, -Fwd(_em.GetYaw(e))), "shield down: a frontal hit isn't blocked");
            }
            int one = _line[0];
            float hp = _em.GetHp(one);
            _em.ApplyDamage(one, new DamageInfo { Amount = 10f, Direction = -Fwd(_em.GetYaw(one)) });
            Assert.AreEqual(hp - 10f, _em.GetHp(one), 1e-3f, "it lands in full");
            yield return null;
            yield return null;
            for (int k = 0; k < n; k++)
                Assert.Less(Flat(_em.GetVelocity(_line[k])).magnitude, 0.01f, "staggered: they stand");

            yield return new WaitForSeconds(_centurion.breakShieldsDown + 0.2f);
            for (int k = 0; k < n; k++)
            {
                int e = _line[k];
                Assert.IsTrue(_em.IsAlive(e), "still alive");
                Assert.IsFalse(_em.ShieldsDown(e));
                Assert.IsFalse(_em.HasStatus(e, StatusKind.Root));
                Assert.IsTrue(_em.IsBlocking(e, -Fwd(_em.GetYaw(e))), "shields up again");
                Vector3 toYou = Flat(P - _em.GetPosition(e)).normalized;
                Assert.Greater(Vector3.Dot(Fwd(_em.GetYaw(e)), toYou), 0.9f, "an ordinary shield bearer: it faces you");
                Assert.Greater(Vector3.Dot(Flat(_em.GetVelocity(e)), toYou), 1f, "and comes for you");
            }
            Assert.AreEqual(broken + 1, _em.LinesBroken, "broken once");
            Assert.AreEqual(freed, _em.EscortsFreed);

            int formations = _em.FormationsSpawned;
            _em.Spawn(_centurion, c - f * 2f);
            Assert.AreEqual(formations + 1, _em.FormationsSpawned);
            _em.KillAll();
            Assert.AreEqual(broken + 1, _em.LinesBroken, "the run's end (KillAll) breaks no line in the log");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator HeHoldsShortOfYou_ADeadShieldLeavesAGap_AndWithNoLineHeWalksOn()
        {
            yield return Boot();
            _centurion.attackRange = 0f; // no push yet: the hold
            Vector3 c = OpenSpot(6f, out var f);
            PutPlayer(c + f * 5f);
            yield return null;
            int leader = _em.Spawn(_centurion, c - f * 4f);
            yield return new WaitForSeconds(4.5f);
            float d = Flat(_em.GetPosition(leader) - P).magnitude;
            Assert.That(d, Is.InRange(2.3f, 3.0f), "his line stands: he stops 2.6 m short of you");
            Assert.Less(Flat(_em.GetVelocity(leader)).magnitude, 0.2f, "and holds there");

            // a gap: the left end dies; nobody closes ranks
            int n = _em.FindAlive(_praetorian, _line);
            int left = InSlot(n, 0);
            int broken = _em.LinesBroken;
            _em.ApplyDamage(left, new DamageInfo { Amount = 1e6f, Flags = DamageFlags.Melee, Point = _em.GetPosition(left) });
            Assert.AreEqual(4, _em.FormedCount(leader));
            for (int s = 1; s < 5; s++) Assert.IsTrue(_em.InFormation(InSlot(n, s)), "slot " + s + " holds");
            Assert.AreEqual(broken, _em.LinesBroken, "one man down isn't a broken line");

            // the push lane shrinks to the shields left
            _centurion.attackRange = 4.2f;
            yield return Until(() => _em.GetAiState(leader) == 1, 6f);
            Assert.IsTrue(_em.PushLane(leader, out var from, out var dir, out float width, out _));
            Assert.AreEqual(3.9f + 2f * _praetorian.radius + _centurion.pushPad, width, 0.01f, "from slot 1 to slot 4");
            Vector3 centre = _em.GetPosition(leader) + Right(dir) * 0.65f + dir * (1.8f + _praetorian.radius);
            Assert.Less(Vector3.Distance(Flat(from), Flat(centre)), 0.05f, "centred on them");
            yield return Until(() => _em.GetAiState(leader) == 3, 2f);

            // the whole line dead: a plain Lunger, no hold
            _centurion.attackRange = 0f;
            for (int k = 0; k < n; k++) if (_em.IsAlive(_line[k])) _em.ApplyDamage(_line[k], new DamageInfo { Amount = 1e6f, Flags = DamageFlags.Melee, Point = _em.GetPosition(_line[k]) });
            Assert.AreEqual(0, _em.FormedCount(leader));
            yield return new WaitForSeconds(2.5f);
            Assert.Less(Flat(_em.GetPosition(leader) - P).magnitude, 1.5f, "no line: he walks right up to you");
            _em.ApplyDamage(leader, new DamageInfo { Amount = 1e6f, Flags = DamageFlags.Melee, Point = _em.GetPosition(leader) });
            Assert.AreEqual(broken, _em.LinesBroken, "no line left to break");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheBeastHandler_StillFreesHisCurs_AndIsNoFormation()
        {
            yield return Boot();
            var handler = Load<EnemyDef>("Assets/Data/Enemies/EN_BeastHandler.asset");
            var cur = Load<EnemyDef>("Assets/Data/Enemies/EN_PitCur.asset");
            int formations = _em.FormationsSpawned, broken = _em.LinesBroken, freed = _em.EscortsFreed;
            int h = _em.Spawn(handler, P + new Vector3(0f, 0f, 9f));
            Assert.AreEqual(formations, _em.FormationsSpawned, "leashes, not a line");
            int n = _em.FindAlive(cur, _line);
            Assert.AreEqual(2, n);
            for (int k = 0; k < n; k++)
            {
                Assert.AreEqual(h, _em.LeaderOf(_line[k]), "on his leash");
                Assert.IsFalse(_em.InFormation(_line[k]));
            }
            yield return new WaitForSeconds(0.5f);
            _em.ApplyDamage(h, new DamageInfo { Amount = 1e6f, Flags = DamageFlags.Melee, Point = _em.GetPosition(h) });
            Assert.AreEqual(0, _em.AliveOf(cur), "freed: they vanish");
            Assert.AreEqual(freed + 2, _em.EscortsFreed);
            Assert.AreEqual(broken, _em.LinesBroken);
        }
    }
}
