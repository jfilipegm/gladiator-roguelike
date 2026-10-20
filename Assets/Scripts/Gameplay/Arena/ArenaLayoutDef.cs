using System;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>The secrets an arena hides (Phase 5.6, docs/UNLOCKS.md).</summary>
    [Flags]
    public enum ArenaSecrets { None = 0, RattlingCrate = 1, GoldenRat = 2, Bookmaker = 4 }

    /// <summary>How a placed piece takes part in the arena (CLAUDE.md §7.3).</summary>
    public enum PieceRole
    {
        /// <summary>Ground enemies walk on (floor, ramps, terraces): feeds the baked height grid, mesh collider.</summary>
        Walkable,
        /// <summary>Big static geometry (walls, gates): baked obstacle, mesh collider, the camera avoids it.</summary>
        Solid,
        /// <summary>Small props (cages, crates): baked obstacle, one box collider, the camera ignores it.</summary>
        Prop,
        /// <summary>Visual only (decals, palisade tops, stands outside the pit): no collider.</summary>
        Decor,
    }

    /// <summary>
    /// An arena as data (CLAUDE.md §7.3, M3.2): every piece placement, the gates enemies pour through, mud and fire
    /// zones, crowd zones, chest/altar anchors, the player spawn and the pacing cap. `ArenaBuilder` (Editor) turns it
    /// into a scene with one menu item and bakes the height grid and obstacles; runtime systems read the rest.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Arena Layout", fileName = "AL_")]
    public sealed class ArenaLayoutDef : ScriptableObject
    {
        [Serializable]
        public struct Piece
        {
            [Tooltip("Model under Assets/Art/Models/ without .fbx, e.g. \"Environment/SM_PitWall_4m\".")]
            public string model;
            public PieceRole role;
            public Vector3 position;
            public float yaw;
            public Vector3 scale;
        }

        [Serializable]
        public struct Gate
        {
            [Tooltip("Where the gate model stands (its front faces into the arena).")]
            public Vector3 position;
            public float yaw;
            [Tooltip("This gate's model under Assets/Art/Models/ (empty = the layout's gateModel).")]
            public string model;
        }

        public enum ZoneKind { Mud, Fire }

        [Serializable]
        public struct Zone
        {
            public ZoneKind kind;
            public Vector3 position;
            public float radius;
        }

        [Serializable]
        public struct CrowdZone
        {
            [Tooltip("Centre of the standing area (y = the surface they stand on).")]
            public Vector3 center;
            [Tooltip("Width along the zone's local X, depth along its local Z (m).")]
            public Vector2 size;
            public float yaw;
            public int count;
        }

        public string id = "slave_pit";
        public string displayName = "The Slave Pit";
        [Tooltip("0 = the Slave Pit, 1 = the Provincial Pit… (Renown bonus, deepest arena, ReachArena unlocks).")]
        public int tier;

        [Header("Difficulty")]
        [Tooltip("The Director's enemy table here (empty = the Slave Pit's).")]
        public SpawnTableDef spawnTable;
        [Tooltip("Every non-boss enemy spawns with this much HP.")]
        public float enemyHpMul = 1f;
        [Tooltip("Every hit on the player is multiplied by this (enemies, hazards, the boss).")]
        public float playerDamageTakenMul = 1f;

        [Header("Play area")]
        public Vector3 center = Vector3.zero;
        [Tooltip("Radius enemies stay inside (m), along X.")]
        public float radius = 20.5f;
        [Tooltip("Z extent ÷ X extent: 1 = a round arena, below 1 an oval (the playtest Slave Pit).")]
        public float aspect = 1f;
        [Tooltip("The play area's outline (world XZ, in order around it), where enemies stay: any polygon whose whole edge the centre can see (rectangle, octagon, D-shape...). Empty: the ellipse of radius × aspect.")]
        public Vector2[] outline = Array.Empty<Vector2>();
        [Tooltip("Invisible walls (x, z, yaw, width) that keep the player in even over a low fence with air jumps; only the player collides with them.")]
        public Vector4[] boundary = Array.Empty<Vector4>();
        [Tooltip("Linear fog start / end (m); zero = the look's default. Big arenas push it out.")]
        public Vector2 fog;
        public Vector3 playerSpawn = new Vector3(0f, 0.1f, -6f);
        public float playerYaw;
        [Tooltip("Director cap on enemies alive in this arena (0 = DirectorConfig.hardCap).")]
        public int maxAlive;
        [Tooltip("Ring spawns come from the part of the edge within this distance of the player (m; 0 = anywhere). On a big floor the horde walks in from the near side, not from 100 m away.")]
        public float spawnMaxDistance;
        public Vector3 sunEuler = new Vector3(38f, -32f, 0f);
        [Tooltip("Who comes through the gate when the timer runs out (none = the run just ends).")]
        public BossDef boss;
        [Tooltip("Which gate (index into gates) the boss enters through.")]
        public int bossGate;

        [Header("Content")]
        public Piece[] pieces = Array.Empty<Piece>();
        [Tooltip("Gate model under Assets/Art/Models/ (needs hinged DoorL / DoorR children).")]
        public string gateModel = "Environment/SM_PitGate";
        public Gate[] gates = Array.Empty<Gate>();
        public Zone[] zones = Array.Empty<Zone>();
        public CrowdZone[] crowd = Array.Empty<CrowdZone>();
        public Vector3[] chestAnchors = Array.Empty<Vector3>();
        public Vector3[] altarAnchors = Array.Empty<Vector3>();
        [Tooltip("Where a secret can hide: the rattling crate picks one per run, the golden rat's holes, the bookmaker's booth (empty = no secret here).")]
        public Vector3[] secretSpots = Array.Empty<Vector3>();
        [Tooltip("Which secrets hide in this arena (Phase 5.6); each uses secretSpots.")]
        public ArenaSecrets secrets = ArenaSecrets.RattlingCrate;

        [Header("Breakables and traps (Phase 5.4)")]
        [Tooltip("Barricades (x, y, z, yaw): obstacles a boss charge wrecks for the rest of the run.")]
        public Vector4[] barricades = Array.Empty<Vector4>();
        [Tooltip("Braziers a boss charge topples into burning ground (base position).")]
        public Vector3[] braziers = Array.Empty<Vector3>();
        [Tooltip("Spike grate centres (y = the floor): they cycle and hurt everyone on them.")]
        public Vector3[] spikeTraps = Array.Empty<Vector3>();

        [Header("Beast cages and stone (Phase 5.8)")]
        [Tooltip("Cage gates in the walls (x, y, z, yaw; the model's +Z faces the arena): BeastCages releases packs through them.")]
        public Vector4[] cages = Array.Empty<Vector4>();
        [Tooltip("What comes out of the cages.")]
        public EnemyDef cageBeast;
        [Tooltip("Seconds into the run before the first cage opens, then between cages.")]
        public float cageFirst = 75f;
        public float cageEvery = 40f;
        [Tooltip("Boars per pack at the start of the run and at the end of the timer.")]
        public Vector2Int cagePack = new Vector2Int(3, 6);
        [Tooltip("Invisible stone (x, z, yaw, width; yaw faces the arena): Environment-layer walls behind the line, for a wall that is decor only (one big mesh would bake as one huge obstacle). The camera, the player and the nav bake see them.")]
        public Vector4[] solidWalls = Array.Empty<Vector4>();
        public float solidWallHeight = 8.4f;
        [Tooltip("Seconds between elites here (0 = DirectorConfig.eliteEvery).")]
        public float eliteEvery;

        /// <summary>The play area's floor (m²): the outline's, else the ellipse's. Each arena of the ladder must be bigger
        /// than the one before it (docs/GDD.md).</summary>
        public float PlayArea()
        {
            if (outline == null || outline.Length < 3) return Mathf.PI * radius * radius * (aspect > 0f ? aspect : 1f);
            float twice = 0f; // shoelace
            for (int i = 0, j = outline.Length - 1; i < outline.Length; j = i++)
                twice += outline[j].x * outline[i].y - outline[i].x * outline[j].y;
            return Mathf.Abs(twice) * 0.5f;
        }
    }
}
