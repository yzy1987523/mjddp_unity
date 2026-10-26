using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;

/// <summary>
/// 图鉴面板组件（挂在 CodexPanel 预制体根）。预制体含：标题 + CodexScroll(ScrollRect>Viewport>Content) + CloseBtn。
/// Init 绑定关闭回调；Refresh 按数据运行时实例化 CodexItem 预制体并 Bind。
/// </summary>
public class CodexPanel : MonoBehaviour
{
    private Transform _content;

    public void Init(UnityAction onClose)
    {
        _content = FindDeep(transform, "Content");
        var closeBtn = FindDeep(transform, "CloseBtn")?.GetComponent<Button>();
        if (closeBtn == null) closeBtn = FindButtonByText(transform, "关闭");
        closeBtn?.onClick.AddListener(onClose);
    }

    public void Refresh(List<Sprite> faces, int[] collected, Sprite[] levelBacks, Color[] codeColors)
    {
        if (_content == null) return;
        var children = new List<GameObject>();
        for (int i = 0; i < _content.childCount; i++) children.Add(_content.GetChild(i).gameObject);
        foreach (var c in children) UnityEngine.Object.Destroy(c);

        var prefab = Resources.Load<GameObject>("Prefab/CodexItem");
        var font = UiTools.LoadFont();
        for (int f = 0; f < faces.Count; f++)
        {
            int count = (f < collected.Length) ? collected[f] : 0;
            int lvl = LevelOf(count);
            if (prefab != null)
            {
                var go = Instantiate(prefab, _content);
                var item = go.GetComponent<CodexItem>();
                if (item != null)
                {
                    Sprite lb = (lvl >= 0 && lvl < levelBacks.Length) ? levelBacks[lvl] : null;
                    Color cc = (codeColors != null && lvl >= 0 && lvl < codeColors.Length) ? codeColors[lvl] : Color.gray;
                    item.Bind(faces[f], count, lvl, lb, cc);
                }
            }
            else
            {
                // 回退：CodexItem 预制体缺失时运行时构建行
                BuildRowFallback(_content, f, faces[f], count, lvl, levelBacks, codeColors, font);
            }
        }
    }

    // 回退：CodexItem 预制体缺失时运行时构建行（逻辑同原 RefreshCodex）
    private static void BuildRowFallback(Transform parent, int f, Sprite face, int count, int lvl, Sprite[] levelBacks, Color[] codeColors, Font font)
    {
        var row = UiTools.MakeGO("Row" + f, parent);
        var rtr = row.GetComponent<RectTransform>(); rtr.sizeDelta = new Vector2(0, 64);
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12; hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

        var icon = UiTools.MakeGO("Icon", row.transform);
        var itr = icon.GetComponent<RectTransform>(); itr.sizeDelta = new Vector2(48, 48);
        var iimg = icon.AddComponent<Image>(); iimg.preserveAspect = true; iimg.sprite = face;

        var txt = UiTools.MakeGO("Txt", row.transform);
        var t2 = txt.AddComponent<Text>();
        t2.text = "x" + count + "  Lv" + lvl; t2.fontSize = 24; t2.alignment = TextAnchor.MiddleLeft;
        if (font != null) t2.font = font;
        var ttr2 = txt.GetComponent<RectTransform>(); ttr2.sizeDelta = new Vector2(200, 48);

        var col = UiTools.MakeGO("Color", row.transform);
        var c2 = col.GetComponent<RectTransform>(); c2.sizeDelta = new Vector2(48, 48);
        var cimg = col.AddComponent<Image>(); cimg.preserveAspect = true;
        if (lvl >= 0 && lvl < levelBacks.Length && levelBacks[lvl] != null) cimg.sprite = levelBacks[lvl];
        else if (codeColors != null && lvl >= 0 && lvl < codeColors.Length) cimg.color = codeColors[lvl];
        else cimg.color = Color.gray;
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

    // 兜底：递归找文本为指定内容的 Button（防止预制体按钮改名后找不到）
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

    private static int LevelOf(int count) => count >= 1000 ? 3 : (count >= 100 ? 2 : (count >= 10 ? 1 : 0));
}
