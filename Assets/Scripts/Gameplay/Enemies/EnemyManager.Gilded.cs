using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Grand Arena's enemies (Phase 5.13, docs/ARENAS.md §4): the Gilded Mercenary pockets the player's gold when it
    /// hits and drops everything it took when it dies; the Sorcerer (Caster) blinks away when the player closes in.
    /// </summary>
    public sealed partial class EnemyManager
    {
        int[] _carried;     // gold a mercenary took from the player
        float[] _blinkCd;   // a caster's blink cooldown

        [Header("Gilded Mercenary / Sorcerer (Phase 5.13)")]
        public Color stolenColorA = new Color(1f, 0.84f, 0.25f), stolenColorB = new Color(1f, 0.55f, 0.15f);
        public Color blinkColorA = new Color(0.55f, 0.35f, 0.95f), blinkColorB = new Color(1f, 0.45f, 0.2f);

        /// <summary>Gold the player lost to mercenaries this run, and gold they gave back by dying (tests, bot log).</summary>
        public int GoldStolen { get; private set; }
        public int GoldReturned { get; private set; }
        public int Blinks { get; private set; }
        public int CarriedGold(int i) => _carried != null && i >= 0 && i < capacity ? _carried[i] : 0;

        void AllocGilded()
        {
            _carried = new int[capacity];
            _blinkCd = new float[capacity];
        }

        void ResetGilded(int i)
        {
            _carried[i] = 0;
            _blinkCd[i] = 1.5f; // not straight out of the gate
        }

        /// <summary>A mercenary's hit landed: it pockets up to stealGold of the run's gold.</summary>
        void Steal(int i, EnemyDef def, Vector3 player)
        {
            var run = RunState.Current;
            if (run == null || def.stealGold <= 0) return;
            int take = Mathf.Min(def.stealGold, run.Gold);
            if (take <= 0 || !run.SpendGold(take)) return;
            _carried[i] += take;
            GoldStolen += take;
            if (deathPop != null) deathPop.Burst(player + Vector3.up * 1.1f, 8, 3.5f, 0.1f, stolenColorA, stolenColorB);
            SfxPlayer.Play(SfxId.Coin, player, 0.7f);
        }

        /// <summary>A mercenary died: what it took falls out of its purse (a few coins).</summary>
        void DropCarried(int i)
        {
            int gold = _carried[i];
            _carried[i] = 0;
            var pickups = PickupSystem.Instance;
            if (gold <= 0 || pickups == null) return;
            GoldReturned += gold;
            int coins = Mathf.Clamp(gold / 6, 1, 5);
            int left = gold;
            for (int k = 0; k < coins; k++)
            {
                int v = k == coins - 1 ? left : gold / coins;
                left -= v;
                float a = (k + GameRandom.Next01(RngStream.Loot) * 0.5f) * Mathf.PI * 2f / coins;
                pickups.Spawn(PickupSystem.Kind.Gold, _pos[i] + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.9f, v);
            }
        }

        /// <summary>A caster the player has closed in on vanishes in a puff and reappears blinkDistance away from them,
        /// inside the arena, out of stone. Runs before its Think, so it moves from where it lands.</summary>
        void TryBlink(int i, EnemyDef def, Vector3 player, float dt)
        {
            _blinkCd[i] -= dt;
            if (def.blinkRange <= 0f || _blinkCd[i] > 0f || _state[i] == AiState.Windup) return;
            Vector3 p = _pos[i];
            float dx = player.x - p.x, dz = player.z - p.z;
            if (dx * dx + dz * dz > def.blinkRange * def.blinkRange || Mathf.Abs(player.y - p.y) > 2.5f) return;
            Vector3 away = new Vector3(-dx, 0f, -dz);
            away = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward;
            Vector3 best = p;
            float bestD = -1f;
            for (int attempt = 0; attempt < 4; attempt++) // a few headings away from the player: the farthest that fits
            {
                Vector3 dir = Quaternion.Euler(0f, GameRandom.Range(RngStream.Enemies, -70f, 70f), 0f) * away;
                Vector3 q = player + dir * GameRandom.Range(RngStream.Enemies, def.blinkDistance.x, def.blinkDistance.y);
                _obstacles?.PushOut(ref q, _radius[i]);
                ClampInside(ref q, _radius[i]);
                float d = (q.x - player.x) * (q.x - player.x) + (q.z - player.z) * (q.z - player.z);
                if (d > bestD) { bestD = d; best = q; }
            }
            if (bestD < 16f) { _blinkCd[i] = 0.5f; return; } // cornered: nowhere to go, try again soon
            if (deathPop != null) deathPop.Burst(p + Vector3.up * 1f, 14, 4f, 0.13f, blinkColorA, blinkColorB);
            best.y = _height != null ? _height.Sample(best.x, best.z) : p.y;
            _pos[i] = best;
            _vel[i] = Vector3.zero;
            _knock[i] = Vector3.zero;
            if (deathPop != null) deathPop.Burst(best + Vector3.up * 1f, 14, 4f, 0.13f, blinkColorA, blinkColorB);
            SfxPlayer.Play(SfxId.Spark, best, 0.8f);
            _blinkCd[i] = def.blinkCooldown;
            _attackCd[i] = Mathf.Max(_attackCd[i], 0.6f); // a breath before it casts again
            Blinks++;
        }

        /// <summary>Slots of the living enemies of type `def` (up to results.Length). O(high water): for a boss's guard.</summary>
        public int FindAlive(EnemyDef def, int[] results)
        {
            int n = 0;
            for (int i = 0; i < _high && n < results.Length; i++)
                if (_active[i] && _view[i] != null && _view[i].Def == def) results[n++] = i;
            return n;
        }
    }
}
