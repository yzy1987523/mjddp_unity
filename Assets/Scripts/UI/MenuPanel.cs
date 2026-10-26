using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// 主菜单面板组件（挂在 MenuPanel 预制体根）。预制体含：标题 Text + 三个按钮
/// Btn_Start / Btn_Levels / Btn_Settings。Init 绑定回调，不生成 UI。
/// </summary>
public class MenuPanel : MonoBehaviour
{
    public void Init(UnityAction onStart, UnityAction onLevels, UnityAction onSettings)
    {
        transform.Find("Btn_Start")?.GetComponent<Button>()?.onClick.AddListener(onStart);
        transform.Find("Btn_Levels")?.GetComponent<Button>()?.onClick.AddListener(onLevels);
        transform.Find("Btn_Settings")?.GetComponent<Button>()?.onClick.AddListener(onSettings);
    }
}
