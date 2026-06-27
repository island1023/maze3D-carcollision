using UnityEngine;
using UnityEditor;

/// <summary>
/// 菜单方式细分（可选）。更推荐用组件 CarriageMeshSubdivide + Inspector 按钮。
/// </summary>
public static class CarriageMeshSubdivider
{
    const string MenuPath = "Tools/细分车厢外壳网格";

    [MenuItem(MenuPath, true)]
    static bool Validate() => Selection.activeGameObject != null;

    [MenuItem(MenuPath, false, 100)]
    static void SubdivideSelected()
    {
        var go = Selection.activeGameObject;
        if (go == null) return;

        var comp = go.GetComponent<CarriageMeshSubdivide>();
        if (comp == null)
            comp = Undo.AddComponent<CarriageMeshSubdivide>(go);

        if (!comp.TrySubdivide(out string msg))
        {
            EditorUtility.DisplayDialog("细分失败", msg, "确定");
            return;
        }

        var mf = go.GetComponent<MeshFilter>();
        string folder = "Assets/GeneratedMeshes";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "GeneratedMeshes");

        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{go.name}_subdivided.asset");
        AssetDatabase.CreateAsset(mf.sharedMesh, path);
        AssetDatabase.SaveAssets();
        mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);

        EditorUtility.DisplayDialog("细分完成", msg, "确定");
        Debug.Log("[车厢细分] " + msg + " → " + path);
    }
}
