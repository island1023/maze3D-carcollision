using UnityEngine;
using UnityEditor; // 必须在 Editor 文件夹内才能使用此命名空间
using System.Collections.Generic;
using UnityEditor.Animations; // 引入 Animator Controller 相关命名空间

public class EditorEventInjector : EditorWindow
{
    private GameObject targetObject;
    private string attackClipName = "attack"; // 你的攻击动画剪辑的名称 (根据你的截图，它叫 'attack')
    private string methodName = "EndAttackCheck";
    private float eventTime = 0.5f; // 动画持续时间的一半作为示例 (0.0 到 1.0 之间)
    private bool needsStartEvent = true;
    private float startTime = 0.1f;
    private string startMethodName = "StartAttackCheck";


    [MenuItem("Tools/Animation/Inject Attack Events")]
    public static void ShowWindow()
    {
        GetWindow<EditorEventInjector>("Inject Attack Events");
    }

    void OnGUI()
    {
        GUILayout.Label("攻击动画事件注入工具", EditorStyles.boldLabel);

        // 目标对象选择
        targetObject = (GameObject)EditorGUILayout.ObjectField("目标玩家对象:", targetObject, typeof(GameObject), true);

        // 动画剪辑信息
        attackClipName = EditorGUILayout.TextField("攻击动画剪辑名称:", attackClipName);
        eventTime = EditorGUILayout.FloatField("EndEvent 时间 (0.0 - 1.0):", eventTime);
        methodName = EditorGUILayout.TextField("EndEvent 函数名:", methodName);

        GUILayout.Space(10);
        needsStartEvent = EditorGUILayout.Toggle("需要 StartAttackCheck?", needsStartEvent);
        if (needsStartEvent)
        {
            startTime = EditorGUILayout.FloatField("StartEvent 时间 (0.0 - 1.0):", startTime);
            startMethodName = EditorGUILayout.TextField("StartEvent 函数名:", startMethodName);
        }

        GUILayout.Space(20);

        if (GUILayout.Button("注入 EndAttackCheck 事件"))
        {
            InjectEvents(targetObject, attackClipName, eventTime, methodName, needsStartEvent ? startTime : -1f, startMethodName);
        }
    }


    private void InjectEvents(GameObject target, string clipName, float endTime, string endMethod, float startTime = -1f, string startMethod = "")
    {
        if (target == null)
        {
            Debug.LogError("请拖入目标玩家对象。");
            return;
        }

        Animator animator = target.GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("目标对象没有 Animator 组件。");
            return;
        }

        AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
        if (controller == null)
        {
            Debug.LogError("无法找到 Animator Controller。");
            return;
        }

        // 1. 查找动画剪辑
        AnimationClip clip = FindClip(controller, clipName);

        if (clip == null)
        {
            Debug.LogError($"在 Controller 中找不到名为 '{clipName}' 的动画剪辑。");
            return;
        }

        // 2. 清空旧事件并设置新事件
        AnimationEvent[] oldEvents = AnimationUtility.GetAnimationEvents(clip);
        List<AnimationEvent> newEvents = new List<AnimationEvent>();

        // 保留非攻击事件（如果需要，一般不保留）
        // for (int i = 0; i < oldEvents.Length; i++) { if (oldEvents[i].functionName != endMethod) newEvents.Add(oldEvents[i]); }


        // 3. 添加 EndAttackCheck 事件
        AnimationEvent endEvent = new AnimationEvent
        {
            functionName = endMethod,
            // 确保 eventTime 在 0.0 到 1.0 之间，对应归一化时间
            time = Mathf.Clamp01(endTime) * clip.length
        };
        newEvents.Add(endEvent);
        Debug.Log($"✅ 添加了 EndEvent: {endMethod}，时间点: {endEvent.time:F3}s");

        // 4. 添加 StartAttackCheck 事件 (如果需要)
        if (startTime >= 0)
        {
            AnimationEvent startEvent = new AnimationEvent
            {
                functionName = startMethod,
                time = Mathf.Clamp01(startTime) * clip.length
            };
            newEvents.Add(startEvent);
            Debug.Log($"✅ 添加了 StartEvent: {startMethod}，时间点: {startEvent.time:F3}s");
        }


        // 5. 应用事件到动画剪辑
        AnimationUtility.SetAnimationEvents(clip, newEvents.ToArray());
        AssetDatabase.SaveAssets();
        Debug.Log($"🎉 动画剪辑 '{clipName}' 事件注入完成。");
    }

    private AnimationClip FindClip(AnimatorController controller, string clipName)
    {
        foreach (var clip in controller.animationClips)
        {
            if (clip.name == clipName)
            {
                return clip;
            }
        }
        return null;
    }
}