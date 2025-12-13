using UnityEngine;
// 移除 UnityEngine.SceneManagement，因为场景管理现在由 UIManager 负责

public class FinishGate : MonoBehaviour
{
    [Tooltip("需要检测触发的玩家Tag，请确保玩家对象设置为此 Tag。")]
    public string playerTag = "Player";

    // 移除 public GameObject gameOverUI; 字段，UI 管理职责转移给 UIManager

    // ----------------------------------------------------------------------
    // Unity 内建方法：当另一个 Collider 进入此触发器时调用
    // ----------------------------------------------------------------------
    private void OnTriggerEnter(Collider other)
    {
        // 检查碰撞到的对象是否带有正确的玩家 Tag
        if (other.CompareTag(playerTag))
        {
            // 确保只触发一次 Game Over 逻辑
            if (Time.timeScale != 0)
            {
                TriggerGameGoal(); // 调用新的胜利逻辑
            }
        }
    }

    // ----------------------------------------------------------------------
    // 胜利逻辑 (调用 UIManager 显示胜利界面)
    // ----------------------------------------------------------------------
    private void TriggerGameGoal()
    {
        Debug.Log("迷宫完成！触发胜利 Game Over 逻辑。");

        // 关键改动：检查 UIManager 实例，并调用显示胜利界面的方法
        if (UIManager.Instance != null)
        {
            // UIManager 会负责 Time.timeScale = 0f; 和激活 Canvas
            UIManager.Instance.ShowGoalUI();
        }
        else
        {
            Debug.LogError("UIManager 实例未找到！请确保场景中有 UIManager 脚本挂载。");

            // 如果 UIManager 找不到，为了不让游戏卡住，手动停止时间并输出警告
            Time.timeScale = 0f;
        }

        // 清理 Hint 路径（如果需要，需要先修改 MazeHint.ClearHintPath 为 public）
        // MazeHint hintScript = FindObjectOfType<MazeHint>();
        // if (hintScript != null)
        // {
        //     hintScript.ClearHintPath();
        // }
    }

    // ----------------------------------------------------------------------
    // 移除 RestartGame() 方法，它现在位于 UIManager.cs 中
    // ----------------------------------------------------------------------
}