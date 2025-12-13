using UnityEngine;
using UnityEngine.SceneManagement; // 导入场景管理命名空间
using UnityEngine.UI; // 必须导入 UI 命名空间来使用 Text

public class UIManager : MonoBehaviour
{
    [Header("UI 面板引用")]
    public GameObject gameOverDeathCanvas; // 玩家死亡界面 (Restart/Exit)
    public GameObject gameOverGoalCanvas;  // 到达终点界面 (Again/Exit)

    // --- 新增得分 UI 字段 ---
    [Header("Game UI")]
    [Tooltip("左上方的得分 Text 组件")]
    public Text scoreText;

    private int currentScore = 0; // 跟踪当前得分，默认初始化为 0

    // 通常 UI 管理器应只有一个实例，使用 Singleton 模式确保
    public static UIManager Instance { get; private set; }

    void Awake()
    {
        // 确保单例模式
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
            // 确保刚开始面板都是隐藏的
            if (gameOverDeathCanvas != null) gameOverDeathCanvas.SetActive(false);
            if (gameOverGoalCanvas != null) gameOverGoalCanvas.SetActive(false);

            // 初始化得分显示
            UpdateScoreUI();
        }
    }

    // ----------------------
    // 公共显示方法
    // ----------------------

    // 在玩家死亡时调用此方法
    public void ShowDeathUI()
    {
        // 冻结游戏时间
        Time.timeScale = 0f;
        // 显示死亡 Canvas
        if (gameOverDeathCanvas != null) gameOverDeathCanvas.SetActive(true);
    }

    // 在到达终点时调用此方法
    public void ShowGoalUI()
    {
        // 冻结游戏时间
        Time.timeScale = 0f;
        // 显示胜利 Canvas
        if (gameOverGoalCanvas != null) gameOverGoalCanvas.SetActive(true);
    }

    // --- 新增公共方法：增加得分 ---
    /// <summary>
    /// 增加得分并更新 UI。由 Diamond.cs 或其他拾取物调用。
    /// </summary>
    public void AddScore(int amount)
    {
        currentScore += amount;
        UpdateScoreUI();
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            // 只显示数字，并将 int 类型转换为 string
            scoreText.text = currentScore.ToString();
        }
    }


    // ----------------------
    // 按钮功能方法 (按钮点击事件绑定到这里)
    // ----------------------

    // "Restart" (死亡界面) 或 "Again" (终点界面) 按钮共用此方法
    public void RestartLevel()
    {
        // 恢复游戏时间
        Time.timeScale = 1f;
        // 加载当前活动场景
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // "Exit" 按钮共用此方法
    public void ExitGame()
    {
        Debug.Log("退出游戏...");

        // 退出逻辑：在编辑器中停止播放，在构建版本中退出应用
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}