using System.Collections;
using UnityEngine;
using UnityEngine.UI;
// using UnityEngine.SceneManagement; // 不再需要场景管理

public class LoadingScreenManager : MonoBehaviour
{
    // ===============================================
    // UI 引用
    // ===============================================
    public Slider loadingSlider;
    public Text progressText;
    public Button startButton;
    public Image imageBackground;

    [Header("Transition Settings")]
    [Tooltip("加载完成后需要激活的目标 Canvas 或 UI 根对象")]
    public GameObject targetCanvas; // 新增：目标 Canvas 引用

    public float loadingSpeed = 0.3f;

    void Start()
    {
        // 1. 确保目标 Canvas 在加载开始时是隐藏的
        if (targetCanvas != null)
        {
            targetCanvas.SetActive(false);
        }

        // 2. 按钮隐藏
        if (startButton != null)
        {
            startButton.gameObject.SetActive(false);
        }

        // 3. 显示加载 UI
        if (loadingSlider != null) loadingSlider.gameObject.SetActive(true);
        if (progressText != null) progressText.gameObject.SetActive(true);
        if (imageBackground != null) imageBackground.enabled = true;

        // 4. 为按钮添加点击事件
        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartButtonClick);
        }

        // 5. 模拟加载的协程
        StartCoroutine(FillProgressBar());
    }

    private IEnumerator FillProgressBar()
    {
        float progress = 0f;

        while (progress < 1f)
        {
            progress += loadingSpeed * Time.deltaTime;
            progress = Mathf.Clamp01(progress);

            if (loadingSlider != null) loadingSlider.value = progress;
            if (progressText != null)
            {
                progressText.text = (progress * 100f).ToString("F0") + "%";
            }

            yield return null;
        }

        // 模拟加载完成后
        Debug.Log("模拟加载完成!");


        if (loadingSlider != null) loadingSlider.gameObject.SetActive(false);
        if (progressText != null) progressText.gameObject.SetActive(false);

        // 禁用Image组件让背景变透明
        if (imageBackground != null)
        {
            imageBackground.enabled = false;
        }

        // 显示开始按钮
        if (startButton != null)
        {
            startButton.gameObject.SetActive(true);
        }
    }

    public void OnStartButtonClick()
    {
        Debug.Log("按钮被点击，切换到下一个 Canvas...");

        // ** 核心修改：禁用当前加载 Canvas，启用目标 Canvas **

        // 1. 启用目标 Canvas
        if (targetCanvas != null)
        {
            targetCanvas.SetActive(true);
        }
        else
        {
            Debug.LogError("目标 Canvas 未设置！无法切换。");
        }

        // 2. 禁用当前的加载 Canvas
        // 假设该脚本挂载在加载 Canvas 的根对象上
        this.gameObject.SetActive(false);
    }
}