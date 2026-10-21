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
    public struct DamagedEvent { public Vector3 Position; public float Damage; public float HealthAfter; public float HealthMax; public bool Status; }
    public struct PickupEvent { public int Kind; public Vector3 Position; public float Amount; }
    public struct LevelUpEvent { public int NewLevel; }
    public struct RunEndEvent { public RunOutcome Outcome; public float Elapsed; }
    public enum DirectorEventKind { Swarm, Elite }
    /// <summary>What an announcer line is about (Favor listens for streaks, close calls and the boss's beats).</summary>
    public enum AnnounceKind { Line, Streak, CloseCall, Roar, BossEnters, BossPhase, BossGuards, BossFalls, Gift, Whim }
    /// <summary>A line for the HUD announcer. Text must be a constant string. Tier = the streak rung for Streak lines.</summary>
    public struct AnnounceEvent { public string Text; public Color Color; public float Seconds; public AnnounceKind Kind; public int Tier; }
    public enum FavorEventKind { GiftEarned, GiftThrown, GiftCollected, WhimFired, ChallengeWon, ChallengeFailed }
    /// <summary>The crowd's Favor (Phase 5.1): gifts earned, thrown and collected; the Arena Master's whims.</summary>
    public struct FavorEvent { public FavorEventKind Kind; public GiftKind Gift; public WhimKind Whim; public Vector3 Position; }
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
        public static readonly EventChannel<FavorEvent> OnFavor = new EventChannel<FavorEvent>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ClearAll()
        {
            OnHit.Clear(); OnCrit.Clear(); OnKill.Clear(); OnDamaged.Clear(); OnLevelUp.Clear();
            OnJump.Clear(); OnSlide.Clear(); OnLand.Clear(); OnPickup.Clear(); OnRunEnd.Clear(); OnDirector.Clear(); OnAnnounce.Clear(); OnFavor.Clear();
        }
    }
}
