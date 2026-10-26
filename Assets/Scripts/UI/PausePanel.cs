using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// 暂停面板组件（挂在 PausePanel 预制体根）。预制体含按钮
/// Btn_Resume / Btn_Retry / Btn_Menu。Init 绑定回调，不生成 UI。
/// </summary>
public class PausePanel : MonoBehaviour
{
    public void Init(UnityAction onResume, UnityAction onRetry, UnityAction onMenu)
    {
        transform.Find("Btn_Resume")?.GetComponent<Button>()?.onClick.AddListener(onResume);
        transform.Find("Btn_Retry")?.GetComponent<Button>()?.onClick.AddListener(onRetry);
        transform.Find("Btn_Menu")?.GetComponent<Button>()?.onClick.AddListener(onMenu);
    }
}
