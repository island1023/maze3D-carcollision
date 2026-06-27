using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(CarriageMeshSubdivide))]
public class CarriageMeshSubdivideEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var comp = (CarriageMeshSubdivide)target;
        var mf = comp.GetComponent<MeshFilter>();

        EditorGUILayout.Space(10);

        if (mf != null && mf.sharedMesh != null)
        {
            int vc = mf.sharedMesh.vertexCount;
            EditorGUILayout.LabelField("当前顶点数", vc.ToString(), EditorStyles.boldLabel);

            if (vc < 5000)
                EditorGUILayout.HelpBox("顶点太少，变形会像方块。请点下方按钮细分（建议 Iterations=3）。", MessageType.Warning);
            else if (vc >= 5000)
                EditorGUILayout.HelpBox("顶点数已足够支撑变形。Scene 视图请切换到 Shaded Wireframe 才能看到像路灯一样的网格线。", MessageType.Info);
        }

        EditorGUILayout.HelpBox(
            "看网格线：Scene 视图右上角 → 点击 Shaded 下拉 → 选 Shaded Wireframe\n" +
            "细分后必须保存预制体（Overrides → Apply All）",
            MessageType.None);

        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("▶ 执行车厢网格细分", GUILayout.Height(40)))
        {
            if (!comp.TrySubdivide(out string msg))
            {
                EditorUtility.DisplayDialog("细分失败", msg, "确定");
                Debug.LogError("[车厢细分] " + msg);
            }
            else
            {
                SaveMeshAsset(comp);
                EditorUtility.SetDirty(comp);
                PrefabUtility.RecordPrefabInstancePropertyModifications(mf);

                EditorUtility.DisplayDialog(
                    "细分完成",
                    msg + "\n\n重要：\n1. 若物体是预制体实例，点 Hierarchy 顶部 Overrides → Apply All\n2. Scene 视图选 Shaded Wireframe 看网格线\n3. Play 撞车测试",
                    "确定");
                Debug.Log("[车厢细分] " + msg);
            }
        }
        GUI.backgroundColor = Color.white;
    }

    static void SaveMeshAsset(CarriageMeshSubdivide comp)
    {
        var mf = comp.GetComponent<MeshFilter>();
        Mesh mesh = mf.sharedMesh;

        string folder = "Assets/GeneratedMeshes";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "GeneratedMeshes");

        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{comp.gameObject.name}_subdivided.asset");
        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();

        Undo.RecordObject(mf, "Subdivide Carriage Mesh");
        mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        EditorUtility.SetDirty(mf);
        PrefabUtility.RecordPrefabInstancePropertyModifications(mf);

        Debug.Log("[车厢细分] 已保存并应用网格：" + path + "，顶点=" + mf.sharedMesh.vertexCount);
    }
}
