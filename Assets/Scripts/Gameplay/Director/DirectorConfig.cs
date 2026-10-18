using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Pacing numbers for the Director (CLAUDE.md §6.5, §8): how many enemies it keeps alive over the run, how fast it
    /// refills, swarm spikes and the elite cadence. Curves use run seconds on X.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Config/Director", fileName = "DirectorConfig")]
    public sealed class DirectorConfig : ScriptableObject
    {
        [Header("Pressure")]
        [Tooltip("Enemies the Director keeps alive at run time t (s). §8: 300 by minute 5, ~500 at peak (with swarms).")]
        public AnimationCurve targetAlive = new AnimationCurve(
            new Keyframe(0f, 12f), new Keyframe(30f, 30f), new Keyframe(60f, 55f), new Keyframe(120f, 110f),
            new Keyframe(180f, 170f), new Keyframe(240f, 235f), new Keyframe(300f, 300f), new Keyframe(390f, 380f),
            new Keyframe(480f, 460f));
        [Tooltip("Max spawns per second while below the target.")]
        public AnimationCurve spawnPerSecond = new AnimationCurve(
            new Keyframe(0f, 4f), new Keyframe(120f, 10f), new Keyframe(300f, 25f), new Keyframe(480f, 40f));
        [Tooltip("Never more than this alive (perf ceiling, swarms included).")]
        public int hardCap = 500;

        [Header("Placement")]
        [Tooltip("Spawn on a ring this far inside the arena edge (m).")]
        public float ringInset = 1.5f;
        [Tooltip("Never spawn closer than this to the player (m).")]
        public float minPlayerDistance = 10f;
        [Tooltip("Try to spawn outside the camera's view so enemies walk in rather than pop in.")]
        public bool preferOffscreen = true;

        [Header("Swarms (spikes from one direction)")]
        public float firstSwarm = 50f;
        public float swarmEvery = 70f;
        [Tooltip("Enemies per swarm at run time t.")]
        public AnimationCurve swarmSize = new AnimationCurve(new Keyframe(0f, 18f), new Keyframe(240f, 45f), new Keyframe(480f, 80f));
        [Tooltip("Arc the swarm pours in from (deg).")]
        public float swarmArc = 50f;
        [Tooltip("Seconds over which the swarm spawns.")]
        public float swarmDuration = 1.6f;

        [Header("Elites")]
        public float firstElite = 60f;
        public float eliteEvery = 90f;
    }
}
