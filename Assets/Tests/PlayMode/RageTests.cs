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
    /// R3 (docs/GLADIATORS.md "Asterion, the Chained", docs/tracks/R3-design.md "Rage"): his Rage and Gore. The press
    /// (enemies within 3.5 m) fills the meter, capped at 5/s, an elite or a boss counting 4; hits add 3, status ticks
    /// nothing; it cools after 2.5 s of quiet. Full, he's Unchained at once (or, with triggerOnSlide, at the next slide):
    /// the bellow, ×1.3 attack speed and ×1.2 move speed, no knockback, the chain's whirl, the poses, "UNCHAINED!"; 8 s
    /// later Spent (×0.75 move speed, no fill, head bowed), 2 s after that calm with everything back; death or the run's
    /// end undoes it all. Gore: running into an enemy ahead gores it and tosses it aside; not one beside him, not at a
    /// walk, not twice within its interval; Unchained, from every side at a jog; a boss is hurt but not moved.
    ///
    /// Asterion is a stand-in made here with the design's numbers (GL_Asterion comes with the setup part), on the
    /// Nobody's model, in the test arena with no Director, no level-ups and no obstacles. The enemies are copies of the
    /// Convict held in place (move speed 0, no contact damage, plenty of HP).
    /// </summary>
    public class RageTests
    {
        const string TestArena = "Assets/Scenes/Dev/TestArena.unity";
        /// <summary>The test arena's open runway: the spawn, and 10 m clear toward +Z.</summary>
        static readonly Vector3 Open = new Vector3(0f, 0f, -8f);
        static T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);

        readonly List<Object> _made = new List<Object>();
        readonly List<string> _announced = new List<string>();
        GladiatorDef _def;
        WeaponDef _chain;
        RageTrait _rageTrait;
        GoreTrait _goreTrait;

        EnemyManager _em;
        WeaponSystem _weapons;
        PlayerStats _stats;
        PlayerHealth _health;
        PlayerMotor _motor;
        PlayerPartAnimator _animator;
        ScriptedInput _input;
        Rage _rage;
        Gore _gore;
        float _baseAttack, _baseMove;

        void OnAnnounce(in AnnounceEvent e) => _announced.Add(e.Text);

        T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            _made.Add(o);
            return o;
        }

        void MakeAsterion()
        {
            _goreTrait = Make<GoreTrait>();
            _goreTrait.id = "gore";
            _goreTrait.displayName = "Gore";
            _rageTrait = Make<RageTrait>();
            _rageTrait.id = "rage";
            _rageTrait.displayName = "Rage";

            // the Labyrinth Chain's numbers (R3-design "Setup"); the Sweep's own fields keep their defaults, the chain's
            _chain = Make<WeaponDef>();
            _chain.id = "test_labyrinth_chain";
            _chain.displayName = "Labyrinth Chain (test)";
            _chain.pattern = WeaponPattern.Sweep; // R3-STUB(chain)
            _chain.damage = 15f; _chain.critChance = 0.05f; _chain.critMultiplier = 2f; _chain.knockback = 7f;
            _chain.cooldown = 1.6f; _chain.swingDuration = 0.8f; _chain.windup = 0.32f;
            _chain.range = 5.2f; _chain.arcDegrees = 240f; _chain.width = 0.5f; _chain.acquireRange = 5.6f;
            _chain.amount = 1; _chain.tipLength = 1.4f;

            _def = Make<GladiatorDef>();
            _def.id = "test_asterion";
            _def.displayName = "Asterion (test)";
            _def.baseStats = new[]
            {
                new GladiatorDef.BaseStat { stat = StatId.MaxHp, value = 130f },
                new GladiatorDef.BaseStat { stat = StatId.MoveSpeed, value = 0.95f },
            };
            _def.startingWeapon = _chain;
            _def.traits = new GladiatorTrait[] { _goreTrait, _rageTrait };
        }

        /// <summary>A Convict that stays where it's put: no move speed, no contact damage, 1000 HP.</summary>
        EnemyDef Held(string id, float knockbackTaken = 1f, EnemyBehaviour behaviour = EnemyBehaviour.Chaser)
        {
            var d = Object.Instantiate(Load<EnemyDef>("Assets/Data/Enemies/EN_Convict.asset"));
            _made.Add(d);
            d.id = id;
            d.behaviour = behaviour;
            d.maxHp = 1000f; d.moveSpeed = 0f; d.speedJitter = 0f; d.contactDamage = 0f; d.knockbackTaken = knockbackTaken;
            d.prewarm = 0; d.goldChance = 0f; d.xp = 0f;
            return d;
        }

        IEnumerator Boot(bool keepChain = false)
        {
            DevSettings.ResetAll();
            DevSettings.GodMode = true;
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            MakeAsterion();
            GladiatorLoadout.NextOverride = _def;
            var op = SceneManager.LoadSceneAsync(TestArena, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            while (RunIntro.Instance != null && !RunIntro.Instance.Done) yield return null;
            var spawner = Object.FindAnyObjectByType<DebugEnemySpawner>();
            if (spawner != null) spawner.enabled = false;
            if (Director.Instance != null) Director.Instance.enabled = false; // the tests place their own enemies
            var lvl = Object.FindAnyObjectByType<LevelUpController>();
            if (lvl != null) lvl.enabled = false; // a level-up would pause the clock and the waits never end
            _input = new ScriptedInput();
            var p = Player.Main;
            p.OverrideSource = _input;
            _em = EnemyManager.Instance;
            _weapons = p.GetComponent<WeaponSystem>();
            _stats = p.GetComponent<PlayerStats>();
            _health = p.GetComponent<PlayerHealth>();
            _motor = p.GetComponent<PlayerMotor>();
            _animator = p.GetComponent<PlayerPartAnimator>();
            _rage = p.GetComponent<Rage>();
            _gore = p.GetComponent<Gore>();
            Assert.IsNotNull(_rage, "the Rage trait installed");
            Assert.IsNotNull(_gore, "the Gore trait installed");
            _health.invulnerableTime = 0f; // every hit counts: the meter's rule, not i-frames
            if (!keepChain) _weapons.RemoveAll(); // nothing but the traits touches the enemies
            _em.DespawnAll();
            _em.Obstacles.ToArrays(out var circles, out var boxes, out _);
            for (int k = 0; k < circles.Length; k++) _em.Obstacles.SetCircleActive(k, false);
            for (int k = 0; k < boxes.Length; k++) _em.Obstacles.SetBoxActive(k, false);
            PutPlayer(Spot(Open));
            _announced.Clear();
            GameEvents.OnAnnounce.Subscribe(OnAnnounce);
            yield return new WaitForSeconds(0.3f);
            _rage.Halt(); // nothing from the intro
            _baseAttack = _stats.AttackSpeed;
            _baseMove = _stats.MoveSpeed;
        }

        [TearDown]
        public void TearDown()
        {
            GameEvents.OnAnnounce.Unsubscribe(OnAnnounce);
            // nothing in the scene may hold the test's assets once they're destroyed
            if (Player.Main != null)
            {
                var ws = Player.Main.GetComponent<WeaponSystem>();
                if (ws != null) ws.RemoveAll();
            }
            if (EnemyManager.Instance != null) EnemyManager.Instance.DespawnAll();
            DevSettings.ResetAll();
            TimeController.ResetForNewRun();
            GladiatorLoadout.NextOverride = null;
            GameSession.Clear();
            foreach (var o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
        }

        // ------------------------------------------------------------------ helpers

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        static Vector3 Ground(Vector3 p)
        {
            var h = EnemyManager.Instance.Height;
            p.y = h != null ? h.Sample(p.x, p.z) : 0f;
            return p;
        }

        /// <summary>
        /// `want` on the ground, if the ground the tests use round it (3 m either side, 5 m behind, 7 m ahead along +Z)
        /// is smooth; else the test can't run here.
        /// </summary>
        static Vector3 Spot(Vector3 want)
        {
            var h = EnemyManager.Instance.Height;
            if (h != null)
            {
                float h0 = h.Sample(want.x, want.z);
                for (float x = -3f; x <= 3f; x += 0.5f)
                    for (float z = -5f; z <= 7f; z += 0.5f)
                        if (Mathf.Abs(h.Sample(want.x + x, want.z + z) - h0) > 0.15f)
                            Assert.Inconclusive("the test arena changed: no open ground round " + want);
            }
            return Ground(want);
        }

        void PutPlayer(Vector3 p) => _motor.Teleport(Ground(p) + Vector3.up * 0.05f);

        Vector3 PlayerPos => Player.Main.transform.position;

        int SpawnAt(EnemyDef def, Vector3 offset, AffixDef affix = null)
        {
            int i = _em.Spawn(def, Ground(PlayerPos + offset), affix);
            Assert.GreaterOrEqual(i, 0, "spawned");
            return i;
        }

        /// <summary>The stick (camera-relative) that moves the player along world `dir` at `speed` m/s.</summary>
        Vector2 Stick(Vector3 dir, float speed)
        {
            dir = Flat(dir);
            if (dir.sqrMagnitude < 1e-6f) return Vector2.zero;
            dir.Normalize();
            float yaw = Player.Main.ReferenceYaw * Mathf.Deg2Rad, c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
            float top = _motor.config.maxSpeed * _stats.MoveSpeed;
            float mag = Mathf.Clamp01(speed / Mathf.Max(0.01f, top));
            return new Vector2(dir.x * c - dir.z * s, dir.x * s + dir.z * c) * mag; // world = x·(cos, −sin) + y·(sin, cos)
        }

        void Steer(Vector3 dir, float speed) => _input.Next = new PlayerInputFrame { Move = Stick(dir, speed) };
        void Stop() => _input.Next = new PlayerInputFrame();

        static IEnumerator Until(System.Func<bool> done, float seconds)
        {
            float end = Time.time + seconds;
            while (!done() && Time.time < end) yield return null;
        }

        Gladiator.UI.Hud Hud => Object.FindAnyObjectByType<Gladiator.UI.Hud>();

        /// <summary>Waits for the meter to have risen for `seconds` and returns how much it rose.</summary>
        IEnumerator MeasureRise(float seconds, float[] into)
        {
            yield return null; // the press is counted from the next tick
            float v0 = _rage.Value, t0 = Time.time;
            while (Time.time - t0 < seconds) yield return null;
            into[0] = (_rage.Value - v0) * seconds / Mathf.Max(0.001f, Time.time - t0);
        }

        // ------------------------------------------------------------------ the meter

        [UnityTest, Timeout(60000)]
        public IEnumerator ThePress_FillsTheMeter_OneASecondForEachEnemyWithin3_5m()
        {
            yield return Boot();
            var held = Held("test_rage_held");
            Assert.AreEqual(0f, _rage.Value, "calm and empty");
            Assert.AreEqual(Rage.Mode.Calm, _rage.Current);
            for (int k = 0; k < 4; k++)
            {
                float a = k * 90f * Mathf.Deg2Rad;
                SpawnAt(held, new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 2.6f);
            }
            SpawnAt(held, new Vector3(0f, 0f, 6.5f)); // outside the press
            var rise = new float[1];
            yield return MeasureRise(2f, rise);
            Assert.AreEqual(4, _rage.Pressing, "four in the press, not the one 6.5 m away");
            Assert.AreEqual(4f, _rage.PressRate, 1e-3f);
            Assert.IsFalse(_rage.Cornered, "four is under the cap");
            Assert.AreEqual(8f, rise[0], 0.6f, "≈ 8 Rage in 2 s");
            Assert.GreaterOrEqual(_rage.PeakPress, 4);
            var hud = Hud;
            Assert.IsNotNull(hud);
            Assert.AreEqual("", hud.BloodLabel, "no label under the cap");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ThePress_IsCappedAt5ASecond_Cornered()
        {
            yield return Boot();
            var held = Held("test_rage_held");
            for (int k = 0; k < 8; k++)
            {
                float a = k * 45f * Mathf.Deg2Rad;
                SpawnAt(held, new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 2.4f);
            }
            var rise = new float[1];
            yield return MeasureRise(2f, rise);
            Assert.AreEqual(8, _rage.Pressing);
            Assert.AreEqual(5f, _rage.PressRate, 1e-3f, "capped");
            Assert.IsTrue(_rage.Cornered, "CORNERED");
            Assert.AreEqual(10f, rise[0], 0.7f, "5 a second, not 8");
            yield return null;
            Assert.AreEqual("CORNERED", Hud.BloodLabel, "by the bar");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AnElite_OrABoss_CountsFour()
        {
            yield return Boot();
            var held = Held("test_rage_held");
            var armored = Load<AffixDef>("Assets/Data/Enemies/Affixes/AF_Armored.asset");
            Assert.IsNotNull(armored, "AF_Armored");
            int elite = SpawnAt(held, new Vector3(2.5f, 0f, 0f), armored);
            Assert.IsTrue(_em.IsElite(elite));
            yield return null;
            yield return null;
            Assert.AreEqual(4, _rage.Pressing, "an elite counts 4");
            Assert.AreEqual(4f, _rage.PressRate, 1e-3f);

            _em.DespawnAll();
            var boss = Held("test_rage_boss", 0f, EnemyBehaviour.Scripted);
            SpawnAt(boss, new Vector3(-2.5f, 0f, 0f));
            yield return null;
            yield return null;
            Assert.AreEqual(4, _rage.Pressing, "so does a Scripted enemy (a boss)");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AHitTaken_Adds3_AStatusTick_Nothing()
        {
            yield return Boot();
            _health.TakeDamage(new DamageInfo { Amount = 5f, Flags = DamageFlags.Contact, Point = PlayerPos, Direction = Vector3.forward });
            Assert.AreEqual(3f, _rage.Value, 1e-3f, "a hit adds 3");
            _health.TakeDamage(new DamageInfo { Amount = 5f, Flags = DamageFlags.Melee, Point = PlayerPos, Direction = Vector3.forward });
            Assert.AreEqual(6f, _rage.Value, 1e-3f, "and another 3");
            _health.TakeDamage(new DamageInfo { Amount = 4f, Flags = DamageFlags.Status, Status = StatusKind.Burn });
            Assert.AreEqual(6f, _rage.Value, 1e-3f, "a Burn tick adds nothing");
            yield return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator WithNobodyNearAndNoHit_ItCools_After2_5s_At8ASecond()
        {
            yield return Boot();
            _rage.SetRage(50f);
            float t0 = Time.time;
            yield return new WaitForSeconds(2.2f);
            Assert.AreEqual(50f, _rage.Value, 1e-3f, "still for 2.5 s");
            yield return Until(() => _rage.Value < 50f, 1f);
            Assert.Less(_rage.Value, 50f, "then it cools");
            Assert.AreEqual(2.5f, Time.time - t0, 0.2f, "from 2.5 s on");
            float v1 = _rage.Value;
            float t1 = Time.time;
            yield return new WaitForSeconds(1f);
            float rate = (v1 - _rage.Value) / (Time.time - t1);
            Assert.AreEqual(8f, rate, 0.6f, "at 8 a second");
        }

        // ------------------------------------------------------------------ Unchained and Spent

        [UnityTest, Timeout(90000)]
        public IEnumerator AFullMeter_UnchainsHimAtOnce_ThenSpent_ThenCalmWithEverythingBack()
        {
            yield return Boot();
            var held = Held("test_rage_held");
            int near = SpawnAt(held, new Vector3(3f, 0f, 0f));
            int far = SpawnAt(held, new Vector3(-6f, 0f, 0f));
            Vector3 nearAt = _em.GetPosition(near);
            Assert.AreEqual(1f, _motor.KnockbackTakenMul, 1e-3f, "calm: knockback lands");
            Assert.IsFalse(_weapons.WhirlSweeps); // R3-STUB(chain)
            int waves = PlayerProjectiles.Instance.Shockwaves;

            _rage.Fill();
            yield return null;
            Assert.IsTrue(_rage.IsUnchained, "full: Unchained at once, no button");
            float t0 = Time.time;
            Assert.AreEqual(1, _rage.Unchains);
            Assert.Contains("UNCHAINED!", _announced);
            Assert.GreaterOrEqual(_rage.BellowHits, 1);
            float bellow = _rageTrait.bellowDamage * _stats.Damage;
            Assert.AreEqual(_em.GetMaxHp(near) - bellow, _em.GetHp(near), 0.01f, "the bellow hits the one 3 m away");
            Assert.AreEqual(_em.GetMaxHp(far), _em.GetHp(far), 0.01f, "not the one 6 m away");
            Assert.GreaterOrEqual(PlayerProjectiles.Instance.Shockwaves, waves + 2, "two rings");
            Assert.AreEqual(_baseAttack * 1.3f, _stats.AttackSpeed, 1e-3f, "×1.3 attack speed");
            Assert.AreEqual(_baseMove * 1.2f, _stats.MoveSpeed, 1e-3f, "×1.2 move speed");
            Assert.AreEqual(0f, _motor.KnockbackTakenMul, 1e-3f, "nothing knocks him back");
            Assert.IsTrue(_weapons.WhirlSweeps, "the chain whirls"); // R3-STUB(chain)
            Assert.AreEqual(1.1f, _animator.ScaleBoost, 1e-3f, "he swells"); // R3-STUB(chain)
            Assert.AreEqual(14f, _animator.ExtraLean, 1e-3f, "horns down"); // R3-STUB(chain)
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(Flat(_em.GetPosition(near) - PlayerPos).magnitude - Flat(nearAt - PlayerPos).magnitude, 0.5f, "thrown back");
            Assert.AreEqual("UNCHAINED", Hud.BloodLabel);
            Assert.Less(_rage.Value, _rageTrait.meterMax, "the bar drains");

            // the press round him while he's Unchained changes nothing
            for (int k = 0; k < 6; k++)
            {
                float a = (k * 60f + 30f) * Mathf.Deg2Rad;
                SpawnAt(held, new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 2f);
            }
            yield return Until(() => !_rage.IsUnchained, 9f);
            Assert.IsTrue(_rage.IsSpent, "then Spent");
            Assert.AreEqual(8f, Time.time - t0, 0.25f, "8 s later");
            Assert.AreEqual(_baseMove * 0.75f, _stats.MoveSpeed, 1e-3f, "×0.75 move speed");
            Assert.AreEqual(_baseAttack, _stats.AttackSpeed, 1e-3f, "attack speed back");
            Assert.AreEqual(1f, _motor.KnockbackTakenMul, 1e-3f, "knockback lands again");
            Assert.IsFalse(_weapons.WhirlSweeps, "no more whirl"); // R3-STUB(chain)
            Assert.AreEqual(1f, _animator.ScaleBoost, 1e-3f); // R3-STUB(chain)
            Assert.AreEqual(0f, _animator.ExtraLean, 1e-3f); // R3-STUB(chain)
            Assert.AreEqual(30f, _animator.HeadDrop, 1e-3f, "head bowed"); // R3-STUB(chain)
            Assert.AreEqual(0f, _rage.Value, 1e-3f, "empty");
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(0f, _rage.Value, 1e-3f, "Spent: the press fills nothing");
            Assert.AreEqual("SPENT", Hud.BloodLabel);

            float t1 = Time.time;
            yield return Until(() => !_rage.IsSpent, 3f);
            Assert.AreEqual(Rage.Mode.Calm, _rage.Current, "then calm");
            Assert.AreEqual(1f, Time.time - t1, 0.25f, "2 s after Spent began");
            Assert.AreEqual(_baseMove, _stats.MoveSpeed, 1e-3f, "every stat back to its base");
            Assert.AreEqual(_baseAttack, _stats.AttackSpeed, 1e-3f);
            Assert.AreEqual(0f, _animator.HeadDrop, 1e-3f); // R3-STUB(chain)
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(_rage.Value, 0f, "calm, the press fills it again");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator WithTriggerOnSlide_AFullMeterWaitsForASlide()
        {
            yield return Boot();
            _rageTrait.triggerOnSlide = true;
            _rage.Fill();
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(Rage.Mode.Calm, _rage.Current, "a full meter waits");
            Assert.IsTrue(_rage.Full && _rage.WaitingForSlide);
            Assert.AreEqual("SLIDE!", Hud.BloodLabel);
            GameEvents.OnSlide.Raise(new SlideEvent { Position = PlayerPos, Velocity = Vector3.forward * 8f, Boost = 2f });
            Assert.IsTrue(_rage.IsUnchained, "the slide Unchains him");
            yield return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator DyingWhileUnchained_PutsEveryStatBack()
        {
            yield return Boot();
            DevSettings.GodMode = false;
            Assert.IsTrue(_rage.Unchain());
            yield return null;
            Assert.AreEqual(_baseMove * 1.2f, _stats.MoveSpeed, 1e-3f);
            _health.Kill();
            yield return null;
            yield return null;
            Assert.AreEqual(Rage.Mode.Calm, _rage.Current, "halted");
            Assert.AreEqual(0f, _rage.Value, 1e-3f);
            Assert.AreEqual(_baseMove, _stats.MoveSpeed, 1e-3f, "move speed back");
            Assert.AreEqual(_baseAttack, _stats.AttackSpeed, 1e-3f, "attack speed back");
            Assert.AreEqual(1f, _motor.KnockbackTakenMul, 1e-3f);
            Assert.IsFalse(_weapons.WhirlSweeps); // R3-STUB(chain)
            Assert.AreEqual(1f, _animator.ScaleBoost, 1e-3f); // R3-STUB(chain)
            Assert.AreEqual(0f, _animator.ExtraLean, 1e-3f); // R3-STUB(chain)
            Assert.IsFalse(_rage.Unchain(), "no rampage once he's down");
            _rage.Fill();
            yield return null;
            Assert.AreEqual(Rage.Mode.Calm, _rage.Current);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheRunEndingWhileSpent_PutsEveryStatBack()
        {
            yield return Boot();
            _rageTrait.unchainedSeconds = 0.3f;
            Assert.IsTrue(_rage.Unchain());
            yield return Until(() => _rage.IsSpent, 2f);
            Assert.IsTrue(_rage.IsSpent);
            Assert.AreEqual(_baseMove * 0.75f, _stats.MoveSpeed, 1e-3f);
            RunState.Current.End(RunOutcome.Survived);
            yield return null;
            yield return null;
            Assert.AreEqual(Rage.Mode.Calm, _rage.Current, "halted with the run");
            Assert.AreEqual(_baseMove, _stats.MoveSpeed, 1e-3f);
            Assert.AreEqual(0f, _animator.HeadDrop, 1e-3f); // R3-STUB(chain)
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Unchained_TheChainWhirlsAllRound_HittingWhatsBehindHim()
        {
            yield return Boot(keepChain: true);
            Assert.AreEqual(1, _weapons.Weapons.Count);
            Assert.AreEqual(WeaponPattern.Sweep, _weapons.Weapons[0].Def.pattern); // R3-STUB(chain)
            var still = Held("test_rage_still", 0f);
            int front = SpawnAt(still, new Vector3(0f, 0f, 2.5f));  // the nearest: the chain aims at it
            int behind = SpawnAt(still, new Vector3(0f, 0f, -4.6f)); // straight behind, past the bellow (4 m)
            int sweeps = _weapons.Sweeps; // R3-STUB(chain)
            yield return Until(() => _weapons.Sweeps > sweeps, 4f); // R3-STUB(chain)
            Assert.Greater(_weapons.Sweeps, sweeps, "calm, the chain sweeps"); // R3-STUB(chain)
            yield return new WaitForSeconds(0.6f);
            Assert.Less(_em.GetHp(front), _em.GetMaxHp(front), "the one it aims at is hit");
            Assert.AreEqual(_em.GetMaxHp(behind), _em.GetHp(behind), 0.01f, "240°: the one behind him isn't");

            int whirls = _weapons.Whirls; // R3-STUB(chain)
            Assert.IsTrue(_rage.Unchain());
            Assert.AreEqual(_em.GetMaxHp(behind), _em.GetHp(behind), 0.01f, "the bellow doesn't reach it");
            yield return Until(() => _weapons.Whirls > whirls, 4f); // R3-STUB(chain)
            Assert.Greater(_weapons.Whirls, whirls, "Unchained, every sweep is a whirl"); // R3-STUB(chain)
            yield return new WaitForSeconds(0.8f);
            Assert.Less(_em.GetHp(behind), _em.GetMaxHp(behind), "360°: now the one behind him is hit too");
        }

        // ------------------------------------------------------------------ Gore

        /// <summary>Runs the player along +Z at `speed` until `done` or `seconds`.</summary>
        IEnumerator Run(float speed, System.Func<bool> done, float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end && !done())
            {
                Steer(Vector3.forward, speed);
                yield return null;
            }
            Stop();
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Running_IntoAnEnemyAhead_GoresIt_AndTossesItAside()
        {
            yield return Boot();
            PutPlayer(Spot(Open) + new Vector3(0f, 0f, -3f));
            var held = Held("test_rage_held");
            int target = SpawnAt(held, new Vector3(0.25f, 0f, 6f)); // dead ahead, a little to the right
            Vector3 at = _em.GetPosition(target);
            float hp0 = _em.GetHp(target);
            int gored = _gore.Gored;
            yield return Run(10f, () => _gore.Gored > gored, 3f); // flat out: 7.1 m/s
            Assert.AreEqual(gored + 1, _gore.Gored, "gored");
            Assert.AreEqual(_goreTrait.damage * _stats.Damage, hp0 - _em.GetHp(target), 0.01f, "8 × Damage");
            yield return new WaitForSeconds(0.4f);
            float sideways = _em.GetPosition(target).x - at.x;
            Assert.Greater(sideways, 0.6f, "tossed aside to the right, out of his path");
            Assert.Greater(sideways, Mathf.Abs(_em.GetPosition(target).z - at.z), "more sideways than along his path");
            Assert.AreEqual(gored + 1, _gore.Gored, "once");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator NotOneBesideHim_NorAtAWalk()
        {
            yield return Boot();
            PutPlayer(Spot(Open) + new Vector3(0f, 0f, -3f));
            var held = Held("test_rage_held");
            int gored = _gore.Gored;
            // at full speed, one appears right beside him: 90° off his heading, then behind
            yield return Run(10f, () => _motor.HorizontalSpeed > 6.5f, 2f);
            Steer(Vector3.forward, 10f);
            int beside = SpawnAt(held, new Vector3(-1f, 0f, 0f));
            yield return Run(10f, () => false, 0.6f);
            Assert.AreEqual(gored, _gore.Gored, "not beside him");
            Assert.AreEqual(_em.GetMaxHp(beside), _em.GetHp(beside), 0.01f);

            // at a walk, straight into one
            _em.DespawnAll();
            PutPlayer(Spot(Open) + new Vector3(0f, 0f, -3f));
            yield return new WaitForSeconds(0.3f);
            int ahead = SpawnAt(held, new Vector3(0f, 0f, 2f));
            float top = 0f;
            yield return Run(2f, () => { top = Mathf.Max(top, _motor.HorizontalSpeed); return false; }, 2.5f);
            Assert.Greater(PlayerPos.z, _em.GetPosition(ahead).z, "he walked right through it");
            Assert.Less(top, _goreTrait.minSpeed, "at a walk");
            Assert.AreEqual(gored, _gore.Gored, "not at a walk (2 m/s)");
            Assert.AreEqual(_em.GetMaxHp(ahead), _em.GetHp(ahead), 0.01f);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheSameEnemy_IsntGoredTwice_WithinASecond()
        {
            yield return Boot();
            Vector3 start = Spot(Open) + new Vector3(0f, 0f, -3f);
            PutPlayer(start);
            var post = Held("test_rage_post", 0f); // it stays where it is: he runs through it and back
            int e = SpawnAt(post, new Vector3(0f, 0f, 5f));
            float ez = _em.GetPosition(e).z;
            int gored = _gore.Gored;
            yield return Run(10f, () => _gore.Gored > gored, 3f);
            Assert.AreEqual(gored + 1, _gore.Gored, "the first pass gores it");
            float first = Time.time;
            yield return Run(10f, () => PlayerPos.z > ez + 0.4f, 1f);
            // straight back through it, well within the second
            float end = Time.time + 0.6f;
            while (Time.time < end && PlayerPos.z > ez - 1.5f) { Steer(Vector3.back, 10f); yield return null; }
            Assert.Less(PlayerPos.z, ez, "back through it");
            Assert.Less(Time.time - first, 1f, "within the second");
            Assert.AreEqual(gored + 1, _gore.Gored, "not gored again within 1 s");
            Stop();
            while (Time.time - first < 1.2f) yield return null;
            PutPlayer(start);
            yield return new WaitForSeconds(0.2f);
            yield return Run(10f, () => _gore.Gored > gored + 1, 3f);
            Assert.AreEqual(gored + 2, _gore.Gored, "after the second, again");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Unchained_HeGoresFromEverySide_AtAJog()
        {
            yield return Boot();
            PutPlayer(Spot(Open) + new Vector3(0f, 0f, -3f));
            Assert.IsTrue(_rage.Unchain());
            var held = Held("test_rage_held");
            yield return Run(3f, () => _motor.HorizontalSpeed > 2.9f, 2f);
            Steer(Vector3.forward, 3f);
            Assert.Less(_motor.HorizontalSpeed, _goreTrait.minSpeed, "slower than calm Gore's 4 m/s");
            int gored = _gore.Gored;
            int behind = SpawnAt(held, new Vector3(0f, 0f, -0.9f)); // right behind him
            float hp0 = _em.GetHp(behind);
            yield return Run(3f, () => _gore.Gored > gored, 1f);
            Assert.AreEqual(gored + 1, _gore.Gored, "gored from behind");
            Assert.AreEqual(_goreTrait.unchainedDamage * _stats.Damage, hp0 - _em.GetHp(behind), 0.01f, "14 × Damage");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ABoss_IsGored_ButNotMoved()
        {
            yield return Boot();
            PutPlayer(Spot(Open) + new Vector3(0f, 0f, -3f));
            var boss = Held("test_rage_boss", 1f, EnemyBehaviour.Scripted);
            int b = SpawnAt(boss, new Vector3(0.25f, 0f, 6f));
            Vector3 at = _em.GetPosition(b);
            float hp0 = _em.GetHp(b);
            int gored = _gore.Gored;
            yield return Run(10f, () => _gore.Gored > gored, 3f);
            Assert.AreEqual(gored + 1, _gore.Gored, "gored");
            Assert.AreEqual(_goreTrait.damage * _stats.Damage, hp0 - _em.GetHp(b), 0.01f, "hurt");
            yield return new WaitForSeconds(0.4f);
            Assert.Less(Flat(_em.GetPosition(b) - at).magnitude, 0.05f, "not tossed");
        }
    }
}
