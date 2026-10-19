using System;
using UnityEngine;

namespace Gladiator.Gameplay
{
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

        [Header("Play area")]
        public Vector3 center = Vector3.zero;
        [Tooltip("Radius enemies and the player stay inside (m).")]
        public float radius = 20.5f;
        public Vector3 playerSpawn = new Vector3(0f, 0.1f, -6f);
        public float playerYaw;
        [Tooltip("Director cap on enemies alive in this arena (0 = DirectorConfig.hardCap).")]
        public int maxAlive;
        public Vector3 sunEuler = new Vector3(38f, -32f, 0f);
        [Tooltip("Who comes through the gate when the timer runs out (none = the run just ends).")]
        public BossDef boss;
        [Tooltip("Which gate (index into gates) the boss enters through.")]
        public int bossGate;

        [Header("Content")]
        public Piece[] pieces = Array.Empty<Piece>();
        public Gate[] gates = Array.Empty<Gate>();
        public Zone[] zones = Array.Empty<Zone>();
        public CrowdZone[] crowd = Array.Empty<CrowdZone>();
        public Vector3[] chestAnchors = Array.Empty<Vector3>();
        public Vector3[] altarAnchors = Array.Empty<Vector3>();
    }
}
