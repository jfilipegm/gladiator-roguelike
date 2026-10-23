using System;
using System.Collections.Generic;
using Gladiator.Core;
using TMPro;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// T1 (docs/tracks/T1-playground.md): the Playground's Showroom, every character on show. On Start it lines them up
    /// in labelled rows in its own space (rows along +X, each row behind the last along +Z, every model facing -Z, toward
    /// whoever walks up to the front row): row 1 the gladiators, each with his skins lined up behind him; row 2 the
    /// enemies of ladder tiers I–II; row 3 tiers III–IV; row 4 the bosses at full size (EnemyDef.scale), each spaced by
    /// its own bounds. Every model wears the one shared character material, carries a name label (TextMeshPro in the
    /// project's title font, turned to the camera about the vertical) and has no collider: the player walks through
    /// them. Each model's PartRig plays idle → run → attack swing → hit recoil (with the hit flash), `stageSeconds` each,
    /// the loop phase-offset per model so the rows never move in step. The rig is the model's own root under a slot:
    /// PartRig.Apply puts the root back at its captured local position each tick, so the slot carries the placement.
    /// Centrally ticked, zero allocation per frame (after Start). While it's loaded it also holds the Playground's
    /// switches, god mode and timeless (F1 changes them), and puts them back as they were when the scene goes.
    /// Filled by Gladiator/Setup/Dev/Build Playground (PlaygroundSetup).
    /// </summary>
    public sealed class Showroom : MonoBehaviour, ITickable
    {
        public enum Move : byte { Idle, Run, Attack, Hit }
        public const int Moves = 4;

        /// <summary>The rows, front to back.</summary>
        public const int GladiatorRow = 0, EarlyRow = 1, LateRow = 2, BossRow = 3, Rows = 4;
        public static readonly string[] RowTitles = { "GLADIATORS", "TIERS I–II", "TIERS III–IV", "BOSSES" };

        [Header("On show (filled by Gladiator/Setup/Dev/Build Playground)")]
        public GladiatorDef[] gladiators = Array.Empty<GladiatorDef>();
        [Tooltip("Each stands behind its gladiator (one whose gladiator isn't on show gets a column of its own).")]
        public SkinDef[] skins = Array.Empty<SkinDef>();
        [Tooltip("The EnemyCatalog's enemies, bosses included, in ladder order.")]
        public EnemyDef[] enemies = Array.Empty<EnemyDef>();
        [Tooltip("Per enemy: its ladder tier (1–4, the GDD's: from the first arena whose horde brings it), 0 = a boss. Missing = 1.")]
        public int[] enemyTiers = Array.Empty<int>();
        [Tooltip("For a gladiator with no model of his own (the Nobody wears the player prefab's).")]
        public GameObject defaultModel;
        [Tooltip("For a character with no animation profile (the player prefab's).")]
        public PartAnimProfile defaultProfile;
        [Tooltip("The one shared character material (LookSetup.CharMat).")]
        public Material material;
        [Tooltip("The hit flash (LookSetup.FlashMat; none = no flash).")]
        public Material flashMaterial;
        public TMP_FontAsset font;
        public Material fontMaterial;

        [Header("Layout (m, in the showroom's space)")]
        [Tooltip("Between neighbouring columns of a row.")]
        public float gap = 1.2f;
        [Tooltip("No column is narrower than this (its label needs the room).")]
        public float minWidth = 2.6f;
        [Tooltip("Between a gladiator and the skin behind him, and between skins.")]
        public float skinGap = 0.9f;
        [Tooltip("Between one row's back and the next row's front.")]
        public float rowGap = 4f;
        [Tooltip("A label's bottom over its model's top; a skin's sits higher by depthLift per place back, so a column's labels don't cover each other.")]
        public float labelLift = 0.3f;
        public float depthLift = 0.45f;
        public float labelSize = 2.6f;
        public float bossLabelSize = 3.6f;
        public float titleSize = 7f;
        [Tooltip("A row's title stands this far left of its first column, this high.")]
        public Vector2 titleOffset = new Vector2(2.2f, 1.6f);
        public Color nameColor = new Color(0.94f, 0.9f, 0.84f);
        public Color skinColor = new Color(0.7f, 0.65f, 0.56f);
        public Color titleColor = new Color(0.89f, 0.69f, 0.29f);

        [Header("Motion")]
        [Tooltip("Seconds of each move: idle, run, attack, hit.")]
        public float stageSeconds = 1.5f;
        [Tooltip("How fast poses blend (1/s).")]
        public float poseSharpness = 14f;
        [Tooltip("The attack: wind-up held, the swing, the follow-through held (s); the rest of the stage eases back.")]
        public Vector3 attackTimes = new Vector3(0.35f, 0.4f, 0.3f);
        [Tooltip("The hit stage takes two hits: each tips the body back this far (deg) and fades over recoilSeconds.")]
        public float recoilAngle = 20f;
        public float recoilSeconds = 0.35f;
        public float recoilSquash = 0.12f;
        public float flashSeconds = 0.08f;
        [Tooltip("Idle arm sway and the run's weapon-arm swing are at least this (deg): every model visibly moves.")]
        public float minArmSway = 3f;

        [Header("The Playground's switches (set while this is loaded, put back after)")]
        public bool godMode = true;
        public bool timeless = true;

        struct Spec
        {
            public UnityEngine.Object Def;
            public GameObject Prefab;
            public float Scale;
            public PartAnimProfile Profile;
            public string Text;
            public int Row, Column, Depth;
            public bool Skin;
        }

        int _count;
        UnityEngine.Object[] _defs;
        Transform[] _models, _billboards;
        TextMeshPro[] _labels;
        int[] _rows;
        PartRig[] _rigs;
        PartAnimProfile[] _profiles;
        PartPose[] _poses;
        float[] _offsets;
        Renderer[][] _renderers;
        bool[] _flashing;
        float _time;
        Transform _cam;
        PartAnimProfile _fallback;

        // the switches' owner: a reload enables the new showroom and disables the old one in either order
        static Showroom _owner;
        static bool _prevGod, _prevTimeless;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _owner = null;

        // ---------- what tests and tools read ----------
        /// <summary>Models on show (0 until Start).</summary>
        public int Count => _count;
        /// <summary>Model i's GladiatorDef, SkinDef or EnemyDef.</summary>
        public UnityEngine.Object DefAt(int i) => _defs[i];
        /// <summary>Model i's root (its PartRig's root, under its slot).</summary>
        public Transform ModelAt(int i) => _models[i];
        public Transform LabelAt(int i) => _labels[i] != null ? _labels[i].transform : null;
        public string LabelText(int i) => _labels[i] != null ? _labels[i].text : null;
        /// <summary>GladiatorRow, EarlyRow, LateRow or BossRow.</summary>
        public int RowOf(int i) => _rows[i];

        public int IndexOf(UnityEngine.Object def)
        {
            for (int i = 0; i < _count; i++) if (_defs[i] == def) return i;
            return -1;
        }

        /// <summary>The move model i is in now.</summary>
        public Move MoveOf(int i) => (Move)Stage(Mathf.Repeat(_time + _offsets[i], StageLength * Moves));

        /// <summary>What a character's label says: its display name, else its asset's name.</summary>
        public static string NameOf(UnityEngine.Object def)
        {
            string n = def is GladiatorDef g ? g.displayName : def is SkinDef s ? s.displayName : def is EnemyDef e ? e.displayName : null;
            return string.IsNullOrEmpty(n) && def != null ? def.name : n;
        }

        float StageLength => Mathf.Max(0.1f, stageSeconds);

        int Stage(float u) => Mathf.Min(Moves - 1, (int)(u / StageLength));

        void OnEnable()
        {
            GameLoop.Register(this, TickGroup.Feedback, 40);
            if (_owner == null) { _prevGod = DevSettings.GodMode; _prevTimeless = DevSettings.Timeless; }
            _owner = this;
            if (godMode) DevSettings.GodMode = true;
            if (timeless) DevSettings.Timeless = true;
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            if (_owner != this) return;
            _owner = null;
            DevSettings.GodMode = _prevGod;
            DevSettings.Timeless = _prevTimeless;
        }

        void OnDestroy()
        {
            if (_fallback != null) Destroy(_fallback);
        }

        void Start() => Build();

        // ---------- building the rows (Start only: allocation is fine here) ----------
        List<Spec> Collect()
        {
            var specs = new List<Spec>();
            int col = 0;
            foreach (var g in gladiators)
            {
                if (g == null) continue;
                var model = g.model != null ? g.model : defaultModel;
                var profile = g.animProfile != null ? g.animProfile : defaultProfile;
                specs.Add(new Spec { Def = g, Prefab = model, Scale = 1f, Profile = profile, Text = NameOf(g), Row = GladiatorRow, Column = col });
                int depth = 0;
                foreach (var s in skins)
                    if (s != null && s.gladiator == g)
                        specs.Add(new Spec { Def = s, Prefab = s.model != null ? s.model : model, Scale = 1f, Profile = profile, Text = NameOf(s), Row = GladiatorRow, Column = col, Depth = ++depth, Skin = true });
                col++;
            }
            foreach (var s in skins) // a skin whose gladiator isn't on show: a column of its own
            {
                if (s == null || (s.gladiator != null && Array.IndexOf(gladiators, s.gladiator) >= 0)) continue;
                var profile = s.gladiator != null && s.gladiator.animProfile != null ? s.gladiator.animProfile : defaultProfile;
                specs.Add(new Spec { Def = s, Prefab = s.model, Scale = 1f, Profile = profile, Text = NameOf(s), Row = GladiatorRow, Column = col++, Skin = true });
            }
            var cols = new int[Rows];
            for (int i = 0; i < enemies.Length; i++)
            {
                var e = enemies[i];
                if (e == null) continue;
                int tier = i < enemyTiers.Length ? enemyTiers[i] : 1;
                int row = tier == 0 ? BossRow : tier <= 2 ? EarlyRow : LateRow;
                specs.Add(new Spec { Def = e, Prefab = e.model, Scale = e.scale > 0f ? e.scale : 1f, Profile = e.anim, Text = NameOf(e), Row = row, Column = cols[row]++ });
            }
            return specs;
        }

        void Build()
        {
            var specs = Collect();
            for (int i = specs.Count - 1; i >= 0; i--)
                if (specs[i].Prefab == null)
                {
                    Debug.LogWarning("[Showroom] " + specs[i].Def.name + " has no model: not on show");
                    specs.RemoveAt(i);
                }
            int n = specs.Count;
            _defs = new UnityEngine.Object[n];
            _models = new Transform[n];
            _labels = new TextMeshPro[n];
            _rows = new int[n];
            _rigs = new PartRig[n];
            _profiles = new PartAnimProfile[n];
            _poses = new PartPose[n];
            _offsets = new float[n];
            _renderers = new Renderer[n][];
            _flashing = new bool[n];
            var slots = new Transform[n];
            var half = new Vector2[n]; // half width (x), half depth (z), about the model's pivot
            var top = new float[n];
            var rowRoots = new Transform[Rows];
            var billboards = new List<Transform>(n + Rows);

            for (int i = 0; i < n; i++)
            {
                var s = specs[i];
                if (rowRoots[s.Row] == null)
                {
                    rowRoots[s.Row] = new GameObject("Row" + (s.Row + 1) + " " + RowTitles[s.Row]).transform;
                    rowRoots[s.Row].SetParent(transform, false);
                }
                var slot = new GameObject(s.Text).transform;
                slot.SetParent(rowRoots[s.Row], false);
                slot.localRotation = Quaternion.Euler(0f, 180f, 0f); // models face +Z: turn them to the front
                var model = Instantiate(s.Prefab, slot, false);
                model.name = s.Prefab.name;
                model.transform.localScale = Vector3.one * s.Scale;
                _renderers[i] = Dress(model);
                var b = LocalBounds(slot, model.transform);
                half[i] = new Vector2(Mathf.Max(-b.min.x, b.max.x), Mathf.Max(-b.min.z, b.max.z));
                top[i] = b.max.y;
                slots[i] = slot;
                _defs[i] = s.Def;
                _models[i] = model.transform;
                _rows[i] = s.Row;
                _profiles[i] = s.Profile != null ? s.Profile : Fallback();
                _poses[i] = PartPose.Identity;
            }

            // columns along X (centred), skins stacked back along Z, each row behind the last
            float zFront = 0f;
            var width = new float[n];
            for (int r = 0; r < Rows; r++)
            {
                int cols = 0;
                for (int i = 0; i < n; i++) if (_rows[i] == r) cols = Mathf.Max(cols, specs[i].Column + 1);
                if (cols == 0) continue;
                var colHalf = new float[cols];
                for (int c = 0; c < cols; c++) colHalf[c] = minWidth * 0.5f;
                for (int i = 0; i < n; i++) if (_rows[i] == r) colHalf[specs[i].Column] = Mathf.Max(colHalf[specs[i].Column], half[i].x);
                var colX = new float[cols];
                float x = 0f;
                for (int c = 0; c < cols; c++) { colX[c] = x + colHalf[c]; x += colHalf[c] * 2f + gap; }
                float left = -(x - gap) * 0.5f;
                var colZ = new float[cols];
                for (int c = 0; c < cols; c++) colZ[c] = zFront;
                float back = zFront;
                for (int i = 0; i < n; i++)
                {
                    if (_rows[i] != r) continue;
                    int c = specs[i].Column;
                    float z = colZ[c] + half[i].y;
                    slots[i].localPosition = new Vector3(left + colX[c], 0f, z);
                    colZ[c] = z + half[i].y + skinGap;
                    back = Mathf.Max(back, z + half[i].y);
                    width[i] = colHalf[c] * 2f;
                }
                var title = Label(rowRoots[r], "Title", RowTitles[r], titleSize, titleColor, 8f);
                title.transform.localPosition = new Vector3(left - titleOffset.x, titleOffset.y, (zFront + back) * 0.5f);
                billboards.Add(title.transform);
                zFront = back + rowGap;
            }

            // on the ground, labelled, rigged
            float cycle = StageLength * Moves;
            for (int i = 0; i < n; i++)
            {
                var slot = slots[i];
                var above = slot.position + Vector3.up * 20f;
                if (Physics.Raycast(above, Vector3.down, out var hit, 40f, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore))
                    slot.position = hit.point;
                var s = specs[i];
                bool boss = s.Row == BossRow;
                var label = Label(slot, "Label", s.Text, boss ? bossLabelSize : labelSize, s.Skin ? skinColor : nameColor, width[i]);
                label.transform.localPosition = new Vector3(0f, top[i] + labelLift + s.Depth * depthLift, 0f);
                _labels[i] = label;
                billboards.Add(label.transform);
                _rigs[i] = new PartRig(_models[i]); // after the scale: Apply keeps it (the root's captured local scale)
                _offsets[i] = Mathf.Repeat(i * 0.618034f, 1f) * cycle; // golden-ratio scatter: every move on show at once
            }
            _billboards = billboards.ToArray();
            _count = n;
            Debug.Log($"[Showroom] {n} models on show: {CountIn(GladiatorRow)} gladiators and skins, {CountIn(EarlyRow)} tier I–II, {CountIn(LateRow)} tier III–IV, {CountIn(BossRow)} bosses");
        }

        int CountIn(int row)
        {
            int k = 0;
            for (int i = 0; i < _rows.Length; i++) if (_rows[i] == row) k++;
            return k;
        }

        PartAnimProfile Fallback()
        {
            if (_fallback == null) _fallback = ScriptableObject.CreateInstance<PartAnimProfile>();
            return _fallback;
        }

        /// <summary>The shared material on every slot, the Props layer, shadows on, no colliders (the player walks through).</summary>
        Renderer[] Dress(GameObject model)
        {
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Props;
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                SetMaterial(r, material);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            return renderers;
        }

        static void SetMaterial(Renderer r, Material mat)
        {
            if (mat == null) return;
            var slots = r.sharedMaterials;
            if (slots.Length <= 1) { r.sharedMaterial = mat; return; }
            for (int k = 0; k < slots.Length; k++) slots[k] = mat;
            r.sharedMaterials = slots;
        }

        /// <summary>The model's mesh bounds in `space` (all its parts).</summary>
        static Bounds LocalBounds(Transform space, Transform model)
        {
            var inv = space.worldToLocalMatrix;
            bool any = false;
            var b = new Bounds();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh;
                if (m == null) continue;
                var mb = m.bounds;
                var to = inv * mf.transform.localToWorldMatrix;
                for (int k = 0; k < 8; k++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    var p = to.MultiplyPoint3x4(c);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            if (!any) b = new Bounds(new Vector3(0f, 0.8f, 0f), new Vector3(1f, 1.6f, 1f));
            return b;
        }

        TextMeshPro Label(Transform parent, string objectName, string text, float size, Color color, float rectWidth)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshPro>();
            if (font != null) tmp.font = font;
            if (fontMaterial != null) tmp.fontSharedMaterial = fontMaterial;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = FontStyles.Normal;
            tmp.alignment = TextAlignmentOptions.Bottom;
            tmp.textWrappingMode = TextWrappingModes.Normal; // a long name takes two lines, not the neighbour's place
            tmp.rectTransform.pivot = new Vector2(0.5f, 0f);
            tmp.rectTransform.sizeDelta = new Vector2(Mathf.Max(1f, rectWidth), 1f);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return tmp;
        }

        // ---------- the loop ----------
        public void Tick(float dt, float unscaledDt)
        {
            if (_count == 0) return;
            if (_cam == null)
            {
                var c = Camera.main;
                if (c != null) _cam = c.transform;
            }
            if (dt > 0f)
            {
                _time += dt;
                float cycle = StageLength * Moves;
                float blend = 1f - Mathf.Exp(-poseSharpness * dt);
                for (int i = 0; i < _count; i++)
                {
                    float t = _time + _offsets[i];
                    float u = Mathf.Repeat(t, cycle);
                    int stage = Stage(u);
                    float s = u - stage * StageLength;
                    var p = _profiles[i];
                    var target = PartPose.Identity;
                    float squash = 0f;
                    bool flash = false;
                    switch ((Move)stage)
                    {
                        case Move.Idle: Idle(p, t, s, ref target); break;
                        case Move.Run: Run(p, t, ref target); break;
                        case Move.Attack: Attack(p, s, ref target); break;
                        default: squash = Hit(s, ref target, out flash); break;
                    }
                    _poses[i].LerpTo(target, blend);
                    var applied = _poses[i];
                    // the squash isn't smoothed: it's the snap that sells the hit
                    applied.RootScale = new Vector3(1f + squash, 1f - squash, 1f + squash);
                    _rigs[i].Apply(applied);
                    flash &= flashMaterial != null;
                    if (flash != _flashing[i])
                    {
                        // the first slot only: reading sharedMaterials allocates (models have one slot; Dress set any stray one)
                        _flashing[i] = flash;
                        var rs = _renderers[i];
                        var mat = flash ? flashMaterial : material;
                        for (int k = 0; k < rs.Length; k++) rs[k].sharedMaterial = mat;
                    }
                }
            }
            if (_cam != null && _billboards != null)
            {
                // labels turn to the camera about the vertical: readable from any side
                Vector3 cam = _cam.position;
                for (int k = 0; k < _billboards.Length; k++)
                {
                    var b = _billboards[k];
                    Vector3 d = b.position - cam;
                    d.y = 0f;
                    if (d.sqrMagnitude > 1e-4f) b.rotation = Quaternion.LookRotation(d);
                }
            }
        }

        void Idle(PartAnimProfile p, float t, float s, ref PartPose target)
        {
            float b = Mathf.Sin(t * Mathf.Max(0.5f, p.idleBobHz) * Mathf.PI * 2f);
            target.RootY = b * p.idleBobHeight;
            float sway = Mathf.Max(p.idleArmSway, minArmSway) * (0.5f + 0.5f * b);
            target.ArmL = new Vector3(0f, 0f, -sway);
            target.ArmR = new Vector3(0f, 0f, sway);
            if (p.idleHeadLift != 0f && s >= p.idleHeadDelay) target.Head = PartPose.Lean(-p.idleHeadLift); // Asterion looks up
        }

        /// <summary>Running in place at the profile's full run speed (the in-game cadence and amplitudes).</summary>
        void Run(PartAnimProfile p, float t, ref PartPose target)
        {
            float hz = Mathf.Max(0.1f, p.fullRunSpeed) / Mathf.Max(0.1f, p.strideLength);
            float sn = Mathf.Sin(t * hz * Mathf.PI * 2f);
            target.LegL = PartPose.Swing(sn * p.legSwing);
            target.LegR = PartPose.Swing(-sn * p.legSwing);
            target.ArmL = PartPose.Swing(-sn * p.armSwing);
            target.ArmR = PartPose.Swing(sn * Mathf.Max(p.armSwing * p.weaponArmSwingScale, minArmSway));
            target.Body = PartPose.Lean(p.runLean) + new Vector3(0f, sn * 6f, 0f);
            target.Head = PartPose.Lean(-p.runLean * 0.5f);
            target.RootY = Mathf.Abs(sn) * p.bobHeight;
        }

        /// <summary>One swing of the weapon arm, as the player animator does it (the overhead chop and the two-handed grip
        /// included): wind-up, swing, follow-through, then the rest of the stage eases back to stand.</summary>
        void Attack(PartAnimProfile p, float s, ref PartPose target)
        {
            float wind = attackTimes.x, swing = Mathf.Max(0.05f, attackTimes.y), hold = attackTimes.z;
            if (s >= wind + swing + hold) return;
            float x = s < wind ? 0f : Mathf.Clamp01((s - wind) / swing);
            float k = p.swingCurve != null && p.swingCurve.length > 0 ? p.swingCurve.Evaluate(x) : x;
            float yaw = Mathf.Lerp(p.swingYawStart, p.swingYawEnd, k);
            float raise = Mathf.Sin(k * Mathf.PI) * 0.4f + 0.6f;
            target.ArmR = new Vector3(-p.swingRaise * raise, yaw, 0f);
            if (p.overheadChop)
            {
                yaw = 0f;
                target.ArmR = new Vector3(-Mathf.Lerp(p.chopPitchStart, p.chopPitchEnd, k), 0f, 0f);
                target.Body += new Vector3(Mathf.Lerp(-8f, 22f, k), 0f, 0f);
            }
            if (p.twoHanded) target.ArmL = new Vector3(target.ArmR.x, -target.ArmR.y, -target.ArmR.z);
            target.Body += new Vector3(0f, yaw * p.bodyTwistScale, 0f);
        }

        /// <summary>Two hits in the stage: each tips the body and head back, flings the arms and squashes the model, then
        /// fades. Returns the squash; `flash` while a hit is fresh.</summary>
        float Hit(float s, ref PartPose target, out bool flash)
        {
            float halfStage = StageLength * 0.5f;
            float r = s < halfStage ? s : s - halfStage;
            float k = r < recoilSeconds ? 1f - r / Mathf.Max(0.01f, recoilSeconds) : 0f;
            flash = r < flashSeconds;
            target.Body = PartPose.Lean(-recoilAngle * k);
            target.Head = PartPose.Lean(-recoilAngle * 0.5f * k);
            target.ArmL = PartPose.Swing(-30f * k) + new Vector3(0f, 0f, -25f * k);
            target.ArmR = PartPose.Swing(-30f * k) + new Vector3(0f, 0f, 25f * k);
            return recoilSquash * k;
        }
    }
}
