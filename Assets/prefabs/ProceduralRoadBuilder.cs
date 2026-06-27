using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using Unity.AI.Navigation;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

public class ProceduralRoadBuilder : MonoBehaviour
{
    [Header("核心依赖")]
    public SplineContainer splineContainer;

    [Header("--- 直线覆盖设置 (指定场景物理点) ---")]
    [Tooltip("勾选后，将在下方两个物体之间的路段强行拉直（硬拐角）")]
    public bool enableStraightOverride = false;

    public Transform straightStartPoint;
    public Transform straightEndPoint;

    // 内部缓存计算出的实际 t 值
    private float cachedStartT;
    private float cachedEndT;

    [Header("道路设置 (Road - Plane)")]
    public GameObject roadPrefab;
    public float roadSpacing = 9.98f;
    public float roadWidth = 24f;

    [Header("车道白线 (LineMarks)")]
    public GameObject laneLinePrefab;
    public float laneLineSpacing = 2.98f;
    public float laneLineHeightOffset = 0.005f;

    [Header("护栏设置 (Guardwall - Cube)")]
    public GameObject guardwallPrefab;
    public float guardwallSpacing = 2.98f;
    public float guardwallHeightOffset = 1.5f;
    public float guardwallDistance = 12.0f;

    [Header("AI 导航关联")]
    public NavMeshSurface navMeshSurface;

    [ContextMenu("Generate Road, Lines And Guardrails (一键生成)")]
    public void Build()
    {
        if (splineContainer == null) splineContainer = GetComponent<SplineContainer>();
        if (splineContainer == null || splineContainer.Spline == null) return;

        // 计算两点在样条线上的实际进度 (t)
        if (enableStraightOverride && straightStartPoint != null && straightEndPoint != null)
        {
            float3 localStart = splineContainer.transform.InverseTransformPoint(straightStartPoint.position);
            float3 localEnd = splineContainer.transform.InverseTransformPoint(straightEndPoint.position);

            SplineUtility.GetNearestPoint(splineContainer.Spline, localStart, out float3 nearestStart, out cachedStartT);
            SplineUtility.GetNearestPoint(splineContainer.Spline, localEnd, out float3 nearestEnd, out cachedEndT);

            if (cachedStartT > cachedEndT)
            {
                float temp = cachedStartT;
                cachedStartT = cachedEndT;
                cachedEndT = temp;
            }
        }
        else if (enableStraightOverride)
        {
            Debug.LogWarning("开启了拉直，但未设置起始点或终点！");
            enableStraightOverride = false;
        }

        GenerateRoads();
        if (laneLinePrefab != null) GenerateLineMarkings();
        GenerateGuardrails();

        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    // ==========================================
    // ⭐ 硬核直线覆盖逻辑 (大角度拐角，无平滑过渡)
    // ==========================================
    private void ApplyStraightOverride(Spline spline, float t, ref float3 pos, ref float3 tangent, ref float3 up)
    {
        // 只有开启了功能且严格处于两个点之间时，才触发绝对拉直
        if (!enableStraightOverride || t < cachedStartT || t > cachedEndT) return;

        // 提取拉直起点的坐标和法线
        spline.Evaluate(cachedStartT, out float3 posStart, out _, out float3 upStart);
        // 提取拉直终点的坐标和法线
        spline.Evaluate(cachedEndT, out float3 posEnd, out _, out float3 upEnd);

        // 计算当前 t 在拉直区间内的相对进度 (0 ~ 1)
        float localT = (t - cachedStartT) / (cachedEndT - cachedStartT);

        // 强行使用两点一线覆盖原本的曲线坐标
        pos = math.lerp(posStart, posEnd, localT);

        // 强行修正切线方向（使其笔直指向终点，彻底消除切线扭曲）
        float3 dir = posEnd - posStart;
        tangent = math.lengthsq(dir) > 0.001f ? math.normalize(dir) : new float3(0, 0, 1);

        // 线性过渡法线
        up = math.normalize(math.lerp(upStart, upEnd, localT));
    }

    private void GenerateRoads()
    {
        if (roadPrefab == null) return;
        Transform roadParent = GetOrCreateHolder("RoadsHolder");

        Spline spline = splineContainer.Spline;
        float totalLength = spline.GetLength();
        int count = Mathf.FloorToInt(totalLength / roadSpacing);

        for (int i = 0; i <= count; i++)
        {
            float t = (i * roadSpacing) / totalLength;
            if (t > 1f) t = 1f;

            spline.Evaluate(t, out float3 pos, out float3 tangent, out float3 up);
            ApplyStraightOverride(spline, t, ref pos, ref tangent, ref up);

            float smoothedHeight = GetSmoothedHeight(spline, t);

            // ⭐ 核心优化：微小阶梯化处理，专治高密度层叠时的 Z-Fighting 闪烁
            // 每一块路面比上一块高出 1 毫米 (0.001f)，物理引擎（车轮）完全感觉不到，但画面不会再闪烁了
            float microOffset = i * 0.001f;

            Vector3 position = new Vector3(pos.x, smoothedHeight + microOffset, pos.z);

            Quaternion rotation = Quaternion.LookRotation(tangent, up);
            Instantiate(roadPrefab, position, rotation, roadParent);
        }
    }

    private void GenerateLineMarkings()
    {
        Transform lineParent = GetOrCreateHolder("LinesHolder");

        Spline spline = splineContainer.Spline;
        float totalLength = spline.GetLength();
        int count = Mathf.FloorToInt(totalLength / laneLineSpacing);

        for (int i = 0; i <= count; i++)
        {
            float t = (i * laneLineSpacing) / totalLength;
            if (t > 1f) t = 1f;

            spline.Evaluate(t, out float3 pos, out float3 tangent, out float3 up);
            ApplyStraightOverride(spline, t, ref pos, ref tangent, ref up);

            Vector3 position = pos;
            position += (Vector3)up * laneLineHeightOffset;
            Quaternion rotation = Quaternion.LookRotation(tangent, up);

            Instantiate(laneLinePrefab, position, rotation, lineParent);
        }
    }

    private void GenerateGuardrails()
    {
        if (guardwallPrefab == null) return;
        Transform guardwallParent = GetOrCreateHolder("GuardwallsHolder");

        Spline spline = splineContainer.Spline;
        float totalLength = spline.GetLength();
        int count = Mathf.FloorToInt(totalLength / guardwallSpacing);

        for (int i = 0; i <= count; i++)
        {
            float t = (i * guardwallSpacing) / totalLength;
            if (t > 1f) t = 1f;

            spline.Evaluate(t, out float3 pos, out float3 tangent, out float3 up);
            ApplyStraightOverride(spline, t, ref pos, ref tangent, ref up);

            // 这里的 cross 计算依赖于坚硬不扭曲的 tangent，去除平滑后护栏排列会恢复整齐
            float3 right = math.normalize(math.cross(up, tangent));

            Vector3 leftPos = (Vector3)pos - (Vector3)right * guardwallDistance;
            Vector3 rightPos = (Vector3)pos + (Vector3)right * guardwallDistance;

            leftPos += (Vector3)up * guardwallHeightOffset;
            rightPos += (Vector3)up * guardwallHeightOffset;

            Quaternion rotation = Quaternion.LookRotation(tangent, up);

            Instantiate(guardwallPrefab, leftPos, rotation, guardwallParent);
            Instantiate(guardwallPrefab, rightPos, rotation, guardwallParent);
        }
    }

    private Transform GetOrCreateHolder(string holderName)
    {
        Transform existingHolder = transform.Find(holderName);
        if (existingHolder != null)
        {
            DestroyImmediate(existingHolder.gameObject);
        }

        GameObject newHolder = new GameObject(holderName);
        newHolder.transform.SetParent(this.transform);
        newHolder.transform.localPosition = Vector3.zero;
        return newHolder.transform;
    }

    private float GetSmoothedHeight(Spline spline, float t)
    {
        float step = 0.05f;
        float prevT = Mathf.Clamp01(t - step);
        float nextT = Mathf.Clamp01(t + step);

        spline.Evaluate(prevT, out float3 p1, out float3 t1, out float3 u1);
        ApplyStraightOverride(spline, prevT, ref p1, ref t1, ref u1);

        spline.Evaluate(t, out float3 p2, out float3 t2, out float3 u2);
        ApplyStraightOverride(spline, t, ref p2, ref t2, ref u2);

        spline.Evaluate(nextT, out float3 p3, out float3 t3, out float3 u3);
        ApplyStraightOverride(spline, nextT, ref p3, ref t3, ref u3);

        float h1 = p1.y;
        float h2 = p2.y;
        float h3 = p3.y;

        return (h1 + h2 + h3) / 3.0f;
    }
}