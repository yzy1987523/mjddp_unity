using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;

/// <summary>
/// 关卡选择面板组件（挂在 LevelPanel 预制体根）。预制体含：标题 + LevelList(VLG 容器) + Btn_Back。
/// Init 绑定返回回调；BuildList 按解锁状态运行时实例化 LevelItem 预制体并 Bind。
/// </summary>
public class LevelSelectPanel : MonoBehaviour
{
    private Transform _list;

    public void Init(UnityAction onBack)
    {
        _list = FindDeep(transform, "LevelList");
        var backBtn = FindDeep(transform, "Btn_Back")?.GetComponent<Button>();
        if (backBtn == null) backBtn = FindButtonByText(transform, "返回");
        backBtn?.onClick.AddListener(onBack);
    }

    public void BuildList(string[] names, int unlockedMax, UnityAction<int> onPick)
    {
        if (_list == null) return;
        var children = new List<GameObject>();
        for (int i = 0; i < _list.childCount; i++) children.Add(_list.GetChild(i).gameObject);
        foreach (var c in children) UnityEngine.Object.Destroy(c);

        var prefab = Resources.Load<GameObject>("Prefab/LevelItem");
        for (int i = 0; i < names.Length; i++)
        {
            int idx = i;
            bool locked = i > unlockedMax;
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, _list);
            }
            else
            {
                // 回退：LevelItem 预制体缺失时运行时构建按钮
                var b = UiTools.AddButton(_list, names[i], UiTools.LoadFont());
                go = b.gameObject;
                var le = go.AddComponent<LayoutElement>();
                le.minHeight = 64; le.preferredHeight = 64; le.flexibleWidth = 1;
                b.interactable = !locked;
                if (!locked) b.onClick.AddListener(() => onPick(idx));
                continue;
            }
            var item = go.GetComponent<LevelItem>();
            if (item != null) item.Bind(idx, names[i], locked, onPick);
            else
            {
                var le = go.AddComponent<LayoutElement>();
                le.minHeight = 64; le.preferredHeight = 64; le.flexibleWidth = 1;
                var b = go.GetComponent<Button>();
                if (b != null) { b.interactable = !locked; if (!locked) b.onClick.AddListener(() => onPick(idx)); }
            }
        }
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
