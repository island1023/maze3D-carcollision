using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景加载后自动为静止 SedenAItruck 车厢补全变形组件（兼容旧预制体）。
/// 不会作用于移动 AITruck（AITruck1）。
/// </summary>
public static class SedenAICarriageBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void SetupAllCarriages()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.isLoaded)
            return;

        foreach (GameObject root in scene.GetRootGameObjects())
            Visit(root.transform);
    }

    static void Visit(Transform node)
    {
        if (TruckTrailerDeformation.IsDeformationTarget(node)
            && node.GetComponent<TruckTrailerDeformation>() == null)
        {
            node.gameObject.AddComponent<TruckTrailerDeformation>();
            Debug.Log($"[车厢变形] 已为 {node.name} 自动添加 TruckTrailerDeformation");
        }

        for (int i = 0; i < node.childCount; i++)
            Visit(node.GetChild(i));
    }
}
