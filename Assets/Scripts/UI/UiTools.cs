using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 运行时 / Editor 共用的极简 UI 构建工具。
/// 生成脚本用它把完整 UI 层级建进预制体；面板组件用它运行时填充动态列表（关卡 / 图鉴）。
/// </summary>
public static class UiTools
{
    public static Font LoadFont() { return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }

    public static GameObject MakeGO(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    public static Text AddText(Transform parent, string txt, int size, Color color, Font font = null)
    {
        if (font == null) font = LoadFont();
        var go = MakeGO("Text", parent);
        var t = go.AddComponent<Text>();
        t.text = txt; t.font = font; t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter; t.color = color;
        var tr = go.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
        return t;
    }

    public static Button AddButton(Transform parent, string label, Font font = null)
    {
        if (font == null) font = LoadFont();
        var go = MakeGO("Button", parent);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.20f, 0.50f, 0.82f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        AddText(go.transform, label, 26, Color.white, font);
        return btn;
    }

    // 全屏遮罩面板（铺满父容器 + 半透明黑底 + 居中垂直布局），不含子内容
    public static GameObject MakePanel(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var tr = go.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.7f);
        var lg = go.AddComponent<VerticalLayoutGroup>();
        lg.spacing = 20; lg.childAlignment = TextAnchor.MiddleCenter;
        lg.padding = new RectOffset(40, 40, 40, 40);
        return go;
    }
}
