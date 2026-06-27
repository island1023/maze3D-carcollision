using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;

/// <summary>
/// 墙面：LineRenderer 世界坐标（稳定可见）。
/// 车身：贴面网格条带，局部坐标（随车）。
/// </summary>
public class WallScratchTracker : MonoBehaviour
{
    [Header("--- 划痕颜色 ---")]
    public Color scratchColor = new Color(0.12f, 0.11f, 0.1f, 1f);

    [Header("--- 线条粗细 ---")]
    public float scratchWidth = 0.014f;

    [Header("--- 表面类型 ---")]
    [Tooltip("勾选=车身；不勾选=墙面")]
    public bool followSurface = false;

    [Header("--- 生成条件 ---")]
    public float minSpeedToScratch = 0.3f;
    public float minPointDistance = 0.025f;
    public float sessionBreakTime = 0.6f;
    public float surfaceOffset = 0.005f;
    public float maxVerticalJump = 0.06f;

    [Header("--- 性能限制 ---")]
    public int maxScratchSessions = 20;
    public int maxPointsPerSession = 400;

    // 墙面
    private readonly List<LineSession> lineSessions = new List<LineSession>();
    private LineSession activeLine;
    private Vector3 lastLinePoint;
    private bool hasLastLinePoint;

    // 车身
    private readonly List<RibbonSession> ribbonSessions = new List<RibbonSession>();
    private RibbonSession activeRibbon;
    private Vector3 lastRibbonPoint;
    private Vector3 lastRibbonNormal;
    private bool hasLastRibbonPoint;

    private float lastScratchTime = -999f;
    private Transform scratchRoot;
    private bool useWorldSpace;

    private static Transform globalWallRoot;

    private Transform bodyAnchorTransform;

    public void SetBodyAnchor(Transform anchor)
    {
        if (!followSurface || anchor == null)
            return;

        if (bodyAnchorTransform == anchor && scratchRoot != null)
            return;

        bodyAnchorTransform = anchor;
        ClearScratchGeometry();
    }

    public void ConfigureAsVehicle()
    {
        followSurface = true;
        scratchWidth = 0.01f;
        surfaceOffset = 0.005f;
        minPointDistance = 0.022f;
        minSpeedToScratch = 0.3f;
        scratchColor = new Color(0.09f, 0.085f, 0.08f, 1f);
    }

    public void AddScratchPoint(
        Vector3 worldPoint,
        Vector3 surfaceOutwardNormal,
        Vector3 slideDirection,
        float tangentSpeed)
    {
        if (tangentSpeed < minSpeedToScratch)
            return;

        Vector3 normal = surfaceOutwardNormal.sqrMagnitude > 0.0001f
            ? surfaceOutwardNormal.normalized
            : Vector3.up;

        Vector3 tangent = Vector3.ProjectOnPlane(slideDirection, normal);
        if (tangent.sqrMagnitude < 0.0001f)
            tangent = Vector3.Cross(normal, Vector3.up);
        tangent.Normalize();

        Vector3 surfacePoint = worldPoint + normal * surfaceOffset;

        if (followSurface)
            AddBodyPoint(surfacePoint, normal, tangent, tangentSpeed);
        else
            AddWallPoint(surfacePoint, normal, tangent, tangentSpeed);
    }

    // ── 墙面 LineRenderer ──────────────────────────────

    private void AddWallPoint(Vector3 point, Vector3 normal, Vector3 tangent, float speed)
    {
        EnsureScratchRoot();

        if (hasLastLinePoint && Mathf.Abs(point.y - lastLinePoint.y) > maxVerticalJump)
            point = new Vector3(point.x, lastLinePoint.y, point.z);

        bool needNew = activeLine == null
            || Time.time - lastScratchTime > sessionBreakTime
            || (activeLine != null && Vector3.Dot(activeLine.surfaceNormal, normal) < 0.8f);

        if (needNew)
            StartLineSession(normal, tangent, speed);

        lastScratchTime = Time.time;

        if (hasLastLinePoint)
        {
            if ((point - lastLinePoint).sqrMagnitude < minPointDistance * minPointDistance)
                return;
        }

        AppendLinePoint(point, tangent);
        lastLinePoint = point;
        hasLastLinePoint = true;
    }

    private void StartLineSession(Vector3 normal, Vector3 tangent, float speed)
    {
        EnsureScratchRoot();
        TrimLines();

        GameObject obj = new GameObject($"WallLine_{lineSessions.Count}");
        obj.transform.SetParent(scratchRoot, false);

        float width = scratchWidth * Mathf.Lerp(0.85f, 1.2f, Mathf.Clamp01(speed / 10f));

        LineRenderer lr = obj.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Stretch;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 4;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.startColor = scratchColor;
        lr.endColor = scratchColor;
        lr.material = CreateScratchMaterial(scratchColor);
        lr.positionCount = 0;

        activeLine = new LineSession
        {
            root = obj.transform,
            line = lr,
            points = new List<Vector3>(64),
            surfaceNormal = normal,
            slideDirection = tangent
        };

        lineSessions.Add(activeLine);
        hasLastLinePoint = false;
    }

    private void AppendLinePoint(Vector3 worldPoint, Vector3 tangent)
    {
        if (activeLine == null || activeLine.points.Count >= maxPointsPerSession)
            return;

        List<Vector3> pts = activeLine.points;
        LineRenderer lr = activeLine.line;

        if (pts.Count == 0)
        {
            Vector3 tail = worldPoint + tangent * 0.02f;
            pts.Add(worldPoint);
            pts.Add(tail);
            lr.positionCount = 2;
            lr.SetPosition(0, worldPoint);
            lr.SetPosition(1, tail);
            return;
        }

        pts.Add(worldPoint);
        int n = pts.Count;
        lr.positionCount = n;
        for (int i = 0; i < n; i++)
            lr.SetPosition(i, pts[i]);
    }

    private void TrimLines()
    {
        while (lineSessions.Count >= maxScratchSessions)
        {
            LineSession old = lineSessions[0];
            lineSessions.RemoveAt(0);
            if (old.root != null)
                Destroy(old.root.gameObject);
        }
    }

    // ── 车身 Mesh（沿 session 锁定方向，贴曲面） ──────────────────────────────

    private void AddBodyPoint(Vector3 point, Vector3 normal, Vector3 tangent, float speed)
    {
        EnsureScratchRoot();

        bool needNew = activeRibbon == null
            || Time.time - lastScratchTime > sessionBreakTime;

        if (needNew)
            StartRibbonSession(normal, tangent);

        lastScratchTime = Time.time;

        if (!hasLastRibbonPoint)
        {
            lastRibbonPoint = point;
            lastRibbonNormal = normal;
            hasLastRibbonPoint = true;
            return;
        }

        Vector3 slideDir = activeRibbon.slideDirection;
        Vector3 delta = point - lastRibbonPoint;
        float along = Vector3.Dot(delta, slideDir);
        if (along < minPointDistance)
            return;

        // 强制沿锁定滑动方向，防止扇形
        Vector3 end = lastRibbonPoint + slideDir * along;
        Vector3 startNormal = lastRibbonNormal;
        Vector3 endNormal = normal;

        AppendRibbonSegment(lastRibbonPoint, startNormal, end, endNormal, speed);
        lastRibbonPoint = end;
        lastRibbonNormal = endNormal;
    }

    private void StartRibbonSession(Vector3 normal, Vector3 tangent)
    {
        EnsureScratchRoot();
        TrimRibbons();

        Vector3 slideDir = Vector3.ProjectOnPlane(tangent, normal);
        if (slideDir.sqrMagnitude < 0.01f)
            slideDir = Vector3.Cross(normal, Vector3.up);
        slideDir.Normalize();

        GameObject obj = new GameObject($"BodyRibbon_{ribbonSessions.Count}");
        obj.transform.SetParent(scratchRoot, false);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;

        Mesh mesh = new Mesh { name = "BodyScratch" };
        mesh.MarkDynamic();
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();
        mf.sharedMesh = mesh;
        mr.sharedMaterial = CreateScratchMaterial(scratchColor);
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;

        activeRibbon = new RibbonSession
        {
            root = obj.transform,
            mesh = mesh,
            vertices = new List<Vector3>(128),
            uvs = new List<Vector2>(128),
            triangles = new List<int>(192),
            segmentCount = 0,
            surfaceNormal = normal,
            slideDirection = slideDir
        };

        ribbonSessions.Add(activeRibbon);
        hasLastRibbonPoint = false;
    }

    private void AppendRibbonSegment(Vector3 wA, Vector3 nA, Vector3 wB, Vector3 nB, float speed)
    {
        if (activeRibbon == null || activeRibbon.segmentCount >= maxPointsPerSession)
            return;

        Vector3 path = wB - wA;
        float len = path.magnitude;
        if (len < 0.0005f)
            return;
        path /= len;

        float halfW = scratchWidth * 0.5f;
        Vector3 wA_axis = CrossWidth(nA, path) * halfW;
        Vector3 wB_axis = CrossWidth(nB, path) * halfW;

        int i = activeRibbon.vertices.Count;
        activeRibbon.vertices.Add(scratchRoot.InverseTransformPoint(wA - wA_axis));
        activeRibbon.vertices.Add(scratchRoot.InverseTransformPoint(wA + wA_axis));
        activeRibbon.vertices.Add(scratchRoot.InverseTransformPoint(wB + wB_axis));
        activeRibbon.vertices.Add(scratchRoot.InverseTransformPoint(wB - wB_axis));

        float u = activeRibbon.segmentCount * 0.5f;
        activeRibbon.uvs.Add(new Vector2(u, 0f));
        activeRibbon.uvs.Add(new Vector2(u, 1f));
        activeRibbon.uvs.Add(new Vector2(u + 1f, 1f));
        activeRibbon.uvs.Add(new Vector2(u + 1f, 0f));

        activeRibbon.triangles.Add(i);
        activeRibbon.triangles.Add(i + 2);
        activeRibbon.triangles.Add(i + 1);
        activeRibbon.triangles.Add(i);
        activeRibbon.triangles.Add(i + 3);
        activeRibbon.triangles.Add(i + 2);

        activeRibbon.segmentCount++;

        Mesh m = activeRibbon.mesh;
        m.Clear();
        m.SetVertices(activeRibbon.vertices);
        m.SetUVs(0, activeRibbon.uvs);
        m.SetTriangles(activeRibbon.triangles, 0);
        m.RecalculateBounds();
        m.RecalculateNormals();
    }

    private static Vector3 CrossWidth(Vector3 normal, Vector3 pathDir)
    {
        Vector3 w = Vector3.Cross(normal, pathDir);
        if (w.sqrMagnitude < 0.0001f)
            w = Vector3.Cross(normal, Vector3.up);
        return w.normalized;
    }

    private void TrimRibbons()
    {
        while (ribbonSessions.Count >= maxScratchSessions)
        {
            RibbonSession old = ribbonSessions[0];
            ribbonSessions.RemoveAt(0);
            if (old.root != null)
                Destroy(old.root.gameObject);
        }
    }

    // ── 共用 ──────────────────────────────

    private void EnsureScratchRoot()
    {
        bool wantWorld = !followSurface;

        if (scratchRoot != null && useWorldSpace != wantWorld)
        {
            ClearScratches();
        }

        if (scratchRoot != null)
            return;

        useWorldSpace = wantWorld;

        if (useWorldSpace)
        {
            if (globalWallRoot == null)
                globalWallRoot = new GameObject("GlobalWallScratches").transform;

            GameObject root = new GameObject($"Wall_{GetInstanceID()}");
            root.transform.SetParent(globalWallRoot, false);
            scratchRoot = root.transform;
        }
        else
        {
            Transform parent = bodyAnchorTransform != null ? bodyAnchorTransform : transform;
            GameObject root = new GameObject("BodyScratches");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            scratchRoot = root.transform;
        }
    }

    private static Material CreateScratchMaterial(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended");

        Material mat = new Material(shader);
        mat.color = color;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Cull"))
            mat.SetInt("_Cull", (int)CullMode.Off);
        mat.renderQueue = 3100;
        return mat;
    }

    public void ClearScratches()
    {
        ClearScratchGeometry();
        bodyAnchorTransform = null;
    }

    private void ClearScratchGeometry()
    {
        foreach (LineSession s in lineSessions)
            if (s.root != null) Destroy(s.root.gameObject);
        foreach (RibbonSession s in ribbonSessions)
            if (s.root != null) Destroy(s.root.gameObject);

        lineSessions.Clear();
        ribbonSessions.Clear();
        activeLine = null;
        activeRibbon = null;
        hasLastLinePoint = false;
        hasLastRibbonPoint = false;

        if (scratchRoot != null)
        {
            Destroy(scratchRoot.gameObject);
            scratchRoot = null;
        }
    }

    private class LineSession
    {
        public Transform root;
        public LineRenderer line;
        public List<Vector3> points;
        public Vector3 surfaceNormal;
        public Vector3 slideDirection;
    }

    private class RibbonSession
    {
        public Transform root;
        public Mesh mesh;
        public List<Vector3> vertices;
        public List<Vector2> uvs;
        public List<int> triangles;
        public int segmentCount;
        public Vector3 surfaceNormal;
        public Vector3 slideDirection;
    }
}
