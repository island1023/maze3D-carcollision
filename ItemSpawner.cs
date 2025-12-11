// 文件名: ItemSpawner.cs

using UnityEngine;
using System.Collections.Generic;

// 关键改动 1: 允许脚本在编辑模式下运行
[ExecuteInEditMode]
public class ItemSpawner : MonoBehaviour
{
    // 在 Inspector 中赋值钻石预制体
    public GameObject TreasurePrefab;

    [Tooltip("拖入包含所有钻石生成点空对象的父容器。")]
    public GameObject SpawnPointsContainer;

    // *** 关键改动 2: 添加手动触发按钮 ***
    [ContextMenu("Generate Items Now")]
    public void GenerateItems()
    {
        // --- 定义固定的 Y 坐标 ---
        const float FixedYPosition = 1.43f;
        // ------------------------

        // 1. 清理旧的生成的钻石
        if (!Application.isPlaying)
        {
            DestroyExistingItems();
        }

        if (TreasurePrefab == null)
        {
            Debug.LogError("TreasurePrefab is not assigned in the ItemSpawner!");
            return;
        }

        // 查找手动设置的生成点容器
        if (SpawnPointsContainer == null)
        {
            Debug.LogError("FATAL: SpawnPointsContainer object is not assigned!");
            return;
        }

        // 获取所有子对象 (即你手动设置的生成位置)
        Transform[] spawnLocations = SpawnPointsContainer.GetComponentsInChildren<Transform>(true);

        // 创建一个父对象来组织新生成的钻石
        GameObject itemHolder = new GameObject("Generated_Items");

        foreach (Transform spawnPoint in spawnLocations)
        {
            // 排除父容器本身
            if (spawnPoint != SpawnPointsContainer.transform)
            {
                // 创建新的位置向量，使用 spawnPoint 的 X 和 Z，强制使用固定的 Y 值
                Vector3 spawnPosition = new Vector3(
                    spawnPoint.position.x,
                    FixedYPosition, // <-- 关键修改：强制 Y 轴为 1.43f
                    spawnPoint.position.z
                );

                // 在每个标记的位置实例化钻石
                GameObject diamond = Instantiate(TreasurePrefab, spawnPosition, Quaternion.identity);

                // 将新生成的对象归类到 itemHolder 下
                diamond.transform.SetParent(itemHolder.transform);

                // 确保在编辑模式下能被保存
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(diamond);
#endif

                Debug.Log($"Spawned Diamond at: {spawnPoint.name} (Y forced to {FixedYPosition}f)");
            }
        }
    }

    // 清理旧生成的钻石实例的方法
    private void DestroyExistingItems()
    {
        GameObject oldItems = GameObject.Find("Generated_Items");
        if (oldItems != null)
        {
            if (Application.isEditor && !Application.isPlaying)
            {
                // 关键：在编辑模式下必须使用 DestroyImmediate
                DestroyImmediate(oldItems);
                Debug.Log("Cleaned up old generated items.");
            }
            else
            {
                Destroy(oldItems);
            }
        }
    }

    /*
    void Start()
    {
        // 如果想在运行时也生成，可以在这里调用 GenerateItems()，
        // 但如果主要目的是编辑模式固化，则不需要 Start()
    }
    */
}