using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>Euler offsets (deg, part-local == character space) + root offsets applied on top of the bind pose.</summary>
    public struct PartPose
    {
        public Vector3 Body, Head, ArmL, ArmR, LegL, LegR;
        public float RootY;
        public Vector3 RootScale;

        public static PartPose Identity => new PartPose { RootScale = Vector3.one };

        public void LerpTo(in PartPose t, float k)
        {
            Body = Vector3.LerpUnclamped(Body, t.Body, k);
            Head = Vector3.LerpUnclamped(Head, t.Head, k);
            ArmL = Vector3.LerpUnclamped(ArmL, t.ArmL, k);
            ArmR = Vector3.LerpUnclamped(ArmR, t.ArmR, k);
            LegL = Vector3.LerpUnclamped(LegL, t.LegL, k);
            LegR = Vector3.LerpUnclamped(LegR, t.LegR, k);
            RootY = Mathf.LerpUnclamped(RootY, t.RootY, k);
            RootScale = Vector3.LerpUnclamped(RootScale, t.RootScale, k);
        }

        // Character space: +X right, +Y up, +Z forward. Limbs hang down (-Y), torso/head point up (+Y).
        /// <summary>Limb swung forward by deg (negative = backward).</summary>
        public static Vector3 Swing(float deg) => new Vector3(-deg, 0f, 0f);
        /// <summary>Torso/head leaning forward by deg (negative = back).</summary>
        public static Vector3 Lean(float deg) => new Vector3(deg, 0f, 0f);
    }

    /// <summary>
    /// The rigid parts of a character (Body, Head, ArmL/R, LegL/R under a visual root), with their bind pose.
    /// Missing parts are tolerated (e.g. a beast with no arms). Plain class so EnemyManager can drive hundreds centrally.
    /// </summary>
    public sealed class PartRig
    {
        public readonly Transform Root;
        readonly Transform _body, _head, _armL, _armR, _legL, _legR;
        readonly Quaternion _bBody, _bHead, _bArmL, _bArmR, _bLegL, _bLegR;
        readonly Vector3 _rootPos, _rootScale;

        public PartRig(Transform visualRoot)
        {
            Root = visualRoot;
            _rootPos = visualRoot.localPosition;
            _rootScale = visualRoot.localScale;
            _body = Find(visualRoot, "Body"); _head = Find(visualRoot, "Head");
            _armL = Find(visualRoot, "ArmL"); _armR = Find(visualRoot, "ArmR");
            _legL = Find(visualRoot, "LegL"); _legR = Find(visualRoot, "LegR");
            _bBody = Bind(_body); _bHead = Bind(_head); _bArmL = Bind(_armL); _bArmR = Bind(_armR);
            _bLegL = Bind(_legL); _bLegR = Bind(_legR);
        }

        public bool IsValid => _body != null;

        public void Apply(in PartPose p)
        {
            Set(_body, _bBody, p.Body); Set(_head, _bHead, p.Head);
            Set(_armL, _bArmL, p.ArmL); Set(_armR, _bArmR, p.ArmR);
            Set(_legL, _bLegL, p.LegL); Set(_legR, _bLegR, p.LegR);
            Root.localPosition = _rootPos + new Vector3(0f, p.RootY, 0f);
            Root.localScale = Vector3.Scale(_rootScale, p.RootScale);
        }

        static void Set(Transform t, Quaternion bind, Vector3 euler)
        {
            if (t != null) t.localRotation = bind * Quaternion.Euler(euler);
        }

        static Quaternion Bind(Transform t) => t != null ? t.localRotation : Quaternion.identity;

        static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = Find(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
