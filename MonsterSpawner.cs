// 文件名: MonsterSpawner.cs

using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

// 允许脚本在编辑模式下运行
[ExecuteInEditMode]
public class MonsterSpawner : MonoBehaviour
{
    // 在 Inspector 中赋值怪物预制体
    public GameObject MonsterPrefab;

    [Tooltip("拖入包含所有怪物生成点空对象的父容器。")]
    public GameObject SpawnPointsContainer;

    [Tooltip("怪物的近似高度 (Y轴的生成偏移量)。")]
    public float MonsterSpawnYOffset = 0.5f;


    [ContextMenu("Generate Monsters Now")]
    public void GenerateMonsters()
    {
        // 1. 清理旧的生成的怪物
        if (!Application.isPlaying)
        {
            DestroyExistingMonsters();
        }

        if (MonsterPrefab == null)
        {
            Debug.LogError("MonsterPrefab is not assigned in the MonsterSpawner!");
            return;
        }

        if (SpawnPointsContainer == null)
        {
            Debug.LogError("FATAL: SpawnPointsContainer object is not assigned!");
            return;
        }

        // 检查怪物 Prefab 是否有 NavMeshAgent 组件
        if (MonsterPrefab.GetComponent<NavMeshAgent>() == null)
        {
            Debug.LogError("MonsterPrefab is missing the NavMeshAgent component! Patrol Auto-Bind will fail.");
        }

        // 获取所有子对象 (即你手动设置的生成位置)
        Transform[] spawnLocations = SpawnPointsContainer.GetComponentsInChildren<Transform>(true);

        // 创建一个父对象来组织新生成的怪物
        GameObject monsterHolder = new GameObject("Generated_Monsters");

        foreach (Transform spawnPoint in spawnLocations)
        {
            // 排除父容器本身
            if (spawnPoint != SpawnPointsContainer.transform)
            {
                // 计算最终生成位置：使用 SpawnPoint 的 X/Z 坐标，加上设定的 Y 偏移
                Vector3 spawnPosition = new Vector3(
                    spawnPoint.position.x,
                    spawnPoint.position.y + MonsterSpawnYOffset, // 应用 Y 偏移
                    spawnPoint.position.z
                );

                // 实例化怪物
                GameObject monsterInstance = Instantiate(MonsterPrefab, spawnPosition, Quaternion.identity);

                // 将新生成的对象归类到 monsterHolder 下
                monsterInstance.transform.SetParent(monsterHolder.transform);

                // 确保在编辑模式下能被保存
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(monsterInstance);
#endif

                Debug.Log($"Spawned Monster at: {spawnPoint.name}");
            }
        }

        // 确保父对象也被保存
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(monsterHolder);
#endif
    }

    // 清理旧生成的怪物实例的方法
    [ContextMenu("Clear Monsters Now")]
    private void DestroyExistingMonsters()
    {
        // 注意：这里查找的是 GenerateMonsters() 创建的父对象
        GameObject oldMonsters = GameObject.Find("Generated_Monsters");

        if (oldMonsters != null)
        {
            if (Application.isEditor && !Application.isPlaying)
            {
                DestroyImmediate(oldMonsters);
                Debug.Log("Cleaned up old generated monsters.");
            }
            else
            {
                Destroy(oldMonsters);
            }
        }

        // 可选：也可以查找并清理 ItemSpawner 生成的物品
    }
}