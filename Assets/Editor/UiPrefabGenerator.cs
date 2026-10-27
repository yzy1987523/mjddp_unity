using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.IO;

#if UNITY_EDITOR
/// <summary>
/// 一键生成其余 UI 面板预制体到 Assets/Resources/Prefab/：
///   MenuPanel / PausePanel / ResultPanel / LevelPanel / CodexPanel
/// 各预制体含完整 UI 层级（动态内容的面板只含容器，由对应组件运行时填充）。
/// 使用：Unity 菜单 Tools / 生成 UI 预制体。
/// </summary>
public class UiPrefabGenerator
{
    [MenuItem("Tools/生成 UI 预制体")]
    public static void GenerateAll()
    {
        if (!Directory.Exists("Assets/Resources/Prefab")) Directory.CreateDirectory("Assets/Resources/Prefab");
        Save(BuildMenu(), "Assets/Resources/Prefab/MenuPanel.prefab");
        Save(BuildPause(), "Assets/Resources/Prefab/PausePanel.prefab");
        Save(BuildResult(), "Assets/Resources/Prefab/ResultPanel.prefab");
        Save(BuildLevel(), "Assets/Resources/Prefab/LevelPanel.prefab");
        Save(BuildCodex(), "Assets/Resources/Prefab/CodexPanel.prefab");
        Save(BuildLevelItem(), "Assets/Resources/Prefab/LevelItem.prefab");
        Save(BuildCodexItem(), "Assets/Resources/Prefab/CodexItem.prefab");
        Save(BuildGame(), "Assets/Resources/Prefab/GamePanel.prefab");
        AssetDatabase.SaveAssets();
        Debug.Log("[UI] 已生成 Menu/Pause/Result/Level/Codex/Game 预制体到 Resources/Prefab");
    }

    static void Save(GameObject root, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
    }

    static GameObject BuildMenu()
    {
        var root = UiTools.MakePanel("MenuPanel");
        root.AddComponent<MenuPanel>();
        UiTools.AddText(root.transform, "麻将连连看", 64, Color.white);
        var b1 = UiTools.AddButton(root.transform, "开始游戏"); b1.name = "Btn_Start";
        var b2 = UiTools.AddButton(root.transform, "选择关卡"); b2.name = "Btn_Levels";
        var b3 = UiTools.AddButton(root.transform, "设置"); b3.name = "Btn_Settings";
        return root;
    }

    static GameObject BuildPause()
    {
        var root = UiTools.MakePanel("PausePanel");
        root.AddComponent<PausePanel>();
        UiTools.AddText(root.transform, "暂停", 56, Color.white);
        var b1 = UiTools.AddButton(root.transform, "继续"); b1.name = "Btn_Resume";
        var b2 = UiTools.AddButton(root.transform, "重开本关"); b2.name = "Btn_Retry";
        var b3 = UiTools.AddButton(root.transform, "返回主菜单"); b3.name = "Btn_Menu";
        return root;
    }

    static GameObject BuildResult()
    {
        var root = UiTools.MakePanel("ResultPanel");
        root.AddComponent<ResultPanel>();
        UiTools.AddText(root.transform, "", 56, Color.white).name = "Title";
        UiTools.AddText(root.transform, "", 32, Color.white).name = "Info";
        var b1 = UiTools.AddButton(root.transform, "下一关"); b1.name = "Btn_Next";
        var b2 = UiTools.AddButton(root.transform, "重玩"); b2.name = "Btn_Retry";
        var b3 = UiTools.AddButton(root.transform, "主菜单"); b3.name = "Btn_Menu";
        return root;
    }

    static GameObject BuildLevel()
    {
        var root = UiTools.MakePanel("LevelPanel");
        root.AddComponent<LevelSelectPanel>();
        UiTools.AddText(root.transform, "选择关卡", 56, Color.white);
        // 关卡列表容器（运行时由 LevelSelectPanel.BuildList 填充）
        var list = UiTools.MakeGO("LevelList", root.transform);
        var llg = list.AddComponent<VerticalLayoutGroup>();
        llg.spacing = 12; llg.childAlignment = TextAnchor.MiddleCenter;
        llg.padding = new RectOffset(40, 40, 10, 10);
        llg.childControlWidth = true; llg.childControlHeight = true;
        llg.childForceExpandWidth = true; llg.childForceExpandHeight = false;
        var le = list.AddComponent<LayoutElement>();
        le.minHeight = 200; le.preferredHeight = 600; le.flexibleWidth = 1;
        var back = UiTools.AddButton(root.transform, "返回"); back.name = "Btn_Back";
        return root;
    }

    static GameObject BuildCodex()
    {
        var root = UiTools.MakePanel("CodexPanel");
        root.AddComponent<CodexPanel>();

        var title = UiTools.MakeGO("CodexTitle", root.transform);
        var ttr = title.GetComponent<RectTransform>();
        ttr.anchorMin = new Vector2(0, 1); ttr.anchorMax = new Vector2(1, 1); ttr.pivot = new Vector2(0.5f, 1);
        ttr.sizeDelta = new Vector2(0, 80); ttr.anchoredPosition = new Vector2(0, -40);
        UiTools.AddText(title.transform, "图鉴", 48, Color.white);

        var scroll = UiTools.MakeGO("CodexScroll", root.transform);
        var str = scroll.GetComponent<RectTransform>();
        str.anchorMin = new Vector2(0, 0); str.anchorMax = new Vector2(1, 1);
        str.offsetMin = new Vector2(30, 110); str.offsetMax = new Vector2(-30, -110);
        var sr = scroll.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true;

        var viewport = UiTools.MakeGO("Viewport", scroll.transform);
        var vtr = viewport.GetComponent<RectTransform>();
        vtr.anchorMin = Vector2.zero; vtr.anchorMax = Vector2.one; vtr.offsetMin = Vector2.zero; vtr.offsetMax = Vector2.zero;
        var vimg = viewport.AddComponent<Image>(); vimg.color = Color.white;
        viewport.AddComponent<Mask>().showMaskGraphic = true;

        var content = UiTools.MakeGO("Content", viewport.transform);
        var ctr = content.GetComponent<RectTransform>();
        ctr.anchorMin = new Vector2(0, 1); ctr.anchorMax = new Vector2(1, 1); ctr.pivot = new Vector2(0.5f, 1);
        ctr.sizeDelta = new Vector2(0, 0);
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8; vlg.padding = new RectOffset(10, 10, 10, 10); vlg.childAlignment = TextAnchor.UpperCenter;
        sr.content = ctr; sr.viewport = vtr;

        var closeRow = UiTools.MakeGO("CloseRow", root.transform);
        var crtr = closeRow.GetComponent<RectTransform>();
        crtr.anchorMin = new Vector2(0, 0); crtr.anchorMax = new Vector2(1, 0); crtr.pivot = new Vector2(0.5f, 0);
        crtr.sizeDelta = new Vector2(0, 100); crtr.anchoredPosition = new Vector2(0, 50);
        var cb = UiTools.AddButton(closeRow.transform, "关闭"); cb.name = "CloseBtn";
        return root;
    }

    // 关卡列表项：根即 Button（背景 Image）+ 子 Text，运行时按数据 Bind
    static GameObject BuildLevelItem()
    {
        var root = new GameObject("LevelItem", typeof(RectTransform));
        var img = root.AddComponent<Image>();
        img.color = new Color(0.20f, 0.50f, 0.82f);
        var btn = root.AddComponent<Button>();
        btn.targetGraphic = img;
        var le = root.AddComponent<LayoutElement>();
        le.minHeight = 64; le.preferredHeight = 64; le.flexibleWidth = 1;
        root.AddComponent<LevelItem>();
        UiTools.AddText(root.transform, "关卡", 26, Color.white);
        return root;
    }

    // 图鉴行项：根（HLG）+ Icon(Image) + Txt(Text) + Color(Image)，运行时按数据 Bind
    static GameObject BuildCodexItem()
    {
        var root = new GameObject("CodexItem", typeof(RectTransform));
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 64);
        var hlg = root.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12; hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        root.AddComponent<CodexItem>();

        var icon = UiTools.MakeGO("Icon", root.transform);
        icon.GetComponent<RectTransform>().sizeDelta = new Vector2(48, 48);
        icon.AddComponent<Image>().preserveAspect = true;

        var txt = UiTools.MakeGO("Txt", root.transform);
        txt.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 48);
        var t = txt.AddComponent<Text>();
        t.text = "x0  Lv0"; t.fontSize = 24; t.alignment = TextAnchor.MiddleLeft;
        if (UiTools.LoadFont() != null) t.font = UiTools.LoadFont();

        var col = UiTools.MakeGO("Color", root.transform);
        col.GetComponent<RectTransform>().sizeDelta = new Vector2(48, 48);
        col.AddComponent<Image>().preserveAspect = true;
        return root;
    }

    // 游戏界面（HUD）：顶栏 + 底栏 + 棋盘容器 + Toast，按钮/文本命名匹配 GameController 引用
    [MenuItem("Tools/生成游戏界面预制体")]
    public static void GenerateGame()
    {
        if (!Directory.Exists("Assets/Resources/Prefab")) Directory.CreateDirectory("Assets/Resources/Prefab");
        Save(BuildGame(), "Assets/Resources/Prefab/GamePanel.prefab");
        AssetDatabase.SaveAssets();
        Debug.Log("[UI] 已生成 GamePanel.prefab 到 Resources/Prefab");
    }

    // 广告提示框：提示/洗牌次数用尽时弹出，看广告可 +1 次。确认按钮调用广告 SDK（占位）
    [MenuItem("Tools/生成广告提示框预制体")]
    public static void GenerateAdPrompt()
    {
        if (!Directory.Exists("Assets/Resources/Prefab")) Directory.CreateDirectory("Assets/Resources/Prefab");
        Save(BuildAdPrompt(), "Assets/Resources/Prefab/AdPromptPanel.prefab");
        AssetDatabase.SaveAssets();
        Debug.Log("[UI] 已生成 AdPromptPanel.prefab 到 Resources/Prefab");
    }

    static GameObject BuildAdPrompt()
    {
        var root = UiTools.MakePanel("AdPromptPanel");
        root.AddComponent<AdPromptPanel>();
        UiTools.AddText(root.transform, "", 48, Color.white).name = "Title";
        UiTools.AddText(root.transform, "", 30, Color.white).name = "Info";
        var b1 = UiTools.AddButton(root.transform, "确认观看广告"); b1.name = "Btn_Confirm";
        var b2 = UiTools.AddButton(root.transform, "返回"); b2.name = "Btn_Cancel";
        return root;
    }

    static GameObject BuildGame()
    {
        var root = UiTools.MakePanel("GamePanel");
        root.AddComponent<GamePanel>();
        var gp = root.GetComponent<GamePanel>();

        // 顶栏：分数 / 最高分 / 局数 / 暂停 / 图鉴
        var top = UiTools.MakeGO("TopBar", root.transform);
        var ttr = top.GetComponent<RectTransform>();
        ttr.anchorMin = new Vector2(0, 1); ttr.anchorMax = new Vector2(1, 1); ttr.pivot = new Vector2(0.5f, 1);
        ttr.sizeDelta = new Vector2(0, 80); ttr.anchoredPosition = new Vector2(0, -40);
        var topLg = top.AddComponent<HorizontalLayoutGroup>();
        topLg.padding = new RectOffset(16, 16, 8, 8); topLg.spacing = 24; topLg.childAlignment = TextAnchor.MiddleCenter;
        var score = UiTools.AddText(top.transform, "分数 0", 28, Color.white); score.name = "ScoreLabel";
        var best = UiTools.AddText(top.transform, "最高 0", 28, Color.white); best.name = "BestLabel";
        var round = UiTools.AddText(top.transform, "第 1 局", 28, Color.white); round.name = "RoundLabel";
        foreach (var t in new[] { score, best, round }) { var le = t.gameObject.AddComponent<LayoutElement>(); le.flexibleWidth = 1; }
        var pause = UiTools.AddButton(top.transform, "暂停"); pause.name = "PauseBtn";
        var codex = UiTools.AddButton(top.transform, "图鉴"); codex.name = "CodexBtn";
        foreach (var b in new[] { pause, codex }) { var le = b.gameObject.AddComponent<LayoutElement>(); le.minHeight = 64; le.flexibleWidth = 1; }

        // 底栏：提示 / 洗牌 / 重开 / 层偏移滑条（调试）
        var bottom = UiTools.MakeGO("BottomBar", root.transform);
        var btr = bottom.GetComponent<RectTransform>();
        btr.anchorMin = new Vector2(0, 0); btr.anchorMax = new Vector2(1, 0); btr.pivot = new Vector2(0.5f, 0);
        btr.sizeDelta = new Vector2(0, 96); btr.anchoredPosition = new Vector2(0, 48);
        var botLg = bottom.AddComponent<HorizontalLayoutGroup>();
        botLg.padding = new RectOffset(16, 16, 8, 8); botLg.spacing = 16; botLg.childAlignment = TextAnchor.MiddleCenter; botLg.childForceExpandWidth = true;
        var hint = UiTools.AddButton(bottom.transform, "提示(5)"); hint.name = "HintBtn";
        var shuffle = UiTools.AddButton(bottom.transform, "洗牌"); shuffle.name = "ShuffleBtn";
        var restart = UiTools.AddButton(bottom.transform, "重开"); restart.name = "RestartBtn";
        foreach (var b in new[] { hint, shuffle, restart }) { var le = b.gameObject.AddComponent<LayoutElement>(); le.flexibleWidth = 1; le.minHeight = 64; }
        var xLabel = UiTools.AddText(bottom.transform, "X层偏移", 22, Color.white); xLabel.name = "XLabel";
        var xs = UiTools.MakeGO("XOffsetSlider", bottom.transform); var xsSlider = xs.AddComponent<Slider>(); xsSlider.minValue = -40; xsSlider.maxValue = 40; xsSlider.value = 0;
        var xle = xs.AddComponent<LayoutElement>(); xle.minHeight = 36; xle.preferredWidth = 240; xle.flexibleWidth = 1;
        var yLabel = UiTools.AddText(bottom.transform, "Y层偏移", 22, Color.white); yLabel.name = "YLabel";
        var ys = UiTools.MakeGO("YOffsetSlider", bottom.transform); var ysSlider = ys.AddComponent<Slider>(); ysSlider.minValue = -40; ysSlider.maxValue = 40; ysSlider.value = 0;
        var yle = ys.AddComponent<LayoutElement>(); yle.minHeight = 36; yle.preferredWidth = 240; yle.flexibleWidth = 1;

        // 棋盘容器
        var br = UiTools.MakeGO("BoardRoot", root.transform);
        var brtr = br.GetComponent<RectTransform>();
        brtr.anchorMin = brtr.anchorMax = new Vector2(0.5f, 0.5f); brtr.pivot = new Vector2(0.5f, 0.5f);

        // 居中提示文字
        var toast = UiTools.MakeGO("Toast", root.transform);
        var tts = toast.GetComponent<RectTransform>();
        tts.anchorMin = tts.anchorMax = new Vector2(0.5f, 0.5f); tts.pivot = new Vector2(0.5f, 0.5f); tts.sizeDelta = new Vector2(420, 90);
        var toastText = UiTools.AddText(toast.transform, "", 34, Color.white); toastText.name = "ToastText";

        // 绑定到 GamePanel 组件字段（SaveAsPrefabAsset 会保存这些引用）
        gp.topBar = top.GetComponent<RectTransform>();
        gp.bottomBar = bottom.GetComponent<RectTransform>();
        gp.boardRoot = br.GetComponent<RectTransform>();
        gp.toast = toast.GetComponent<RectTransform>();
        gp.scoreLabel = score; gp.bestLabel = best; gp.roundLabel = round; gp.toastText = toastText;
        gp.hintBtn = hint; gp.shuffleBtn = shuffle; gp.restartBtn = restart;
        gp.pauseBtn = pause; gp.codexBtn = codex;
        gp.xOffsetSlider = xsSlider; gp.yOffsetSlider = ysSlider;
        gp.xLabel = xLabel; gp.yLabel = yLabel;

        return root;
    }

    // 只生成 Item 预制体（关卡项 / 图鉴行），不触碰面板预制体，避免覆盖你在编辑器里已调好的面板
    [MenuItem("Tools/生成 Item 预制体")]
    public static void GenerateItems()
    {
        if (!Directory.Exists("Assets/Resources/Prefab")) Directory.CreateDirectory("Assets/Resources/Prefab");
        Save(BuildLevelItem(), "Assets/Resources/Prefab/LevelItem.prefab");
        Save(BuildCodexItem(), "Assets/Resources/Prefab/CodexItem.prefab");
        AssetDatabase.SaveAssets();
        Debug.Log("[UI] 已生成 LevelItem/CodexItem 预制体到 Resources/Prefab（未覆盖面板）");
    }
}
#endif
