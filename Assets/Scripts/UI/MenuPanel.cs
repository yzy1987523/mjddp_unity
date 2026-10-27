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
    public void Init(UnityAction onStart, UnityAction onLevels, UnityAction onSettings, UnityAction onCodex)
    {
        transform.Find("Btn_Start")?.GetComponent<Button>()?.onClick.AddListener(onStart);
        transform.Find("Btn_Levels")?.GetComponent<Button>()?.onClick.AddListener(onLevels);
        transform.Find("Btn_Settings")?.GetComponent<Button>()?.onClick.AddListener(onSettings);
        // 主菜单上的图鉴按钮（用户手动添加，可能嵌套层级）：递归查找 + 文本兜底
        var codexBtn = FindDeep(transform, "Btn_Codex")?.GetComponent<Button>();
        if (codexBtn == null) codexBtn = FindButtonByText(transform, "图鉴");
        codexBtn?.onClick.AddListener(onCodex);
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform c in root)
        {
            if (c.name == name) return c;
            var d = FindDeep(c, name);
            if (d != null) return d;
        }
        return null;
    }

    private static Button FindButtonByText(Transform root, string text)
    {
        var btn = root.GetComponent<Button>();
        if (btn != null)
        {
            var t = root.GetComponentInChildren<Text>();
            if (t != null && t.text == text) return btn;
        }
        foreach (Transform c in root)
        {
            var b = FindButtonByText(c, text);
            if (b != null) return b;
        }
        return null;
    }
}
