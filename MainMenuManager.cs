using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 确保该脚本挂载在 Canvas 或 Canvas 的一个子对象上
public class MainMenuManager : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("游戏主场景的名称，必须与Build Settings中的名称一致")]
    public string gameSceneName = "GameScene";

    [Header("UI References")]
    [Tooltip("游戏介绍Image或Panel，用于点击后显示/隐藏")]
    public GameObject introPanel;

    // 注意：您可以省略按钮引用，但为了在 Inspector 中确认，可以保留
    // public Button startButton;
    // public Button introButton;
    // public Button quitButton;

    void Start()
    {
        // 确保游戏介绍面板在开始时是隐藏的
        if (introPanel != null)
        {
            introPanel.SetActive(false);
        }
    }

    // ----------------------------------------------------
    // 按钮点击事件方法
    // ----------------------------------------------------

    /// <summary>
    /// 开始游戏：加载游戏主场景。
    /// </summary>
    public void OnStartGameClick()
    {
        if (string.IsNullOrEmpty(gameSceneName))
        {
            Debug.LogError("目标游戏场景名称未设置！请在 Inspector 中设置。");
            return;
        }

        Debug.Log("加载场景: " + gameSceneName);
        SceneManager.LoadScene(gameSceneName);
    }

    /// <summary>
    /// 游戏介绍：切换介绍面板的可见性。
    /// </summary>
    public void OnIntroClick()
    {
        if (introPanel != null)
        {
            // 切换面板的激活状态：如果当前激活，则设为非激活；如果非激活，则设为激活。
            bool isCurrentlyActive = introPanel.activeSelf;
            introPanel.SetActive(!isCurrentlyActive);

            Debug.Log("游戏介绍面板已切换为: " + introPanel.activeSelf);
        }
        else
        {
            Debug.LogError("未设置游戏介绍面板的引用 (introPanel)。");
        }
    }

    /// <summary>
    /// 退出游戏：关闭应用程序。
    /// </summary>
    public void OnQuitGameClick()
    {
        Debug.Log("退出游戏...");

#if UNITY_EDITOR
            // 如果在Unity编辑器中运行，则停止播放
            UnityEditor.EditorApplication.isPlaying = false;
#else
        // 如果是构建后的游戏，则退出应用程序
        Application.Quit();
#endif
    }
}