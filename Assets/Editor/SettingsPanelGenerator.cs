using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.IO;

#if UNITY_EDITOR
/// <summary>
/// 一键生成设置面板预制体：在 Assets/Resources/SettingsPanel.prefab 创建含完整 UI 层级的预制体
/// （遮罩 / 米色卡片 / 标题 / 音乐·音效·震动开关 / 画质三档 / 语音 / 返回），根挂 SettingsPanel 组件。
/// 运行时只实例化并显示，组件只查找绑定、不生成 UI。
/// 使用：Unity 菜单 Tools / 生成设置面板预制体（若已存在则覆盖）。
/// </summary>
public class SettingsPanelGenerator
{
    [MenuItem("Tools/生成设置面板预制体")]
    public static void Generate()
    {
        var root = new GameObject("SettingsPanel", typeof(RectTransform));
        root.AddComponent<SettingsPanel>();

        var img = root.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0.7f);
        var lg = root.AddComponent<VerticalLayoutGroup>();
        lg.childAlignment = TextAnchor.MiddleCenter;
        lg.childControlWidth = true; lg.childControlHeight = true;
        lg.childForceExpandWidth = false; lg.childForceExpandHeight = false;
        lg.spacing = 24;

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        MakeText(root.transform, "设置", 46, font, Color.white);

        var card = MakeGO("SettingsCard", root.transform);
        var cimg = card.AddComponent<Image>();
        cimg.color = new Color(0.93f, 0.89f, 0.80f, 1f);
        var clg = card.AddComponent<VerticalLayoutGroup>();
        clg.spacing = 26; clg.padding = new RectOffset(36, 36, 32, 32);
        clg.childAlignment = TextAnchor.MiddleCenter;
        clg.childControlWidth = true; clg.childControlHeight = true;
        clg.childForceExpandWidth = true; clg.childForceExpandHeight = false;
        var cle = card.AddComponent<LayoutElement>();
        cle.minWidth = 560; cle.preferredWidth = 560; cle.preferredHeight = 560;

        BuildToggleRow(card.transform, "Row_音乐", "音乐", font);
        BuildToggleRow(card.transform, "Row_音效", "音效", font);
        BuildToggleRow(card.transform, "Row_震动", "震动", font);
        BuildQualityRow(card.transform, font);
        BuildLangRow(card.transform, font);

        var back = MakeButton(root.transform, "返回", font);
        back.name = "BackBtn";

        if (!Directory.Exists("Assets/Resources/Prefab")) Directory.CreateDirectory("Assets/Resources/Prefab");
        string path = "Assets/Resources/Prefab/SettingsPanel.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[Settings] 已生成含完整 UI 的预制体：" + path);
    }

    static GameObject MakeGO(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static Text MakeText(Transform parent, string s, int size, Font font, Color color)
    {
        var go = MakeGO(s, parent);
        var t = go.AddComponent<Text>();
        t.text = s; t.font = font; t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter; t.color = color;
        var tr = go.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;
        return t;
    }

    static Button MakeButton(Transform parent, string label, Font font)
    {
        var go = MakeGO(label, parent);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.85f, 0.80f, 0.68f);
        var b = go.AddComponent<Button>();
        b.targetGraphic = img;
        MakeText(go.transform, label, 30, font, new Color(0.35f, 0.30f, 0.22f));
        return b;
    }

    static void BuildToggleRow(Transform parent, string rowName, string label, Font font)
    {
        var row = MakeGO(rowName, parent);
        var rlg = row.AddComponent<HorizontalLayoutGroup>();
        rlg.spacing = 16; rlg.childAlignment = TextAnchor.MiddleLeft;
        rlg.childControlWidth = true; rlg.childControlHeight = true;
        rlg.childForceExpandWidth = false; rlg.childForceExpandHeight = true;
        var rle = row.AddComponent<LayoutElement>();
        rle.preferredHeight = 64; rle.flexibleWidth = 1;

        var txt = MakeText(row.transform, label, 34, font, new Color(0.35f, 0.30f, 0.22f));
        txt.alignment = TextAnchor.MiddleLeft;
        txt.gameObject.AddComponent<LayoutElement>().preferredWidth = 160;

        var spacer = MakeGO("Spacer", row.transform);
        spacer.AddComponent<LayoutElement>().flexibleWidth = 1;

        var track = MakeGO("Track", row.transform);
        track.AddComponent<LayoutElement>().preferredWidth = 120;
        track.AddComponent<Image>();
        track.AddComponent<Button>();
        var handle = MakeGO("Handle", track.transform);
        var htr = handle.GetComponent<RectTransform>();
        htr.sizeDelta = new Vector2(44, 44);
        htr.anchorMin = htr.anchorMax = new Vector2(0.5f, 0.5f);
        htr.anchoredPosition = new Vector2(30f, 0f);
        handle.AddComponent<Image>().color = new Color(0.97f, 0.96f, 0.90f);
    }

    static void BuildQualityRow(Transform parent, Font font)
    {
        var row = MakeGO("Row_Quality", parent);
        var rlg = row.AddComponent<HorizontalLayoutGroup>();
        rlg.spacing = 14; rlg.childAlignment = TextAnchor.MiddleLeft;
        rlg.childControlWidth = true; rlg.childControlHeight = true;
        rlg.childForceExpandWidth = false; rlg.childForceExpandHeight = true;
        var rle = row.AddComponent<LayoutElement>();
        rle.preferredHeight = 64; rle.flexibleWidth = 1;

        var txt = MakeText(row.transform, "画面质量", 34, font, new Color(0.35f, 0.30f, 0.22f));
        txt.alignment = TextAnchor.MiddleLeft;
        txt.gameObject.AddComponent<LayoutElement>().preferredWidth = 200;

        var spacer = MakeGO("Spacer", row.transform);
        spacer.AddComponent<LayoutElement>().flexibleWidth = 1;

        string[] names = { "低", "中", "高" };
        for (int i = 0; i < 3; i++)
        {
            var b = MakeButton(row.transform, names[i], font);
            b.name = "Btn_" + i;
            b.gameObject.AddComponent<LayoutElement>().preferredWidth = 84;
        }
    }

    static void BuildLangRow(Transform parent, Font font)
    {
        var row = MakeGO("Row_Lang", parent);
        var rlg = row.AddComponent<HorizontalLayoutGroup>();
        rlg.spacing = 14; rlg.childAlignment = TextAnchor.MiddleLeft;
        rlg.childControlWidth = true; rlg.childControlHeight = true;
        rlg.childForceExpandWidth = false; rlg.childForceExpandHeight = true;
        var rle = row.AddComponent<LayoutElement>();
        rle.preferredHeight = 64; rle.flexibleWidth = 1;

        var txt = MakeText(row.transform, "语音", 34, font, new Color(0.35f, 0.30f, 0.22f));
        txt.alignment = TextAnchor.MiddleLeft;
        txt.gameObject.AddComponent<LayoutElement>().preferredWidth = 200;

        var spacer = MakeGO("Spacer", row.transform);
        spacer.AddComponent<LayoutElement>().flexibleWidth = 1;

        var b = MakeButton(row.transform, "简体中文 ▾", font);
        b.name = "LangBtn";
        b.gameObject.AddComponent<LayoutElement>().preferredWidth = 220;
    }
}
#endif
