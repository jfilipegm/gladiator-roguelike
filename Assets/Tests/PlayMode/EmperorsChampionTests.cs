using System.Collections;
using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gladiator.Tests
{
    /// <summary>
    /// R1 (docs/BOSSES.md "The Emperor's Champion"): he enters through the triumphal gate with his spear, scutum and
    /// javelins and strides in when the player is far; the Spear Thrust, the Shield Bash and the Javelin Volley each
    /// telegraph, then hit (a javelin reaches a player on the spina); at 60 % "PRAETORIANS, TO ME!" brings a Centurion
    /// and his line through each of the two gates nearest the player, and the Shield Charge stuns him on the obelisk; at
    /// 30 % "FOR THE EMPEROR!": wind-ups ÷ 1.4, the charge twice in a row and the chariot races back to back; at his fall
    /// the races halt and the Guard stands down. Gorm, with no phase 3, still roars only once and lights his fence.
    /// Positions are the contract's shared facts (docs/tracks/R1-imperial-arena.md): the track lane runs at z = ±14
    /// (±2), the spina along z = 0 (6 m wide, 1.2 m high) from x = -60 to 40 with the obelisk at x = -10.
    /// Needs the Imperial Arena scene and EN_Centurion (the layout part) plus ChariotRace and the formations.
    /// </summary>
    public class EmperorsChampionTests
    {
        const string Scene = "Assets/Scenes/Arenas/ImperialArena.unity";
        const string SlavePit = "Assets/Scenes/Arenas/SlavePit.unity";
        static T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        static EnemyDef Centurion => Load<EnemyDef>("Assets/Data/Enemies/EN_Centurion.asset");

        /// <summary>The south outer field, clear sand between the track (z -12..-16) and the terraces (z -39..-44).</summary>
        static readonly Vector3 Field = new Vector3(-30f, 0f, -28f);
        /// <summary>The floor between the spina (z -3) and the south straight (z -12).</summary>
        static readonly Vector3 InnerSouth = new Vector3(-30f, 0f, -8f);
        /// <summary>On top of the spina, away from its stairs (x = -45, -20, 5, 30) and the obelisk.</summary>
        static readonly Vector3 SpinaTop = new Vector3(-30f, 1.2f, 0f);

        Player _player;
        PlayerMotor _motor;
        PlayerHealth _health;
        EnemyManager _em;
        BossFight _fight;
        int _damaged;
        string _phaseText, _guardsText;

        void OnDamaged(in DamagedEvent e) { if (!e.Status) _damaged++; }

        void OnAnnounce(in AnnounceEvent e)
        {
            if (e.Kind == AnnounceKind.BossPhase) _phaseText = e.Text;
            if (e.Kind == AnnounceKind.BossGuards) _guardsText = e.Text;
        }

        BossController Boss => BossController.Active;

        IEnumerator BootToBoss(string scene = Scene, string id = "emperors_champion")
        {
            DevSettings.ResetAll();
            DevSettings.GodMode = true;
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            while (RunIntro.Instance != null && !RunIntro.Instance.Done) yield return null;
            _player = Player.Main;
            _motor = _player.GetComponent<PlayerMotor>();
            _health = _player.GetComponent<PlayerHealth>();
            _health.invulnerableTime = 0f;
            _player.OverrideSource = new ScriptedInput();
            _player.GetComponent<WeaponSystem>().RemoveAll();
            var lvl = Object.FindAnyObjectByType<LevelUpController>();
            if (lvl != null) lvl.enabled = false;
            _em = EnemyManager.Instance;
            _fight = Object.FindAnyObjectByType<BossFight>();
            Assert.IsNotNull(_fight, scene + " has its boss fight");
            _damaged = 0;
            _phaseText = _guardsText = null;
            GameEvents.OnDamaged.Subscribe(OnDamaged);
            GameEvents.OnAnnounce.Subscribe(OnAnnounce);
            yield return new WaitForSeconds(0.3f);
            _fight.StartNow();
            float end = Time.time + 12f;
            while (Time.time < end && (BossController.Active == null || BossController.Active.CurrentState == BossController.State.Entering
                                       || BossController.Active.CurrentState == BossController.State.Roaring)) yield return null;
            Assert.IsNotNull(Boss, "the boss is in");
            Assert.AreEqual(id, Boss.def.id);
        }

        [TearDown]
        public void TearDown()
        {
            GameEvents.OnDamaged.Unsubscribe(OnDamaged);
            GameEvents.OnAnnounce.Unsubscribe(OnAnnounce);
            TimeController.ResetForNewRun();
            DevSettings.ResetAll();
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var f = FindDeep(t.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }

        static IEnumerator WaitFor(System.Func<bool> done, float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end && !done()) yield return null;
        }

        Vector3 Forward => Quaternion.Euler(0f, _em.GetYaw(Boss.Slot), 0f) * Vector3.forward;

        /// <summary>Puts him at `at` facing +X and the player `d` m in front of him; waits a frame.</summary>
        IEnumerator Face(Vector3 at, float d)
        {
            Boss.Warp(at, 90f);
            _motor.Teleport(at + Vector3.right * d + Vector3.up * 0.1f);
            yield return null;
        }

        /// <summary>One unblockable hit to just under phase `phase`'s threshold, then his whole roar.</summary>
        IEnumerator ToPhase(int phase)
        {
            float max = _em.GetMaxHp(Boss.Slot);
            float at = phase == 2 ? Boss.def.phase2At : Boss.def.phase3At;
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = _em.GetHp(Boss.Slot) - max * at + 10f, Flags = DamageFlags.Status });
            yield return null;
            Assert.AreEqual(BossController.State.Enraging, Boss.CurrentState, "he roars into phase " + phase);
            yield return WaitFor(() => Boss.Phase == phase && Boss.CurrentState != BossController.State.Enraging, Boss.def.enrageRoar + 1f);
            Assert.AreEqual(phase, Boss.Phase);
        }

        /// <summary>Kills his Guard (the Centurions and their Praetorians) so later steps aren't shoved about.</summary>
        void ClearGuards()
        {
            var slots = new int[64];
            foreach (var def in new[] { Centurion != null ? Centurion.escort : null, Centurion })
            {
                if (def == null) continue;
                int n = _em.FindAlive(def, slots);
                for (int k = 0; k < n; k++) _em.ApplyDamage(slots[k], new DamageInfo { Amount = 1e7f, Flags = DamageFlags.Status });
            }
        }

        /// <summary>The two gate indices nearest `p`, nearest first.</summary>
        static int[] TwoNearestGates(Vector3 p)
        {
            var gates = ArenaGates.Instance;
            int a = -1, b = -1;
            float da = float.MaxValue, db = float.MaxValue;
            for (int i = 0; i < gates.Count; i++)
            {
                float d = Flat(gates.Position(i) - p).sqrMagnitude;
                if (d < da) { b = a; db = da; a = i; da = d; }
                else if (d < db) { b = i; db = d; }
            }
            return new[] { a, b };
        }

        // ---------------------------------------------------------------- entrance and movement

        [UnityTest, Timeout(90000)]
        public IEnumerator HeEntersThroughTheTriumphalGate_StridesWhenYoureFar_AndHisRoarLastsEvenMidStride()
        {
            yield return BootToBoss();
            Assert.AreEqual(1, _em.AliveCount, "the horde was dragged off: only him");
            Assert.AreEqual(0, _fight.gate, "he comes through gate 0");
            Assert.Greater(ArenaGates.Instance.Position(0).x, 85f, "gate 0 is the triumphal gate at the sphendone's apex");
            Assert.Greater(Boss.Position.x, 80f, "in through the triumphal gate");
            Assert.AreEqual(1, Boss.Phase);
            Assert.IsTrue(Boss.ShieldUp, "his scutum blocks from the front");
            var root = _em.GetViewRoot(Boss.Slot);
            foreach (var part in new[] { "Spear", "Shield", "Javelins", "Eagle", "WingL", "WingR" })
                Assert.IsNotNull(FindDeep(root, part), "EN_EmperorsChampion has its " + part);

            // ~65 m away on the south field (off the track): he strides at closeInSpeed and doesn't attack
            _motor.Teleport(new Vector3(26f, 0.1f, -30f));
            yield return null;
            Assert.Greater(Flat(_player.transform.position - Boss.Position).magnitude, Boss.def.closeInRange + 20f);
            Vector3 from = Boss.Position;
            int attacks = Boss.AttacksDone;
            float t0 = Time.time;
            yield return new WaitForSeconds(1f);
            float speed = Flat(Boss.Position - from).magnitude / (Time.time - t0);
            Assert.Greater(speed, 6f, "he strides (closeInSpeed 9.5 m/s) while you're farther than closeInRange");
            Assert.AreEqual(BossController.State.Chase, Boss.CurrentState, "no attack at that range");
            Assert.AreEqual(attacks, Boss.AttacksDone);

            // a hit that crosses 60 % in the middle of a long stride: the roar still lasts enrageRoar (the old code ended it
            // on the frame it started, reusing the chase's progress)
            float max = _em.GetMaxHp(Boss.Slot);
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = max * (1f - Boss.def.phase2At) + 10f, Flags = DamageFlags.Status });
            yield return null;
            Assert.AreEqual(BossController.State.Enraging, Boss.CurrentState);
            Assert.AreEqual(Boss.def.phase2Text, _phaseText, "\"PRAETORIANS, TO ME!\"");
            float hp = Boss.Hp01;
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = 500f, Flags = DamageFlags.Status });
            Assert.AreEqual(hp, Boss.Hp01, 1e-6f, "invulnerable while he roars");
            yield return new WaitForSeconds(Boss.def.enrageRoar * 0.6f);
            Assert.AreEqual(BossController.State.Enraging, Boss.CurrentState, "still roaring");
            Assert.AreEqual(1, Boss.Phase);
        }

        // ---------------------------------------------------------------- phase 1 attacks

        [UnityTest, Timeout(90000)]
        public IEnumerator SpearThrust_FillsItsLane_ThenLungesDownIt_NotBesideIt()
        {
            yield return BootToBoss();
            yield return Face(Field, 6f);
            _damaged = 0;
            Boss.ForceAttack(BossAttack.Whip);
            Assert.AreEqual(BossController.State.Windup, Boss.CurrentState);
            Assert.AreEqual(Boss.def.whipWindup, Boss.StateDuration, 1e-4f, "phase 1: the listed wind-up");
            yield return new WaitForSeconds(Boss.def.whipWindup * 0.5f);
            Assert.AreEqual(BossController.State.Windup, Boss.CurrentState);
            Assert.AreEqual(0, _damaged, "the lane fills first");
            yield return WaitFor(() => Boss.CurrentState == BossController.State.Recover, 2f);
            Assert.AreEqual(1, _damaged, "lunged down the lane: hit");
            Assert.AreEqual(1f, _motor.SlowMultiplier, 1e-4f, "the spear doesn't slow (whipSlow 1)");

            yield return Face(Field, 6f);
            _damaged = 0;
            Boss.ForceAttack(BossAttack.Whip);
            yield return null;
            _motor.Teleport(Field + new Vector3(6f, 0.1f, 2.5f)); // 2.5 m beside a 1.3 m lane
            yield return WaitFor(() => Boss.CurrentState == BossController.State.Recover, 2f);
            Assert.AreEqual(BossController.State.Recover, Boss.CurrentState);
            Assert.AreEqual(0, _damaged, "beside the lane: missed");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator ShieldBash_FillsItsCone_ThenPunchesThePlayerAway()
        {
            yield return BootToBoss();
            yield return Face(Field, 2.8f);
            _damaged = 0;
            Boss.ForceAttack(BossAttack.Bash);
            Assert.AreEqual(BossController.State.Windup, Boss.CurrentState);
            Assert.AreEqual(Boss.def.bashWindup, Boss.StateDuration, 1e-4f);
            yield return new WaitForSeconds(Boss.def.bashWindup * 0.5f);
            Assert.AreEqual(0, _damaged, "the cone fills first");
            yield return WaitFor(() => _damaged > 0, 1.5f);
            Assert.AreEqual(1, _damaged, "in the cone: bashed");
            yield return new WaitForSeconds(0.35f);
            Assert.Greater(Flat(_player.transform.position - Boss.Position).magnitude, 2.8f + 0.8f, "and punched back (bashKnockback 12)");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator JavelinVolley_GlowsFirst_ThenFliesAtYourHeight_ReachingYouOnTheSpina()
        {
            yield return BootToBoss();
            _motor.Teleport(SpinaTop + Vector3.up * 0.1f);
            yield return new WaitForSeconds(0.3f);
            Assert.Greater(_player.transform.position.y, 1f, "the player stands on the 1.2 m spina");
            Boss.Warp(InnerSouth, 0f); // on the floor 8 m south of you, facing the spina
            _damaged = 0;
            Boss.ForceAttack(BossAttack.Volley);
            Assert.AreEqual(Boss.def.volleyWindup, Boss.StateDuration, 1e-4f);
            yield return new WaitForSeconds(Boss.def.volleyWindup * 0.5f);
            Assert.AreEqual(0, Boss.JavelinsFired, "the fan's lanes glow first");
            yield return WaitFor(() => _damaged > 0, 2.5f);
            Assert.AreEqual(Boss.def.volleyCount.x, Boss.JavelinsFired, "five javelins in phase 1");
            Assert.AreEqual(0, Boss.CoinsFired, "javelins, not coins");
            Assert.GreaterOrEqual(_damaged, 1, "thrown at your height: the spina is no shelter");
        }

        // ---------------------------------------------------------------- phase 2

        [UnityTest, Timeout(90000)]
        public IEnumerator PhaseTwo_TwoFormationsMarchInThroughTheTwoGatesNearestYou_AndAgainOnTheClock()
        {
            yield return BootToBoss();
            var centurion = Centurion;
            Assert.IsNotNull(centurion, "EN_Centurion exists (ImperialArenaSetup)");
            Assert.IsNotNull(Boss.def.guards);
            Assert.AreSame(centurion, Boss.def.guards[0], "his guards are Centurions");
            var praetorian = centurion.escort;
            Assert.IsNotNull(praetorian, "a Centurion leads Praetorians");

            // phase 1: the Guard waits (guardsFromPhase 2), however long the fight
            TimeController.GameSpeed = 4f;
            float end = Time.time + Boss.def.guardsEvery + 2f;
            while (Time.time < end && Boss.Phase == 1) yield return null;
            TimeController.GameSpeed = 1f;
            Assert.AreEqual(1, Boss.Phase);
            Assert.AreEqual(0, Boss.GuardWaves, "no guards in phase 1");

            // the player by the south-west gate: the two nearest are the two south side gates (x = -50 and 30)
            Vector3 pp = new Vector3(-40f, 0f, -36f);
            _motor.Teleport(pp + Vector3.up * 0.1f);
            Boss.Warp(Field, 90f);
            yield return null;
            int[] near = TwoNearestGates(pp);
            float max = _em.GetMaxHp(Boss.Slot);
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = max * (1f - Boss.def.phase2At) + 10f, Flags = DamageFlags.Status });
            yield return null;
            Assert.AreEqual(BossController.State.Enraging, Boss.CurrentState);
            Assert.AreEqual(Boss.def.phase2Text, _phaseText);
            Assert.AreEqual(0, Boss.GuardWaves, "the line comes as the roar ends");
            yield return WaitFor(() => Boss.GuardWaves > 0, Boss.def.enrageRoar + 1f);
            Assert.AreEqual(1, Boss.GuardWaves, "\"PRAETORIANS, TO ME!\": the first wave on reaching phase 2");
            Assert.AreEqual(2, Boss.Phase);
            Assert.AreEqual(Boss.def.guardsText, _guardsText, "THE PRAETORIAN GUARD!");
            Assert.AreEqual(2, _em.AliveOf(centurion), "two Centurions");
            Assert.AreEqual(2 * centurion.escortCount, _em.AliveOf(praetorian), "each with his line of Praetorians");

            var slots = new int[8];
            int n = _em.FindAlive(centurion, slots);
            bool first = false, second = false;
            for (int k = 0; k < n; k++)
            {
                Vector3 c = _em.GetPosition(slots[k]);
                float d0 = Flat(c - ArenaGates.Instance.Position(near[0])).magnitude;
                float d1 = Flat(c - ArenaGates.Instance.Position(near[1])).magnitude;
                Assert.Less(Mathf.Min(d0, d1), 5f, "a Centurion comes through one of the two gates nearest the player");
                if (d0 < d1) first = true; else second = true;
                Assert.AreEqual(centurion.escortCount, _em.FormedCount(slots[k]), "his line formed on him (EnemyManager.FormedCount)");
            }
            Assert.IsTrue(first && second, "one formation through each of the two nearest gates");
            yield return null;
            Assert.IsTrue(ArenaGates.Instance.IsOpen(near[0]) && ArenaGates.Instance.IsOpen(near[1]), "both gates swing open");

            // then every guardsEvery seconds
            TimeController.GameSpeed = 4f;
            yield return WaitFor(() => Boss.GuardWaves > 1, Boss.def.guardsEvery + 3f);
            TimeController.GameSpeed = 1f;
            Assert.AreEqual(2, Boss.GuardWaves, "the next wave on the clock");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator PhaseTwo_TheShieldCharge_StunsHimOnTheObelisk_GuardDown_TakesHalfAsMuchAgain()
        {
            yield return BootToBoss();
            yield return ToPhase(2);
            ClearGuards();
            // on the spina east of the obelisk (x = -10), the player beyond it: the lane runs into the stone
            Boss.Warp(new Vector3(4f, 1.2f, 0f), -90f);
            _motor.Teleport(new Vector3(-18f, 1.3f, 0f));
            yield return null;
            Boss.ForceAttack(BossAttack.Charge);
            Assert.AreEqual(BossAttack.Charge, Boss.CurrentAttack);
            Assert.AreEqual(Boss.def.chargeWindup / Boss.def.enrageSpeed, Boss.StateDuration, 1e-4f, "phase 2: wind-ups ÷ enrageSpeed");
            yield return WaitFor(() => Boss.CurrentState == BossController.State.Stunned, 4f);
            Assert.AreEqual(BossController.State.Stunned, Boss.CurrentState, "the obelisk stops him dead");
            Assert.AreEqual(1, Boss.Stuns);
            Assert.That(Boss.Position.x, Is.InRange(-9.5f, -3f), "against the obelisk's east face");
            Assert.IsFalse(Boss.ShieldUp, "guard down while stunned");
            float hp = _em.GetHp(Boss.Slot);
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = 100f, Flags = DamageFlags.Melee, Direction = -Forward });
            Assert.AreEqual(hp - 100f * Boss.def.stunDamageTakenMul, _em.GetHp(Boss.Slot), 0.01f, "from the front, unblocked, ×1.5");
            yield return new WaitForSeconds(Boss.def.chargeStun + 0.2f);
            Assert.AreNotEqual(BossController.State.Stunned, Boss.CurrentState);
            Assert.IsTrue(Boss.ShieldUp, "guard back up");
            hp = _em.GetHp(Boss.Slot);
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = 100f, Flags = DamageFlags.Melee, Direction = -Forward });
            Assert.AreEqual(hp - 100f * (1f - Boss.def.enemy.blockReduction), _em.GetHp(Boss.Slot), 0.01f, "the scutum takes half again");
        }

        // ---------------------------------------------------------------- phase 3

        [UnityTest, Timeout(90000)]
        public IEnumerator PhaseThree_ForTheEmperor_WindupsOver1Point4_AndTheChargeComesTwice()
        {
            yield return BootToBoss();
            yield return ToPhase(2);
            ClearGuards();
            yield return Face(Field, 8f);
            float max = _em.GetMaxHp(Boss.Slot);
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = _em.GetHp(Boss.Slot) - max * Boss.def.phase3At + 10f, Flags = DamageFlags.Status });
            yield return null;
            Assert.AreEqual(BossController.State.Enraging, Boss.CurrentState, "he roars again at 30 %");
            Assert.AreEqual(Boss.def.phase3Text, _phaseText, "\"FOR THE EMPEROR!\"");
            float hp = Boss.Hp01;
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = 500f, Flags = DamageFlags.Status });
            Assert.AreEqual(hp, Boss.Hp01, 1e-6f, "invulnerable while he roars");
            yield return WaitFor(() => Boss.CurrentState != BossController.State.Enraging, Boss.def.enrageRoar + 1f);
            Assert.AreEqual(3, Boss.Phase);
            ClearGuards(); // phase 2's guards don't come again on this roar
            Assert.AreEqual(1, Boss.GuardWaves, "only the phase-2 roar called them at once");

            yield return Face(Field, 6f);
            Boss.ForceAttack(BossAttack.Whip);
            Assert.AreEqual(Boss.def.whipWindup / Boss.def.phase3Speed, Boss.StateDuration, 1e-4f, "phase 3: wind-ups ÷ phase3Speed");
            yield return Face(Field, 2.8f);
            Boss.ForceAttack(BossAttack.Bash);
            Assert.AreEqual(Boss.def.bashWindup / Boss.def.phase3Speed, Boss.StateDuration, 1e-4f);

            // the charge: twice in a row, re-aimed (the player steps out of the first lane)
            yield return Face(Field, 10f);
            int charges = Boss.Charges;
            Boss.ForceAttack(BossAttack.Charge);
            Assert.AreEqual(Boss.def.chargeWindup / Boss.def.phase3Speed, Boss.StateDuration, 1e-4f);
            yield return WaitFor(() => Boss.CurrentState == BossController.State.Strike, 2f);
            Assert.AreEqual(BossController.State.Strike, Boss.CurrentState, "first charge");
            _motor.Teleport(Field + new Vector3(10f, 0.1f, 6f));
            yield return WaitFor(() => Boss.CurrentState != BossController.State.Strike, 3f);
            Assert.AreEqual(BossController.State.Windup, Boss.CurrentState, "re-aims for a second charge");
            Assert.AreEqual(BossAttack.Charge, Boss.CurrentAttack);
            yield return WaitFor(() => Boss.CurrentState != BossController.State.Windup, 2f);
            yield return WaitFor(() => Boss.CurrentState != BossController.State.Strike, 3f);
            Assert.AreEqual(charges + 2, Boss.Charges, "phase3ChargeRepeats: two, not three");
            Assert.AreNotEqual(BossController.State.Windup, Boss.CurrentState, "no third");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator PhaseThree_TheChariotRacesRunBackToBack()
        {
            yield return BootToBoss();
            var races = ChariotRace.Instance;
            Assert.IsNotNull(races, "the Imperial Arena has its chariot races");
            yield return ToPhase(2);
            ClearGuards();
            _motor.Teleport(Field + Vector3.up * 0.1f); // off the track
            yield return ToPhase(3);
            // "a race starts at once if none is running" (ChariotRace reads BossController.Active.Phase)
            yield return WaitFor(() => races.Telegraphing || races.Running, 2.5f);
            Assert.IsTrue(races.Telegraphing || races.Running, "phase 3: the races go back to back, the first at once");
            // that race runs out; the next telegraph follows within backToBackGap (2 s)
            TimeController.GameSpeed = 4f;
            yield return WaitFor(() => !races.Telegraphing && !races.Running, 60f);
            Assert.IsFalse(races.Telegraphing || races.Running, "the first race ran out");
            yield return WaitFor(() => races.Telegraphing || races.Running, 3f);
            TimeController.GameSpeed = 1f;
            Assert.IsTrue(races.Telegraphing || races.Running, "and the next one starts straight away");
            Assert.IsTrue(Boss.Alive, "still fighting");
            Assert.AreEqual(3, Boss.Phase);
        }

        // ---------------------------------------------------------------- defeat

        [UnityTest, Timeout(90000)]
        public IEnumerator AtHisFall_TheRacesHalt_AndTheGuardStandsDown()
        {
            yield return BootToBoss();
            var races = ChariotRace.Instance;
            Assert.IsNotNull(races);
            _motor.Teleport(Field + Vector3.up * 0.1f);
            yield return ToPhase(2);
            Assert.Greater(_em.AliveCount, 1 + 2 * Centurion.escortCount, "his formations are on the sand");
            yield return ToPhase(3);
            yield return WaitFor(() => races.Running, 6f);
            Assert.IsTrue(races.Running, "the chariots are on the track");
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = 1e7f, Flags = DamageFlags.Status });
            yield return null;
            yield return null;
            Assert.IsTrue(Boss.Defeated);
            Assert.AreEqual(BossFight.Stage.Falling, _fight.Current);
            Assert.AreEqual(0, _em.AliveCount, "the Guard stands down: every enemy is dragged off");
            yield return new WaitForSecondsRealtime(0.8f); // slow motion: real seconds
            Assert.IsFalse(races.Running || races.Telegraphing, "the races halt (ChariotRace reads BossController.Active.Defeated)");
        }

        // ---------------------------------------------------------------- regression: a boss without phase 3

        [UnityTest, Timeout(90000)]
        public IEnumerator Gorm_HasNoPhaseThree_RoarsOnce_AndStillLightsTheFence()
        {
            yield return BootToBoss(SlavePit, "gorm");
            Assert.AreEqual(0f, Boss.def.phase3At, "phase3At 0: no phase 3");
            Assert.AreEqual(1, Boss.def.guardsFromPhase, "his guard clock runs from the start");
            int zones = HazardZones.Instance.Count;
            float max = _em.GetMaxHp(Boss.Slot);
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = max * 0.9f, Flags = DamageFlags.Status }); // 100 % to 10 % in one hit
            yield return null;
            Assert.AreEqual(BossController.State.Enraging, Boss.CurrentState);
            Assert.AreEqual(Boss.def.phase2Text, _phaseText, "\"BURN IT!\"");
            yield return new WaitForSeconds(Boss.def.enrageRoar + 0.3f);
            Assert.AreEqual(2, Boss.Phase);
            Assert.Greater(HazardZones.Instance.Count, zones + 30, "a ring of fire along the wall");
            _phaseText = null;
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(2, Boss.Phase, "below 30 % and still phase 2");
            Assert.AreNotEqual(BossController.State.Enraging, Boss.CurrentState, "no second roar");
            Assert.IsNull(_phaseText, "no second phase announcement");
        }
    }
}
