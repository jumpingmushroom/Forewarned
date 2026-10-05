using System;
using System.Collections.Generic;
using Forewarned.Core;
using Forewarned.Core.Model;
using UnityEngine;
using UnityEngine.Rendering;

namespace Forewarned.UI
{
    /// <summary>
    /// PLAN.md §9/§11.4: during a wind-up, the attack's area outlined on the ground in the level colour with
    /// a faint fill (rings drawn as a band), and a white path arrow at the player's feet pointing the way
    /// out. Boss-anchored shapes follow the live boss; target-anchored ones stay where they will land. The
    /// arrow hides once the player is clear. Ground heights are sampled by raycast, so outlines follow terrain.
    /// </summary>
    internal sealed class GroundMarkers : MonoBehaviour
    {
        private const float LineWidth = 0.15f;
        private const float Lift = 0.12f;

        private sealed class Marker
        {
            public GameObject Root;
            public LineRenderer Line;
            public Mesh Mesh;
            public GameObject FillObject;
        }

        private static GroundMarkers _instance;
        private static Material _material;
        private static bool _shaderMissing;
        private static int _solidMask = -1;

        private readonly Marker[] _markers = new Marker[WarningBoard.Slots];
        private GameObject _arrow;
        private Mesh _arrowMesh;
        private readonly List<Vector3> _verts = new List<Vector3>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<int> _tris = new List<int>();
        private Vector3[] _linePoints = new Vector3[ShapeOutline.Segments + 2];

        public static void Ensure()
        {
            if (_instance != null || Player.m_localPlayer == null || _shaderMissing)
                return;
            if (_material == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    _shaderMissing = true;
                    ForewarnedPlugin.WarnOnce("GroundMarkers", new InvalidOperationException("shader Sprites/Default not found; ground markers off"));
                    return;
                }
                _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            var go = new GameObject("ForewarnedGroundMarkers");
            _instance = go.AddComponent<GroundMarkers>();
            _instance.Build();
        }

        private void Build()
        {
            for (int i = 0; i < _markers.Length; i++)
                _markers[i] = MakeMarker(i);
            _arrow = MakeArrow();
        }

        private Marker MakeMarker(int i)
        {
            var m = new Marker { Root = new GameObject("Marker" + i) };
            m.Root.transform.SetParent(transform, false);
            // LineRenderer defaults to camera-facing alignment; pin it flat to the ground instead
            // (positions stay world-space via useWorldSpace, so the root's rotation only affects
            // the line's local axes, not where its points sit).
            m.Root.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            m.Line = m.Root.AddComponent<LineRenderer>();
            m.Line.useWorldSpace = true;
            m.Line.loop = true;
            m.Line.alignment = LineAlignment.TransformZ;
            m.Line.sharedMaterial = _material;
            m.Line.shadowCastingMode = ShadowCastingMode.Off;
            m.Line.receiveShadows = false;
            m.Line.numCornerVertices = 2;
            m.FillObject = new GameObject("Fill");
            m.FillObject.transform.SetParent(m.Root.transform, false);
            m.Mesh = new Mesh();
            m.Mesh.MarkDynamic();
            m.FillObject.AddComponent<MeshFilter>().sharedMesh = m.Mesh;
            var r = m.FillObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = _material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            m.Root.SetActive(false);
            return m;
        }

        /// <summary>A flat arrow pointing +Z: a dark edge under a white body, ~2.5 m long.</summary>
        private GameObject MakeArrow()
        {
            var go = new GameObject("PathArrow");
            go.transform.SetParent(transform, false);
            var mesh = new Mesh();
            var v = new List<Vector3>();
            var c = new List<Color>();
            var t = new List<int>();
            AddArrow(v, c, t, 1.15f, -0.01f, new Color(0f, 0f, 0f, 0.85f));
            AddArrow(v, c, t, 1f, 0f, Color.white);
            mesh.SetVertices(v);
            mesh.SetColors(c);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            _arrowMesh = mesh;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingOrder = 1; // above the translucent area fills
            go.SetActive(false);
            return go;
        }

        private static void AddArrow(List<Vector3> v, List<Color> c, List<int> t, float k, float y, Color col)
        {
            int b = v.Count;
            // Shaft (two triangles) then head (one), centred on the player.
            v.Add(new Vector3(-0.15f * k, y, -0.6f * k));
            v.Add(new Vector3(0.15f * k, y, -0.6f * k));
            v.Add(new Vector3(0.15f * k, y, 1.0f * k));
            v.Add(new Vector3(-0.15f * k, y, 1.0f * k));
            v.Add(new Vector3(-0.55f * k, y, 1.0f * k));
            v.Add(new Vector3(0.55f * k, y, 1.0f * k));
            v.Add(new Vector3(0f, y, 1.9f * k));
            for (int i = 0; i < 7; i++)
                c.Add(col);
            t.AddRange(new[] { b, b + 3, b + 2, b, b + 2, b + 1, b + 4, b + 6, b + 5 });
        }

        private void OnDestroy()
        {
            foreach (Marker m in _markers)
                if (m != null && m.Mesh != null)
                    Destroy(m.Mesh);
            if (_arrowMesh != null)
                Destroy(_arrowMesh);
        }

        private void LateUpdate()
        {
            try
            {
                Draw();
            }
            catch (Exception e)
            {
                ForewarnedPlugin.WarnOnce("GroundMarkers", e);
                HideAll();
            }
        }

        private void Draw()
        {
            Engine engine = Runtime.Engine;
            Player me = Player.m_localPlayer;
            if (engine == null || me == null || !PluginConfig.Enabled.Value || !PluginConfig.GroundMarkers.Value || Hud.IsUserHidden())
            {
                HideAll();
                return;
            }
            float now = Time.time;
            Vector3 mePos = me.transform.position;
            var meFlat = new Vec2(mePos.x, mePos.z);
            int used = 0;
            bool arrowShown = false;
            foreach (Warning w in engine.Board.Active)
            {
                if (!w.Visual || w.Shape == null || w.Shape.Kind == ShapeKind.None || used >= _markers.Length)
                    continue;
                Vec2 origin, facing, bossPos;
                Pose(w, out origin, out facing, out bossPos);
                Place(_markers[used++], w, origin, facing, mePos.y, now);
                if (!arrowShown && PluginConfig.PathArrow.Value &&
                    AreaTest.Classify(w.Shape, origin, facing, meFlat) != Verdict.Outside)
                {
                    Vec2 dir = SafeDirection.Toward(w.Response, w.Shape, bossPos, facing, origin, meFlat);
                    if (dir.Length > 0f)
                    {
                        ShowArrow(mePos, dir);
                        arrowShown = true;
                    }
                }
            }
            for (int i = used; i < _markers.Length; i++)
                if (_markers[i].Root.activeSelf)
                    _markers[i].Root.SetActive(false);
            if (!arrowShown && _arrow.activeSelf)
                _arrow.SetActive(false);
        }

        /// <summary>Boss-anchored shapes follow the live boss; otherwise the pose saved at the trigger.</summary>
        private static void Pose(Warning w, out Vec2 origin, out Vec2 facing, out Vec2 bossPos)
        {
            bossPos = w.BossPos;
            facing = w.BossFacing;
            TrackedBoss t = BossWatch.FindById(w.BossId);
            if (t != null && t.Character != null)
            {
                Vector3 p = t.Character.transform.position;
                Vector3 f = t.Character.transform.forward;
                bossPos = new Vec2(p.x, p.z);
                var live = new Vec2(f.x, f.z).Normalized;
                if (live.Length > 0f)
                    facing = live;
            }
            origin = w.Shape.Anchor == Anchor.Boss ? bossPos : w.Origin;
        }

        private void Place(Marker m, Warning w, Vec2 origin, Vec2 facing, float refY, float now)
        {
            if (!m.Root.activeSelf)
                m.Root.SetActive(true);
            List<Vec2> pts = ShapeOutline.Points(w.Shape, origin, facing);
            float alpha = WarningBoard.Alpha(w, now);
            Color c = PluginConfig.ColorFor(w.Level);
            bool ring = w.Shape.Kind == ShapeKind.Ring;

            if (_linePoints.Length < pts.Count)
                _linePoints = new Vector3[pts.Count];
            for (int i = 0; i < pts.Count; i++)
                _linePoints[i] = new Vector3(pts[i].X, Ground(pts[i].X, pts[i].Z, refY) + Lift, pts[i].Z);
            m.Line.positionCount = pts.Count;
            m.Line.SetPositions(_linePoints);
            m.Line.widthMultiplier = ring ? Mathf.Max(LineWidth, w.Shape.Width) : LineWidth;
            Color lineColor = c;
            lineColor.a = (ring ? 0.35f : 0.9f) * alpha;
            m.Line.startColor = m.Line.endColor = lineColor;

            if (m.FillObject.activeSelf == ring)
                m.FillObject.SetActive(!ring);
            if (ring)
                return;
            _verts.Clear();
            _colors.Clear();
            _tris.Clear();
            float cx = 0f, cz = 0f;
            foreach (Vec2 p in pts)
            {
                cx += p.X;
                cz += p.Z;
            }
            cx /= pts.Count;
            cz /= pts.Count;
            Color fill = c;
            fill.a = PluginConfig.MarkerFillOpacity.Value * alpha;
            _verts.Add(new Vector3(cx, Ground(cx, cz, refY) + Lift - 0.02f, cz));
            _colors.Add(fill);
            for (int i = 0; i < pts.Count; i++)
            {
                _verts.Add(_linePoints[i] - new Vector3(0f, 0.02f, 0f));
                _colors.Add(fill);
                _tris.Add(0);
                _tris.Add(1 + (i + 1) % pts.Count);
                _tris.Add(1 + i);
            }
            m.Mesh.Clear();
            m.Mesh.SetVertices(_verts);
            m.Mesh.SetColors(_colors);
            m.Mesh.SetTriangles(_tris, 0);
            m.Mesh.RecalculateBounds();
        }

        private void ShowArrow(Vector3 mePos, Vec2 dir)
        {
            if (!_arrow.activeSelf)
                _arrow.SetActive(true);
            _arrow.transform.position = new Vector3(mePos.x, Ground(mePos.x, mePos.z, mePos.y) + Lift + 0.03f, mePos.z);
            _arrow.transform.rotation = Quaternion.LookRotation(new Vector3(dir.X, 0f, dir.Z), Vector3.up);
        }

        /// <summary>Solid ground under (x, z) near the player's height, so outlines sit on terrain and floors
        /// but not on roofs above; the player's height when nothing is hit.</summary>
        private static float Ground(float x, float z, float refY)
        {
            if (_solidMask < 0)
                _solidMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(x, refY + 4f, z), Vector3.down, out hit, 30f, _solidMask, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return refY;
        }

        private void HideAll()
        {
            foreach (Marker m in _markers)
                if (m != null && m.Root.activeSelf)
                    m.Root.SetActive(false);
            if (_arrow != null && _arrow.activeSelf)
                _arrow.SetActive(false);
        }
    }
}
