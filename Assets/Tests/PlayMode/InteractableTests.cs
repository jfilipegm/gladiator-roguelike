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
    /// M2.7: Armory Chests (pay gold → Uncommon+ pick, price rises, refused without gold, Interact in reach, respawn on
    /// clear ground) and the Blood Altar (max HP for a Rare+ pick, once).
    /// </summary>
    public class InteractablePlayTests
    {
        const string Scene = "Assets/Scenes/Dev/TestArena.unity";
        Player _player;
        Interactables _props;
        LevelUpController _lvl;
        RunState _run;
        ScriptedInput _input;

        IEnumerator Boot()
        {
            DevSettings.ResetAll();
            DevSettings.GodMode = true;
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            GameRandom.Seed(5150); // Q14 (R7): the chests' generator seeds from GameRandom now: a seeded load places them the same way every time
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            Object.FindAnyObjectByType<DebugEnemySpawner>().enabled = false;
            if (Director.Instance != null) Director.Instance.enabled = false;
            _player = Player.Main;
            _input = new ScriptedInput();
            _player.OverrideSource = _input;
            _player.GetComponent<WeaponSystem>().RemoveAll();
            _props = Interactables.Instance;
            _lvl = LevelUpController.Instance;
            _run = RunState.Current;
            yield return new WaitForSeconds(0.2f); // starting chests and the altar are placed on the first ticks
        }

        [TearDown]
        public void TearDown() => DevSettings.ResetAll();

        Vector3 Ahead(float z) => _player.transform.position + new Vector3(0f, 0f, z);

        IEnumerator WaitOpen()
        {
            float end = Time.realtimeSinceStartup + 2f;
            while (!_lvl.IsOpen && Time.realtimeSinceStartup < end) yield return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Chest_PaysGold_OpensUncommonPlusPick_PriceRises()
        {
            yield return Boot();
            int chest = _props.PlaceChestAt(Ahead(3f));
            Assert.GreaterOrEqual(chest, 0);
            _run.AddGold(100);
            int price = _props.ChestPrice;
            Assert.IsTrue(_props.TryUse(chest));
            Assert.AreEqual(100 - price, _run.Gold, "paid the price");
            Assert.AreEqual(Interactables.State.Opening, _props.StateOf(chest));
            yield return WaitOpen();
            Assert.IsTrue(_lvl.IsOpen, "its pick opens");
            Assert.AreEqual(PickSource.Chest, _lvl.Source);
            for (int i = 0; i < _lvl.ChoiceCount; i++)
                if (_lvl.Offers[i].Def.kind != UpgradeKind.Consumable)
                    Assert.GreaterOrEqual(_lvl.Offers[i].Rarity, Rarity.Uncommon, "chests never offer Commons");
            int owned = _player.GetComponent<PlayerBuild>().State.OwnedCount;
            _lvl.Choose(0);
            yield return null;
            Assert.IsFalse(_lvl.IsOpen);
            Assert.AreEqual(0, _run.PendingLevelUps, "a chest pick is not a level-up");
            Assert.GreaterOrEqual(_player.GetComponent<PlayerBuild>().State.OwnedCount, owned);
            Assert.Greater(_props.ChestPrice, price, "the next chest costs more");
            yield return new WaitForSeconds(_props.openLinger + 0.6f);
            Assert.AreEqual(Interactables.State.Free, _props.StateOf(chest), "the opened chest goes away");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Chest_WithoutEnoughGold_StaysShut()
        {
            yield return Boot();
            int chest = _props.PlaceChestAt(Ahead(3f));
            Assert.AreEqual(0, _run.Gold);
            Assert.IsFalse(_props.TryUse(chest));
            Assert.AreEqual(Interactables.State.Ready, _props.StateOf(chest));
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(_lvl.IsOpen);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Interact_InReach_OpensIt_OutOfReach_DoesNot()
        {
            yield return Boot();
            _run.AddGold(100);
            int near = _props.PlaceChestAt(Ahead(1.2f));
            int far = -1; // a starting chest: placed at least minPlayerDistance away
            for (int i = 0; i < _props.SlotCount && far < 0; i++)
                if (i != near && _props.IsReady(i) && _props.KindOf(i) == Interactables.Kind.Chest) far = i;
            Assert.GreaterOrEqual(far, 0);
            yield return null;
            Assert.AreEqual(near, _props.InReach, "the chest within reach");
            _input.Next.InteractPressed = true;
            yield return null;
            yield return null;
            Assert.AreEqual(Interactables.State.Opening, _props.StateOf(near), "E opened it");
            Assert.AreEqual(Interactables.State.Ready, _props.StateOf(far));
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Altar_TakesMaxHp_RarePlusPick_OnlyOnce()
        {
            yield return Boot();
            var health = _player.GetComponent<PlayerHealth>();
            int altar = _props.PlaceAltarAt(Ahead(3f));
            Assert.GreaterOrEqual(altar, 0);
            float max0 = health.MaxHp;
            Assert.IsTrue(_props.TryUse(altar));
            Assert.AreEqual(max0 * (1f - _props.config.altarMaxHpCost), health.MaxHp, 0.01f, "max HP lost for good");
            Assert.LessOrEqual(health.Hp, health.MaxHp);
            yield return WaitOpen();
            Assert.AreEqual(PickSource.Altar, _lvl.Source);
            for (int i = 0; i < _lvl.ChoiceCount; i++)
                if (_lvl.Offers[i].Def.kind != UpgradeKind.Consumable)
                    Assert.GreaterOrEqual(_lvl.Offers[i].Rarity, Rarity.Rare);
            _lvl.Skip();
            yield return null;
            Assert.IsFalse(_props.TryUse(altar), "an altar works once");
            Assert.AreEqual(1, _props.AltarsUsed);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Chests_AppearOnClearFlatGround_AwayFromThePlayer()
        {
            yield return Boot();
            var em = EnemyManager.Instance;
            var cfg = _props.config;
            int ready = 0;
            for (int i = 0; i < _props.SlotCount; i++)
            {
                if (!_props.IsReady(i)) continue;
                ready++;
                var p = _props.PositionOf(i);
                Assert.IsFalse(em.Obstacles.Overlaps(new Vector2(p.x, p.z), cfg.clearance), "not in a wall/pillar/prop");
                Assert.Less(Mathf.Abs(em.Height.Sample(p.x + cfg.clearance, p.z) - p.y), 0.21f, "on flat ground");
                Vector3 d = p - _player.transform.position;
                Assert.GreaterOrEqual(new Vector2(d.x, d.z).magnitude, cfg.minPlayerDistance - 1f, "away from the player");
            }
            Assert.AreEqual(cfg.startingChests + cfg.altars, ready, "starting chests + the altar");
        }
    }
}
