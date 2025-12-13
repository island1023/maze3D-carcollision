using UnityEngine;

[RequireComponent(typeof(Collider))] // 确保有碰撞体
public class Diamond : MonoBehaviour
{
    [Tooltip("拾取钻石获得的得分")]
    public int scoreValue = 10;

    // --- 新增音效字段 ---
    [Header("Audio Settings")]
    [Tooltip("拾取时播放的音效文件。请从 Project 视图拖入。")]
    public AudioClip collectSound;

    // 确保在 Inspector 中，Collider 的 Is Trigger 被勾选

    private void OnTriggerEnter(Collider other)
    {
        // 假设玩家对象上有一个特定的Tag，例如 "Player"
        if (other.CompareTag("Player"))
        {
            // 1. 播放音效
            if (collectSound != null)
            {
                // 使用 PlayClipAtPoint 在钻石位置播放音效，播放完成后自动清理。
                AudioSource.PlayClipAtPoint(collectSound, transform.position);
            }

            // 2. 增加得分
            if (UIManager.Instance != null)
            {
                UIManager.Instance.AddScore(scoreValue);
            }

            // 3. 销毁钻石
            Destroy(gameObject);
        }
    }
}