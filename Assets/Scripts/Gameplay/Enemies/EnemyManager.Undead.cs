using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The dead (R2, docs/ARENAS.md §6): an enemy with `reviveCount` (the Skeletal Legionnaire) doesn't die the first
    /// time, it collapses into a bone pile (no OnKill: no XP, gold, streak or blast) and stands up `reviveDelay` later at
    /// the spot with `reviveHpMul` of its max HP; the death after that is the kill. Three counters end it for good: the
    /// player stomps the pile (walks or slides over it: that's the kill, with its XP), it dies burning (no pile), or it
    /// falls into an open trapdoor (no pile; a pile on a door that opens falls too, <see cref="DropPiles"/>).
    /// Piles are a short list, not dormant enemy slots: weapons don't aim at them and the Director doesn't count them.
    /// Past <see cref="MaxPiles"/> the dead stay down. Drawn instanced with the enemies' material; a bone-coloured disc
    /// fills round each as it reassembles (not orange-red: a pile doesn't hurt you).
    /// </summary>
    public sealed partial class EnemyManager
    {
        public const int MaxPiles = 48;

        struct Pile
        {
            public Vector3 Pos;
            public float Yaw, Scale, T, Age;
            public EnemyDef Def;
            public AffixDef Affix;
            public int Uid;
            public byte Revived;
        }

        [Header("Bone piles (R2)")]
        [Tooltip("The disc that fills round a pile as it reassembles (the stomp reach): bone, never orange-red (a pile doesn't hurt).")]
        public Color pileRingColor = new Color(0.89f, 0.84f, 0.74f, 0.8f);
        public Color boneColorA = new Color(0.89f, 0.84f, 0.74f), boneColorB = new Color(0.6f, 0.54f, 0.45f);
        [Tooltip("A pile scales in over this long (s).")]
        public float pileScaleIn = 0.15f;
        [Tooltip("It shakes for this long before it stands up (s).")]
        public float pileShakeTime = 0.5f;
        [Tooltip("The player's feet must be less than this above a pile to stomp it (m): jumping over one doesn't.")]
        public float stompMaxHeight = 0.5f;

        readonly Pile[] _piles = new Pile[MaxPiles];
        readonly Matrix4x4[] _pileMatrices = new Matrix4x4[MaxPiles];
        RenderParams _pileRp;
        bool _pileRpReady;
        bool _rising;       // Spawn: a pile standing up brings no escorts
        byte[] _revived;    // per slot: how many times this enemy has already stood up

        /// <summary>Bone piles lying on the floor now.</summary>
        public int PileCount { get; private set; }
        public Vector3 GetPilePosition(int k) => _piles[k].Pos;
        /// <summary>Seconds until pile k stands up.</summary>
        public float GetPileTime(int k) => _piles[k].T;
        public EnemyDef GetPileDef(int k) => _piles[k].Def;
        /// <summary>How many times enemy i has already stood up (0 = its first life).</summary>
        public int TimesRevived(int i) => _revived[i];

        /// <summary>Deaths that became piles (tests, bot log).</summary>
        public int PilesMade { get; private set; }
        /// <summary>Piles that stood up again.</summary>
        public int PilesRisen { get; private set; }
        /// <summary>Piles the player scattered (each one a kill).</summary>
        public int PilesStomped { get; private set; }
        /// <summary>Deaths that would have made a pile but it was burning: it stays down.</summary>
        public int PilesBurned { get; private set; }
        /// <summary>Falls into the hypogeum: deaths that would have made a pile, and piles on a door that opened.</summary>
        public int PilesDenied { get; private set; }
        /// <summary>Deaths that would have made a pile but MaxPiles were down already: it stays down.</summary>
        public int PilesOverflow { get; private set; }

        void AllocUndead() => _revived = new byte[capacity];

        /// <summary>Kill: may enemy i collapse into a pile (it still has a rise left)? Counts the reasons it can't.</summary>
        bool CanCollapse(int i, EnemyDef def, int sourceId)
        {
            if (sourceId == FallSourceId) { PilesDenied++; return false; }
            if (def.burnStopsRevive && HasStatus(i, StatusKind.Burn)) { PilesBurned++; return false; }
            if (PileCount >= MaxPiles) { PilesOverflow++; return false; }
            return true;
        }

        /// <summary>Kill, instead of the kill: its slot is already freed as a death; a pile takes its place.</summary>
        void Collapse(int i, EnemyDef def, AffixDef affix)
        {
            Vector3 p = _pos[i];
            _piles[PileCount++] = new Pile
            {
                Pos = p, Yaw = _yaw[i], Scale = _scale[i] * def.scale, T = Mathf.Max(0.05f, def.reviveDelay), Age = 0f,
                Def = def, Affix = affix, Uid = _uid[i], Revived = (byte)Mathf.Min(255, _revived[i] + 1),
            };
            PilesMade++;
            if (deathPop != null) deathPop.Burst(p + Vector3.up * 0.6f, 10, 3f, 0.13f, boneColorA, boneColorB);
            SfxPlayer.Play(SfxId.Splinter, p, 0.45f); // a soft clatter; the stomp is the loud one (and a kill)
        }

        /// <summary>
        /// The piles, after the enemy loop: count down, stomp (the player's feet within stompRadius and less than
        /// stompMaxHeight above it), stand up at 0, then draw.
        /// </summary>
        void TickPiles(float dt, Vector3 player, TelegraphRenderer tele)
        {
            bool havePlayer = _player != null;
            for (int k = 0; k < PileCount; k++)
            {
                ref var pl = ref _piles[k];
                pl.T -= dt;
                pl.Age += dt;
                float reach = pl.Def.stompRadius * pl.Scale;
                float dx = player.x - pl.Pos.x, dz = player.z - pl.Pos.z, dy = player.y - pl.Pos.y;
                if (havePlayer && dx * dx + dz * dz <= reach * reach && dy < stompMaxHeight && dy > -stompMaxHeight)
                {
                    PilesStomped++;
                    if (deathPop != null) deathPop.Burst(pl.Pos + Vector3.up * 0.3f, 16, 4.5f, 0.15f, boneColorA, boneColorB);
                    SfxPlayer.Play(SfxId.Splinter, pl.Pos);
                    EndPile(k, StompSourceId);
                    k--;
                    continue;
                }
                if (pl.T <= 0f)
                {
                    if (Rise(in pl)) { RemovePile(k); k--; }
                    continue; // no free slot (never at the arena caps): it tries again next frame
                }
                if (tele != null)
                    tele.Circle(pl.Pos, reach, 1f - pl.T / Mathf.Max(0.05f, pl.Def.reviveDelay), pileRingColor, TelegraphRenderer.Style.Telegraph, 0.05f);
            }
            DrawPiles();
        }

        /// <summary>A pile stands up: its enemy again (same affix), with reviveHpMul of its max HP.</summary>
        bool Rise(in Pile pl)
        {
            _rising = true;
            int s = Spawn(pl.Def, pl.Pos, pl.Affix);
            _rising = false;
            if (s < 0) return false;
            _maxHp[s] *= Mathf.Max(0.01f, pl.Def.reviveHpMul);
            _hp[s] = _maxHp[s];
            _revived[s] = pl.Revived;
            PilesRisen++;
            if (deathPop != null) deathPop.Burst(pl.Pos + Vector3.up * 0.4f, 12, 3.5f, 0.14f, boneColorA, boneColorB);
            return true;
        }

        /// <summary>Pile k is gone for good, and that's the kill (credited to `sourceId`, with its XP and gold chance).</summary>
        void EndPile(int k, int sourceId)
        {
            var pl = _piles[k];
            RemovePile(k);
            var affix = pl.Affix;
            bool elite = affix != null;
            GameEvents.OnKill.Raise(new KillEvent
            {
                TargetId = pl.Uid, Index = EventSlot(), Position = pl.Pos, EnemyTypeId = pl.Def.RuntimeId, Elite = elite, SourceId = sourceId,
                Xp = pl.Def.xp * (elite ? affix.xpMultiplier : 1f), GoldChance = pl.Def.goldChance,
            });
        }

        /// <summary>
        /// An enemy slot index for a kill with no enemy behind it (a pile): a free slot with its statuses cleared, so
        /// listeners that look the index up (BoonEffects' bursts on death) find no living enemy and no status.
        /// </summary>
        int EventSlot()
        {
            if (_freeCount == 0) return 0; // every slot alive (never at the arena caps)
            int s = _free[_freeCount - 1];
            ClearStatus(s); // Spawn clears it again anyway
            return s;
        }

        void RemovePile(int k)
        {
            _piles[k] = _piles[--PileCount];
            _piles[PileCount] = default; // drop the def references
        }

        void ClearPiles()
        {
            for (int k = 0; k < PileCount; k++) _piles[k] = default;
            PileCount = 0;
        }

        /// <summary>
        /// Piles inside a rectangle fall (the Hypogeum's trapdoors as they open, R2): gone for good, each one a kill
        /// credited to the fall. `centre` is XZ; `yawDeg` turns the rectangle's local X to (cos, −sin); `size` is along
        /// local X and Z. Returns how many fell.
        /// </summary>
        public int DropPiles(Vector2 centre, float yawDeg, Vector2 size)
        {
            float yr = yawDeg * Mathf.Deg2Rad, c = Mathf.Cos(yr), s = Mathf.Sin(yr);
            float hx = size.x * 0.5f, hz = size.y * 0.5f;
            int n = 0;
            for (int k = 0; k < PileCount; k++)
            {
                float dx = _piles[k].Pos.x - centre.x, dz = _piles[k].Pos.z - centre.y;
                float lx = dx * c - dz * s, lz = dx * s + dz * c;
                if (lx < -hx || lx > hx || lz < -hz || lz > hz) continue;
                PilesDenied++;
                EndPile(k, FallSourceId);
                k--;
                n++;
            }
            return n;
        }

        void DrawPiles()
        {
            if (bodyMaterial == null) return;
            if (!_pileRpReady)
            {
                _pileRp = new RenderParams(bodyMaterial)
                {
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, // blob-shadow world: low heaps cast none
                    receiveShadows = true,
                    worldBounds = new Bounds(arenaCenter, Vector3.one * 600f),
                };
                _pileRpReady = true;
            }
            float now = Time.time;
            ulong drawn = 0; // MaxPiles ≤ 64: one bit per pile
            for (int a = 0; a < PileCount; a++)
            {
                if ((drawn & (1UL << a)) != 0) continue;
                var mesh = _piles[a].Def.pileMesh;
                int n = 0;
                for (int b = a; b < PileCount; b++)
                {
                    if ((drawn & (1UL << b)) != 0 || !ReferenceEquals(_piles[b].Def.pileMesh, mesh)) continue;
                    drawn |= 1UL << b;
                    _pileMatrices[n++] = PileMatrix(in _piles[b], now);
                }
                if (mesh != null) Graphics.RenderMeshInstanced(_pileRp, mesh, 0, _pileMatrices, n);
            }
        }

        /// <summary>A pile's transform: scales in over pileScaleIn, shakes in its last pileShakeTime.</summary>
        Matrix4x4 PileMatrix(in Pile pl, float now)
        {
            float g = pileScaleIn > 0f ? Mathf.Clamp01(pl.Age / pileScaleIn) : 1f;
            g = 1f - (1f - g) * (1f - g);
            Vector3 pos = pl.Pos;
            float yaw = pl.Yaw, sy = 1f;
            if (pl.T < pileShakeTime && pileShakeTime > 0f)
            {
                float k = 1f - Mathf.Max(0f, pl.T) / pileShakeTime;
                float w = Mathf.Sin(now * 55f + pl.Uid * 1.7f) * k;
                pos.x += w * 0.04f;
                yaw += w * 8f;
                sy = 1f + 0.15f * k; // the bones gather themselves
            }
            float s = pl.Scale * g;
            return Matrix4x4.TRS(pos, Quaternion.Euler(0f, yaw, 0f), new Vector3(s, s * sy, s));
        }
    }
}
