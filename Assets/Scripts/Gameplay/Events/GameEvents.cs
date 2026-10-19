using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    // ---- payloads (structs: raising is allocation-free) ----
    public struct JumpEvent { public Vector3 Position; public bool FromSlide; public bool AirJump; }
    public struct SlideEvent { public Vector3 Position; public Vector3 Velocity; public float Boost; }
    public struct LandEvent { public Vector3 Position; public float FallSpeed; }
    /// <summary>`Index` is the enemy's EnemyManager slot, valid during the event (hooks apply statuses through it).</summary>
    public struct HitEvent
    {
        public int TargetId; public int Index; public Vector3 Position; public float Damage; public bool Crit; public int SourceId;
        public bool Blocked; public bool Elite; public DamageFlags Flags; public StatusKind Status;
    }
    /// <summary>`Index` is the dead enemy's slot: its statuses can still be read during the event (Pyre spreads Burn).</summary>
    public struct KillEvent { public int TargetId; public int Index; public Vector3 Position; public int EnemyTypeId; public bool Elite; public int SourceId; public float Xp; public float GoldChance; }
    public struct DamagedEvent { public Vector3 Position; public float Damage; public float HealthAfter; public float HealthMax; }
    public struct PickupEvent { public int Kind; public Vector3 Position; public float Amount; }
    public struct LevelUpEvent { public int NewLevel; }
    public struct RunEndEvent { public RunOutcome Outcome; public float Elapsed; }
    public enum DirectorEventKind { Swarm, Elite }
    /// <summary>A line for the HUD announcer (boss beats now; crowd lines in Phase 4). Text must be a constant string.</summary>
    public struct AnnounceEvent { public string Text; public Color Color; public float Seconds; }
    /// <summary>The Director started a swarm or sent an elite (HUD announcer, crowd excitement later).</summary>
    public struct DirectorEvent { public DirectorEventKind Kind; public Vector3 Position; public Color Color; }

    /// <summary>
    /// The typed event bus (CLAUDE.md §4): OnHit, OnKill, OnCrit, OnDamaged, OnLevelUp, OnJump, OnSlide, OnPickup (+ OnLand).
    /// Upgrades hook in by subscribing; everything is cleared when play starts.
    /// </summary>
    public static class GameEvents
    {
        public static readonly EventChannel<HitEvent> OnHit = new EventChannel<HitEvent>();
        public static readonly EventChannel<HitEvent> OnCrit = new EventChannel<HitEvent>();
        public static readonly EventChannel<KillEvent> OnKill = new EventChannel<KillEvent>();
        public static readonly EventChannel<DamagedEvent> OnDamaged = new EventChannel<DamagedEvent>();
        public static readonly EventChannel<LevelUpEvent> OnLevelUp = new EventChannel<LevelUpEvent>();
        public static readonly EventChannel<JumpEvent> OnJump = new EventChannel<JumpEvent>();
        public static readonly EventChannel<SlideEvent> OnSlide = new EventChannel<SlideEvent>();
        public static readonly EventChannel<LandEvent> OnLand = new EventChannel<LandEvent>();
        public static readonly EventChannel<PickupEvent> OnPickup = new EventChannel<PickupEvent>();
        /// <summary>The run is over (death or timer). Raised once, at the moment it happens (before the end screen).</summary>
        public static readonly EventChannel<RunEndEvent> OnRunEnd = new EventChannel<RunEndEvent>();
        public static readonly EventChannel<DirectorEvent> OnDirector = new EventChannel<DirectorEvent>();
        public static readonly EventChannel<AnnounceEvent> OnAnnounce = new EventChannel<AnnounceEvent>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ClearAll()
        {
            OnHit.Clear(); OnCrit.Clear(); OnKill.Clear(); OnDamaged.Clear(); OnLevelUp.Clear();
            OnJump.Clear(); OnSlide.Clear(); OnLand.Clear(); OnPickup.Clear(); OnRunEnd.Clear(); OnDirector.Clear(); OnAnnounce.Clear();
        }
    }
}
