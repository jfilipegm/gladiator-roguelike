using Gladiator.Core;
using Gladiator.Gameplay;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Stages review screenshots in Play mode from the bridge (Tools/ueval 'return Gladiator.EditorTools.DevShowcase.X();'):
    /// quiet arena, god mode, weapon off, enemies placed in front of the camera.
    /// </summary>
    public static class DevShowcase
    {
        static EnemyCatalog Catalog => AdminController.Instance.catalog;

        static EnemyDef Def(string id)
        {
            foreach (var e in Catalog.enemies) if (e.id == id) return e;
            return null;
        }

        static void Quiet(out Vector3 p, out Vector3 fwd, out Vector3 right, bool weapon = false)
        {
            Object.FindAnyObjectByType<DebugEnemySpawner>().enabled = false;
            EnemyManager.Instance.KillAll();
            DevSettings.GodMode = true;
            Player.Main.GetComponent<WeaponSystem>().enabled = weapon;
            p = Player.Main.transform.position;
            var cam = Camera.main.transform;
            fwd = new Vector3(cam.forward.x, 0f, cam.forward.z).normalized;
            right = Vector3.Cross(Vector3.up, fwd);
        }

        /// <summary>All six types in a row, the three elites behind them.</summary>
        public static string Lineup()
        {
            Quiet(out var p, out var fwd, out var right);
            var em = EnemyManager.Instance;
            string[] ids = { "convict", "pitcur", "shieldthug", "slinger", "brute", "firepot" };
            for (int k = 0; k < ids.Length; k++) em.Spawn(Def(ids[k]), p + fwd * 6.5f + right * ((k - 2.5f) * 1.6f));
            for (int k = 0; k < Catalog.affixes.Length; k++) em.Spawn(Def("convict"), p + fwd * 10f + right * ((k - 1f) * 2.4f), Catalog.affixes[k]);
            return "alive " + em.AliveCount;
        }

        /// <summary>Attackers around the player: brutes close (slam), slingers and fire-pots at range, a cur pack.</summary>
        public static string Fight()
        {
            Quiet(out var p, out var fwd, out var right);
            var em = EnemyManager.Instance;
            em.Spawn(Def("brute"), p + fwd * 2.2f + right * 0.8f);
            em.Spawn(Def("slinger"), p + fwd * 7f - right * 4f);
            em.Spawn(Def("slinger"), p + fwd * 8f + right * 3f);
            em.Spawn(Def("firepot"), p + fwd * 9f);
            em.Spawn(Def("firepot"), p + fwd * 8f + right * 6f);
            for (int k = 0; k < 4; k++) em.Spawn(Def("pitcur"), p + fwd * 6f - right * (1f + k * 0.7f));
            em.Spawn(Def("shieldthug"), p + fwd * 4f - right * 2f);
            em.Spawn(Def("convict"), p + fwd * 5f + right * 2f, Catalog.affixes[1]);
            return "alive " + em.AliveCount;
        }

        /// <summary>Only `weaponAsset` (e.g. "WP_Spear") equipped, a column of brutes ahead-right, slow motion.</summary>
        public static string Weapon(string weaponAsset, float speed = 0.25f)
        {
            Quiet(out var p, out var fwd, out var right, weapon: true);
            if (Director.Instance != null) Director.Instance.enabled = false;
            var lvl = Object.FindAnyObjectByType<LevelUpController>();
            if (lvl != null) lvl.enabled = false;
            var def = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponDef>("Assets/Data/Weapons/" + weaponAsset + ".asset");
            if (def == null) return "no weapon " + weaponAsset;
            var ws = Player.Main.GetComponent<WeaponSystem>();
            ws.RemoveAll();
            ws.Equip(def);
            var em = EnemyManager.Instance;
            // a column off to the right so the camera sees thrusts and volleys side-on
            for (int k = 0; k < 4; k++) em.Spawn(Def("brute"), p + fwd * 4f + right * (2.5f + k * 2.2f));
            em.Spawn(Def("brute"), p + fwd * 7f + right * 3f);
            TimeController.GameSpeed = speed;
            return "alive " + em.AliveCount;
        }

        public static string Pause() { TimeController.PushPause(); return "paused"; }
        public static string Resume() { TimeController.PopPause(); return "resumed"; }
    }
}
