using System.Collections;
using System.IO;
using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gladiator.Tests
{
    /// <summary>
    /// Phase 5.2 (docs/GLADIATORS.md): the Pit Goblin spawns with its own model, capsule, stats, Bomb Satchel and
    /// traits; bombs land on the thickest group and blast its radius (never the player); snares hold and bleed the first
    /// enemy through and expire; Scavenger lends on its timer but never cuts a gift loan short; the rattling crate
    /// rattles only when you're near, breaks and saves the flag; the flag unlocks the goblin on the select screen, and a
    /// mouse click on its card picks it (playtest bug: the cards ignored the pointer) and puts it in the diorama.
    /// </summary>
    public class GoblinPlayTests
    {
        const string TestArena = "Assets/Scenes/Dev/TestArena.unity";
        const string SlavePit = "Assets/Scenes/Arenas/SlavePit.unity";

        static T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        static GladiatorDef Goblin => Load<GladiatorDef>("Assets/Data/Gladiators/GL_PitGoblin.asset");

        EnemyManager _em;
        EnemyDef _convict;
        WeaponSystem _weapons;

        [TearDown]
        public void TearDown()
        {
            DevSettings.ResetAll();
            TimeController.ResetForNewRun();
            GladiatorLoadout.NextOverride = null;
            GameSession.Clear(); // a run started as the goblin must not make the next fixture's player a goblin
        }

        IEnumerator Boot(string scene, GladiatorDef as_)
        {
            DevSettings.ResetAll();
            DevSettings.GodMode = true;
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            GladiatorLoadout.NextOverride = as_;
            var op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            while (RunIntro.Instance != null && !RunIntro.Instance.Done) yield return null;
            var spawner = Object.FindAnyObjectByType<DebugEnemySpawner>();
            if (spawner != null) { _convict = spawner.enemy; spawner.enabled = false; }
            if (Director.Instance != null) Director.Instance.enabled = false;
            var lvl = Object.FindAnyObjectByType<LevelUpController>();
            if (lvl != null) lvl.enabled = false;
            Player.Main.OverrideSource = new ScriptedInput();
            _em = EnemyManager.Instance;
            _weapons = Player.Main.GetComponent<WeaponSystem>();
            if (_convict == null) _convict = Load<EnemyDef>("Assets/Data/Enemies/EN_Convict.asset");
            yield return new WaitForSeconds(0.2f);
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
            return null;
        }

        bool Hurt(int i) => !_em.IsAlive(i) || _em.GetHp(i) < _em.GetMaxHp(i) - 0.01f;

        [UnityTest, Timeout(60000)]
        public IEnumerator Goblin_SpawnsWithItsModel_Capsule_Stats_SatchelAndTraits()
        {
            yield return Boot(TestArena, Goblin);
            var p = Player.Main;
            Assert.AreSame(Goblin, p.GetComponent<GladiatorLoadout>().Current);
            var visual = p.transform.Find("Visual");
            Assert.AreEqual(1, visual.childCount, "the Nobody's model is gone");
            Assert.AreEqual("CH_PitGoblin", visual.GetChild(0).name);
            Assert.IsNotNull(FindDeep(visual, "Head"), "same part names: the animator drives it");
            Assert.IsNotNull(FindDeep(visual, "ArmR"));
            Assert.AreEqual(1.0f, p.GetComponent<CharacterController>().height, 0.05f, "a goblin-sized capsule");
            Assert.AreEqual(70f, p.GetComponent<PlayerHealth>().MaxHp, 0.01f);
            Assert.AreEqual(1.12f, p.GetComponent<PlayerStats>().Get(StatId.MoveSpeed), 0.001f);
            Assert.AreEqual(1, _weapons.Weapons.Count);
            Assert.AreEqual("bomb_satchel", _weapons.Weapons[0].Def.id);
            Assert.IsNotNull(p.GetComponent<Scavenger>());
            Assert.IsNotNull(p.GetComponent<SnareLayer>());
            var build = p.GetComponent<PlayerBuild>();
            var card = build.FindWeaponCard(_weapons.Weapons[0].Def);
            Assert.AreEqual(1, build.State.LevelOf(card), "the satchel is owned from the start");
            Assert.IsTrue(build.State.IsLocked(card), "fresh save: nobody else finds bombs in the cards yet");
            Assert.IsTrue(UpgradeRoller.IsEligible(card, build.State, build.config), "but the goblin can level its own");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Bombs_LandOnTheThickestGroup_AndBlastItsRadius_NeverThePlayer()
        {
            yield return Boot(TestArena, Goblin);
            var root = Load<StatusDef>("Assets/Data/Status/SE_Root.asset");
            Vector3 p = Player.Main.transform.position;
            var group = new int[4];
            for (int k = 0; k < 4; k++) group[k] = _em.Spawn(_convict, p + new Vector3(0f, 0f, 6f) + Quaternion.Euler(0f, k * 90f, 0f) * Vector3.right * 0.6f);
            int lone = _em.Spawn(_convict, p + new Vector3(-6f, 0f, -2f));
            var health = Player.Main.GetComponent<PlayerHealth>();
            var pp = PlayerProjectiles.Instance;
            int blasts = pp.Blasts;
            float end = Time.time + 5f;
            while (Time.time < end && pp.Blasts == blasts)
            {
                for (int k = 0; k < 4; k++) _em.ApplyStatus(group[k], root, 0f); // held still, so the test is about aim, not chase
                _em.ApplyStatus(lone, root, 0f);
                yield return null;
            }
            Assert.Greater(pp.Blasts, blasts, "a bomb went off");
            foreach (int i in group) Assert.IsTrue(Hurt(i), "the whole group is in the blast");
            Assert.IsFalse(Hurt(lone), "the bomb went for the group, not the lone convict");
            Assert.AreEqual(health.MaxHp, health.Hp, 0.01f, "bombs never hurt the goblin");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Snare_HoldsAndBleeds_TheFirstEnemyThrough_ThenTheGroundClears()
        {
            yield return Boot(TestArena, Goblin);
            _weapons.RemoveAll();
            var layer = Player.Main.GetComponent<SnareLayer>();
            layer.trait = Object.Instantiate(layer.trait);
            var trait = layer.trait;
            Vector3 p = Player.Main.transform.position;
            layer.Place(p + Vector3.forward * 3f);
            int convict = _em.Spawn(_convict, p + Vector3.forward * 7f); // walks at the player, through the snare
            float end = Time.time + 6f;
            while (Time.time < end && layer.Snapped == 0) yield return null;
            Assert.AreEqual(1, layer.Snapped, "the jaws closed");
            Assert.AreEqual(0, layer.Count, "a snare is used up");
            Assert.IsTrue(_em.HasStatus(convict, StatusKind.Root), "held");
            Assert.AreEqual(trait.bleedStacks, _em.StatusStacks(convict, StatusKind.Bleed), "and bleeding");
            Vector3 at = _em.GetPosition(convict);
            yield return new WaitForSeconds(0.6f);
            Assert.Less(Vector3.Distance(at, _em.GetPosition(convict)), 0.25f, "it can't walk while rooted");

            _em.KillAll();
            yield return new WaitForSeconds(0.6f); // past the cooldown
            int placed = layer.Placed;
            GameEvents.OnSlide.Raise(new SlideEvent { Position = p, Velocity = Vector3.forward * 8f });
            Assert.AreEqual(placed + 1, layer.Placed, "a slide drops a snare");
            for (int k = 0; k < 4; k++) layer.Place(p + Vector3.right * (2f + k));
            Assert.AreEqual(trait.maxSnares, layer.Count, "never more than the cap on the ground");
            trait.lifetime = 0.4f;
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(0, layer.Count, "and they don't wait forever");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Scavenger_LendsOnItsTimer_ButNeverCutsAGiftShort()
        {
            yield return Boot(TestArena, Goblin);
            var s = Player.Main.GetComponent<Scavenger>();
            s.trait = Object.Instantiate(s.trait);
            s.trait.firstAfter = 0.3f;
            s.trait.interval = 1.2f;
            s.trait.duration = 0.5f;
            s.ResetTimer();
            yield return new WaitForSeconds(0.55f);
            Assert.AreEqual(1, s.Fired, "out of the sack");
            Assert.IsNotNull(_weapons.Temporary, "lent beside the slots");
            Assert.AreNotEqual("bomb_satchel", _weapons.Temporary.Def.id, "something it doesn't hold");
            Assert.AreSame(s.LastWeapon, _weapons.Temporary.Def);
            Assert.AreEqual(s.trait.LevelFor(RunState.Current.Level), _weapons.Temporary.Level);
            yield return new WaitForSeconds(0.5f);
            Assert.IsNull(_weapons.Temporary, "only for a while");

            WeaponDef gift = null;
            foreach (var u in Player.Main.GetComponent<PlayerBuild>().catalog.upgrades)
                if (u != null && u.kind == UpgradeKind.Weapon && u.weapon != null && _weapons.Find(u.weapon) == null) { gift = u.weapon; break; }
            _weapons.GrantTemporary(gift, 4, 3f); // the crowd's weapon in the sand
            yield return new WaitForSeconds(1.4f); // the timer comes due in here
            Assert.AreEqual(1, s.Fired, "it waits while a gift is out");
            Assert.AreSame(gift, _weapons.Temporary.Def);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Scavenger_NeverLendsAnEvolution()
        {
            yield return Boot(TestArena, Goblin);
            var s = Player.Main.GetComponent<Scavenger>();
            s.enabled = false; // pulled by hand below
            var ups = Player.Main.GetComponent<PlayerBuild>().catalog.upgrades;
            int evolutions = 0;
            foreach (var u in ups) if (u != null && u.IsEvolution) evolutions++;
            Assert.Greater(evolutions, 0, "the pool has evolution cards");
            for (int k = 0; k < 40; k++)
            {
                Assert.IsTrue(s.Fire(), "something to lend");
                foreach (var u in ups)
                    if (u != null && u.IsEvolution) Assert.AreNotSame(u.weapon, s.LastWeapon, "an evolution is earned, never lent (" + u.weapon.id + ")");
            }
            yield return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator RattlingCrate_RattlesOnlyNearby_BreaksAndSavesTheFlag()
        {
            SaveSandbox.Wipe();
            yield return Boot(SlavePit, null);
            _weapons.RemoveAll();
            var crate = RattlingCrate.Instance;
            Assert.IsNotNull(crate, "the Slave Pit hides a crate");
            Assert.AreEqual(RattlingCrate.Phase.Waiting, crate.State, "it's there on a fresh save");
            crate.Place(0);
            var motor = Player.Main.GetComponent<PlayerMotor>();
            Vector3 toCenter = (crate.arenaCenter - crate.Position);
            toCenter.y = 0f;
            toCenter.Normalize();
            motor.Teleport(crate.Position + toCenter * 13f + Vector3.up * 0.5f);
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(0, crate.Rattles, "silent from 13 m");
            motor.Teleport(crate.Position + toCenter * 5f + Vector3.up * 0.5f);
            yield return new WaitForSeconds(3f);
            Assert.GreaterOrEqual(crate.Rattles, 1, "it rattles when you're near");

            var run = RunState.Current;
            run.AddGold(8);
            Assert.IsFalse(MetaProgress.Data.HasFlag(crate.flag));
            motor.Teleport(crate.Position + Vector3.up * 0.3f);
            float end = Time.time + 1f;
            while (Time.time < end && crate.State == RattlingCrate.Phase.Waiting) yield return null;
            Assert.AreNotEqual(RattlingCrate.Phase.Waiting, crate.State, "walked into it: it breaks");
            Assert.AreEqual(3, run.Gold, "the goblin grabs 5 gold");
            Assert.IsTrue(MetaProgress.Data.HasFlag(crate.flag), "the discovery is in the save");
            Assert.IsTrue(File.ReadAllText(SaveSandbox.TestPath).Contains(crate.flag), "on disk at once, not at the run's end");
            yield return new WaitForSeconds(2.2f);
            Assert.AreEqual(RattlingCrate.Phase.Gone, crate.State, "and it dives out of the pit");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator TheFlag_UnlocksTheGoblin_OnTheSelectScreen_AndARunUsesItsKit()
        {
            SaveSandbox.Wipe();
            var op = SceneManager.LoadSceneAsync(GameSession.MenuScene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            var menu = Object.FindAnyObjectByType<Gladiator.UI.MainMenu>();
            Assert.IsFalse(Gladiator.UI.MainMenu.IsAvailable(Goblin), "locked on a fresh save");
            Assert.IsFalse(menu.Pick(Goblin));

            MetaProgress.SetFlag("rattling_crate"); // found the crate, then quit the run
            op = SceneManager.LoadSceneAsync(GameSession.MenuScene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            menu = Object.FindAnyObjectByType<Gladiator.UI.MainMenu>();
            Assert.IsTrue(Gladiator.UI.MainMenu.IsAvailable(Goblin), "the menu grants it");
            Assert.IsTrue(menu.Pick(Goblin));
            Assert.AreSame(Goblin, menu.Selected);
            menu.GetType().GetMethod("StartRun", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(menu, null);
            float end = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < end && SceneManager.GetActiveScene().name != "SlavePit") yield return null;
            yield return null;
            Assert.AreEqual("SlavePit", SceneManager.GetActiveScene().name);
            var p = Player.Main;
            Assert.AreSame(Goblin, p.GetComponent<GladiatorLoadout>().Current);
            Assert.AreEqual("CH_PitGoblin", p.transform.Find("Visual").GetChild(0).name);
            Assert.AreEqual(70f, p.GetComponent<PlayerHealth>().MaxHp, 0.01f);
            Assert.AreEqual("bomb_satchel", p.GetComponent<WeaponSystem>().Weapons[0].Def.id);
            Assert.IsFalse(p.GetComponent<PlayerBuild>().State.IsLocked(p.GetComponent<PlayerBuild>().FindWeaponCard(p.GetComponent<WeaponSystem>().Weapons[0].Def)),
                "unlocked: the satchel is in everyone's pool now");
            Assert.IsNull(RattlingCrate.Instance != null && RattlingCrate.Instance.State == RattlingCrate.Phase.Waiting ? RattlingCrate.Instance : null,
                "once found, the crate stops appearing");
        }
        /// <summary>Playtest bug (v0.5.0): the gladiator cards ignored the mouse, so the goblin could be unlocked yet never
        /// picked. A pointer raycast at the card's centre must land on the card's own button, and the click picks it.</summary>
        [UnityTest, Timeout(60000)]
        public IEnumerator ClickingTheGoblinsCard_PicksIt_AndTheDioramaShowsIt()
        {
            SaveSandbox.Wipe();
            MetaProgress.SetFlag("rattling_crate");
            var op = SceneManager.LoadSceneAsync(GameSession.MenuScene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            var menu = Object.FindAnyObjectByType<Gladiator.UI.MainMenu>();
            menu.GetType().GetMethod("OpenSelect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(menu, null);
            yield return null;
            Assert.AreNotSame(Goblin, menu.Selected, "the Nobody starts picked");

            // open cards carry a button, in the order of the menu's earned gladiators
            var available = (GladiatorDef[])menu.GetType().GetField("_available", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(menu);
            int index = System.Array.IndexOf(available, Goblin);
            Assert.GreaterOrEqual(index, 0, "the goblin's card is on the select screen");
            var cards = new System.Collections.Generic.List<UnityEngine.UI.Button>();
            foreach (var b in menu.transform.Find("MenuCanvas/Select").GetComponentsInChildren<UnityEngine.UI.Button>())
                if (b.name == "Card") cards.Add(b);
            Assert.AreEqual(available.Length, cards.Count);
            var goblinCard = cards[index];
            var es = UnityEngine.EventSystems.EventSystem.current;
            var rt = (RectTransform)goblinCard.transform;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector2 centre = (corners[0] + corners[2]) * 0.5f; // screen-space overlay canvas: world = screen pixels
            var ped = new UnityEngine.EventSystems.PointerEventData(es) { position = centre, button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(ped, hits);
            Assert.Greater(hits.Count, 0, "the card takes the pointer");
            var handler = UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(hits[0].gameObject);
            Assert.AreSame(goblinCard.gameObject, handler, "the top hit is the card's button (nothing in front of it eats the click)");
            UnityEngine.EventSystems.ExecuteEvents.Execute(handler, ped, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            yield return null;
            Assert.AreSame(Goblin, menu.Selected, "one click picks the goblin");
            Assert.IsNotNull(menu.hero, "the diorama has a hero to swap");
            Assert.IsFalse(menu.hero.gameObject.activeSelf, "the Nobody steps aside");
            var shown = menu.hero.parent.Find("Hero_pit_goblin");
            Assert.IsNotNull(shown, "the goblin stands in the diorama");
            Assert.IsTrue(shown.gameObject.activeSelf);
            SaveSandbox.Wipe(); // later tests expect a fresh save
        }
    }
}
