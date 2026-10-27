using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// 完整游戏主控制器（Godot game_session.gd 的 C# 移植）。
/// 无限随机模式：随机生成可解牌阵，全消即过关并自动进下一局，分数跨局累加。
/// 不限时；提示（限次）高亮一对可消牌；死局弹提示，玩家可洗牌或重开；也可手动洗牌。
/// 最高分持久化。所有 UI 在运行时构建，场景文件无需引用本脚本。
/// </summary>
public class GameController : MonoBehaviour
{
    const float COMBO_SEC = 3.0f;
    const int HINTS_PER_ROUND = 5;
    const int ROUND_BONUS = 500;
    const int BASE_SCORE = 100;

    private GameBoard _board;
    private Dictionary<string, TileView> _tiles = new Dictionary<string, TileView>();

    private GameObject _tilePrefab;
    private Sprite _cardFront;
    private Sprite[] _levelBacks = new Sprite[4];   // 等级0~3 对应的背面图（Resources/CardPanel/Level0..3）
    private Dictionary<int, Sprite> _faceSprites = new Dictionary<int, Sprite>();
    private LayoutData _layoutData;
    private ConfigData _cfg = new ConfigData();

    private int _score;
    private int _best;
    private int _round;
    private int _combo;
    private float _lastMatchTime;
    private int _hints;
    private int _shuffles;
    private bool _busy;

    private Text _scoreLabel;
    private Text _bestLabel;
    private Text _roundLabel;
    private Text _toast;
    private GameObject _toastGO;
    private Button _hintBtn;
    private Button _shuffleBtn;
    private Transform _uiRoot;
    private GameObject _deadlockOverlay;
    private GameState _codexReturnState = GameState.Playing;
    private GameState _settingsReturnState = GameState.Playing;
    private GameObject _fade;
    private CanvasGroup _fadeCg;
    private bool _transitioning;
    private Button _restartBtn;
    private RectTransform _boardRoot;
    private bool _boardFromPrefab = false;
    private Font _font;

    private float _layerDx;
    private float _layerDy;
    private Slider _xOffsetSlider; // 调试：X 轴层偏移滑条
    private Slider _yOffsetSlider; // 调试：Y 轴层偏移滑条
    private const float X_OFFSET_MIN = -40f, X_OFFSET_MAX = 40f;
    private const float Y_OFFSET_MIN = -40f, Y_OFFSET_MAX = 40f;
    private string _firstPick = "";                 // 当前选中的第一张牌 id
    private Vector2 _asideOffset = new Vector2(40, 40); // 保留（兼容）
    private List<(TileOut t, GameObject go)> _placedView = new List<(TileOut t, GameObject go)>();

    // 图鉴 / 对象池 / 音频
    private int[] _codexCollected = new int[0];      // 每种花色收集数（按 faceIndex）
    private Button _codexBtn;
    private GameObject _codexPanel;
    private AudioSource _audio;
    private AudioClip _peng;
    private AudioClip _bgm;
    private AudioClip _fail;
    private AudioClip _select0;
    private AudioClip _select1;
    private AudioSource _bgmSrc;     // 背景音乐专用音源（与音效分开，便于独立开关）
    private SaveManager.SettingsData _settings;  // 设置数据（音乐/音效/震动/画质/语言）
    private GameObject _settingsPanel;
    private float _flipDur = 0.5f;   // 正反面翻转动画总时长（来自配置 flip_dur）
    private List<TileView> _pool = new List<TileView>(); // 牌对象池
    private static readonly Color[] CODE_COLORS = {
        new Color(0.95f, 0.95f, 0.95f), // Lv0 白
        new Color(0.40f, 0.85f, 0.40f), // Lv1 绿
        new Color(0.40f, 0.60f, 1.00f), // Lv2 蓝
        new Color(1.00f, 0.80f, 0.20f), // Lv3 金
    };

    // 关卡 / 状态机
    private enum GameState { Menu, LevelSelect, Playing, Paused, Result }
    private GameState _state = GameState.Menu;
    private LevelEntry[] _levels = new LevelEntry[0];
    private int _currentLevel = 0;
    private GameObject _topBar, _bottomBar;
    private GameObject _menuPanel, _levelPanel, _pausePanel, _resultPanel, _adPromptPanel;
    private Button _pauseBtn;
    // 面板组件引用（加载预制体后绑定）
    private MenuPanel _menuPanelC;
    private LevelSelectPanel _levelPanelC;
    private PausePanel _pausePanelC;
    private ResultPanel _resultPanelC;
    private CodexPanel _codexPanelC;
    private AdPromptPanel _adPromptPanelC;

    private float _nativeW;
    private float _nativeH;
    private int _lastScreenW;
    private int _lastScreenH;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Object.FindFirstObjectByType<GameController>() != null) return;
        var go = new GameObject("GameController");
        go.AddComponent<GameController>();
    }

    // Unity 6 废弃了内置字体名 "Arial.ttf"，改用 "LegacyRuntime.ttf"；旧版本回退 Arial；
    // 两者都失败则返回 null（Text 会使用引擎默认字体，不崩溃）。
    private static Font LoadBuiltinFont()
    {
        try { return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        try { return Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        return null;
    }

    private void Start()
    {
        _font = LoadBuiltinFont();
        LoadResources(); // 先于 BuildUI：让 _cfg 在 BuildUI 建滑条时可用
        _settings = SaveManager.LoadSettings(); // 提前加载：BuildUI 建设置面板时需读取开关状态
        BuildUI();
        _best = SaveManager.LoadBest();
        _codexCollected = SaveManager.LoadCodex();
        EnsureCodexSize();
        _audio = gameObject.AddComponent<AudioSource>();  // 音效音源
        _bgmSrc = gameObject.AddComponent<AudioSource>(); // 背景音乐音源（独立开关）
        ApplyAudioSettings();
        ApplyQualitySetting();
        ShowMenu();
    }

    private void Update()
    {
        if (_state == GameState.Playing && (Screen.width != _lastScreenW || Screen.height != _lastScreenH))
            FitBoard();
    }

    // ---------- 资源加载 ----------

    // 资源均按 multi-sprite 图集导入，Resources.Load<Sprite> 对多精灵资产会返回 null，
    // 必须用 LoadAll<Sprite> 取子精灵（每张图仅一个子精灵，取 [0]）。
    private Sprite LoadFirstSprite(string path)
    {
        var all = Resources.LoadAll<Sprite>(path);
        return (all != null && all.Length > 0) ? all[0] : null;
    }

    private void LoadResources()
    {
        _cardFront = LoadFirstSprite("Card/card_front");
        _faceSprites.Clear();
        for (int i = 1; i <= 36; i++)
        {
            _faceSprites[i - 1] = LoadFirstSprite("Card/icon_" + i.ToString("D3"));
        }

        LoadLevels();

        // 等级背面图：Resources/CardPanel/Level0..3.png（不同等级花色用不同背面）
        for (int i = 0; i < _levelBacks.Length; i++)
        {
            var s = Resources.Load<Sprite>("CardPanel/Level" + i);
            if (s != null) _levelBacks[i] = s;
        }

        var cfgAsset = Resources.Load<TextAsset>("ui_config");
        if (cfgAsset != null)
        {
            try
            {
                string txt = cfgAsset.text;
                // Unity JsonUtility 对 UTF-8 BOM（\uFEFF）敏感，开头有它即报 Invalid value，先剥离
                if (!string.IsNullOrEmpty(txt) && txt[0] == '\uFEFF') txt = txt.Substring(1);
                _cfg = JsonUtility.FromJson<ConfigData>(txt);
            }
            catch (System.Exception e)
            {
                // Unity JsonUtility 在 JSON 语法错误（如文件含 UTF-8 BOM、// 注释、尾随逗号、true/TRUE 大小写）时会抛异常
                Debug.LogError("[CFG] ui_config.json 解析失败：" + e.Message + "（请检查是否含 BOM/注释/尾随逗号，已退回默认配置）");
                _cfg = new ConfigData();
            }
            if (_cfg == null) Debug.LogWarning("[CFG] ui_config.json 解析失败（检查 JSON 语法，如尾随逗号/缺引号），已退回默认配置");
        }
        else Debug.LogWarning("[CFG] 未找到 Resources/ui_config，已退回默认配置");

        _peng = Resources.Load<AudioClip>("audio/effect/peng"); // 修正路径：资源在 Resources/audio/effect/peng.ogg
        _bgm = Resources.Load<AudioClip>("audio/bgm/bgm_0");     // 背景音乐：Resources/audio/bgm/bgm_0.ogg

        _fail = Resources.Load<AudioClip>("audio/effect/fail");     // 点击无效牌
        _select0 = Resources.Load<AudioClip>("audio/effect/select0"); // 选中有效牌
        _select1 = Resources.Load<AudioClip>("audio/effect/select1"); // 配对成功的第二张

        _flipDur = (_cfg != null) ? _cfg.flip_dur : 0.5f; // 翻转用时（配置表 flip_dur）

        _tilePrefab = Resources.Load<GameObject>("Prefab/TilePrefab");
    }

    private Sprite GetFaceSprite(int idx) => (_faceSprites.ContainsKey(idx) ? _faceSprites[idx] : null);

    // ---------- UI 构建 ----------

    private GameObject MakeGO(string name, Transform parent)
    {
        // 所有 MakeGO 创建的节点最终都作为 Canvas/RectTransform 的子节点并需要直接
        // 操作 RectTransform（锚点/尺寸/偏移），因此创建时即附带 RectTransform，
        // 否则 TopBar/BottomBar/BoardRoot/Toast/tileGO 等容器节点 GetComponent 会返回 null 崩溃。
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private Text AddText(GameObject parent, string txt, int fontSize)
    {
        var go = MakeGO("Text", parent.transform);
        var t = go.AddComponent<Text>();
        t.text = txt;
        t.fontSize = fontSize;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        if (_font != null) t.font = _font;
        var tr = go.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;
        return t;
    }

    private Button AddButton(GameObject parent, string label)
    {
        var go = MakeGO("Button", parent.transform);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.20f, 0.50f, 0.82f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var txt = MakeGO("Text", go.transform);
        var t = txt.AddComponent<Text>();
        t.text = label;
        t.fontSize = 26;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        if (_font != null) t.font = _font;
        var tr = txt.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;
        return btn;
    }

    private void BuildUI()
    {
        // Canvas
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        GameObject canvasGO;
        if (canvas == null)
        {
            canvasGO = new GameObject("GameCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
        }
        else
        {
            canvasGO = canvas.gameObject;
        }
        Transform canvasTr = canvasGO.transform;

        // EventSystem（新输入系统）
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        if (!LoadGamePrefab(canvasTr))
        {
        // 顶栏：分数 / 最高分 / 局数
        var top = MakeGO("TopBar", canvasTr);
        _topBar = top;
        var ttr = top.GetComponent<RectTransform>();
        ttr.anchorMin = new Vector2(0, 1);
        ttr.anchorMax = new Vector2(1, 1);
        ttr.pivot = new Vector2(0.5f, 1);
        ttr.sizeDelta = new Vector2(0, 80);
        ttr.anchoredPosition = new Vector2(0, -40);
        var topLg = top.AddComponent<HorizontalLayoutGroup>();
        topLg.padding = new RectOffset(16, 16, 8, 8);
        topLg.spacing = 24;
        topLg.childAlignment = TextAnchor.MiddleCenter;
        _scoreLabel = AddText(top, "分数 0", 28);
        _bestLabel = AddText(top, "最高 0", 28);
        _roundLabel = AddText(top, "第 1 局", 28);
        foreach (var t in new[] { _scoreLabel, _bestLabel, _roundLabel })
        {
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
        }

        // 底栏：提示 / 洗牌 / 重开
        var bottom = MakeGO("BottomBar", canvasTr);
        _bottomBar = bottom;
        var btr = bottom.GetComponent<RectTransform>();
        btr.anchorMin = new Vector2(0, 0);
        btr.anchorMax = new Vector2(1, 0);
        btr.pivot = new Vector2(0.5f, 0);
        btr.sizeDelta = new Vector2(0, 96);
        btr.anchoredPosition = new Vector2(0, 48);
        var botLg = bottom.AddComponent<HorizontalLayoutGroup>();
        botLg.padding = new RectOffset(16, 16, 8, 8);
        botLg.spacing = 16;
        botLg.childAlignment = TextAnchor.MiddleCenter;
        botLg.childForceExpandWidth = true;
        _hintBtn = AddButton(bottom, "提示(5)");
        _shuffleBtn = AddButton(bottom, "洗牌");
        _restartBtn = AddButton(bottom, "重开");
        foreach (var b in new[] { _hintBtn, _shuffleBtn, _restartBtn })
        {
            var le = b.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            le.minHeight = 64;
        }
        _hintBtn.onClick.AddListener(OnHint);
        _shuffleBtn.onClick.AddListener(OnShuffle);
        _restartBtn.onClick.AddListener(OnRestart);

        // X 轴层偏移滑条（调试用：实时重排，观察 2.5D 堆叠水平错位）
        var xLabel = AddText(bottom, "X层偏移", 22);
        float x0 = (_cfg != null) ? _cfg.layer_dx : 0f;
        _xOffsetSlider = AddSlider(bottom.transform, "XOffsetSlider", X_OFFSET_MIN, X_OFFSET_MAX, x0, v =>
        {
            _layerDx = v;
            LayoutTiles();
        });
        var xle = _xOffsetSlider.gameObject.AddComponent<LayoutElement>();
        xle.minHeight = 36; xle.preferredWidth = 240; xle.flexibleWidth = 1;

        // Y 轴层偏移滑条（调试用：实时重排，观察 2.5D 堆叠高度）
        var yLabel = AddText(bottom, "Y层偏移", 22);
        float y0 = (_cfg != null) ? _cfg.layer_dy : 0f;
        _yOffsetSlider = AddSlider(bottom.transform, "YOffsetSlider", Y_OFFSET_MIN, Y_OFFSET_MAX, y0, v =>
        {
            _layerDy = v;
            LayoutTiles();
        });
        var yle = _yOffsetSlider.gameObject.AddComponent<LayoutElement>();
        yle.minHeight = 36; yle.preferredWidth = 240; yle.flexibleWidth = 1;

        // 调试滑条显隐：由配置 show_offset_sliders 统一控制（默认 false 不显示）
        bool showSliders = (_cfg != null) ? _cfg.show_offset_sliders : false;
        if (_xOffsetSlider != null) _xOffsetSlider.gameObject.SetActive(showSliders);
        if (xLabel != null) xLabel.gameObject.SetActive(showSliders);
        if (_yOffsetSlider != null) _yOffsetSlider.gameObject.SetActive(showSliders);
        if (yLabel != null) yLabel.gameObject.SetActive(showSliders);

        // 牌盘容器
        var br = MakeGO("BoardRoot", canvasTr);
        var brtr = br.GetComponent<RectTransform>();
        brtr.anchorMin = brtr.anchorMax = new Vector2(0.5f, 0.5f);
        brtr.pivot = new Vector2(0.5f, 0.5f);
        _boardRoot = br.GetComponent<RectTransform>();

        // 居中提示文字
        _toastGO = MakeGO("Toast", canvasTr);
        var tts = _toastGO.GetComponent<RectTransform>();
        tts.anchorMin = tts.anchorMax = new Vector2(0.5f, 0.5f);
        tts.pivot = new Vector2(0.5f, 0.5f);
        tts.sizeDelta = new Vector2(420, 90);
        _toast = AddText(_toastGO, "", 34);
        _toastGO.SetActive(false);

        // 游戏中暂停按钮（顶栏右侧）
        _pauseBtn = AddButton(top, "暂停");
        var pLe = _pauseBtn.gameObject.AddComponent<LayoutElement>();
        pLe.minHeight = 64; pLe.flexibleWidth = 1;
        _pauseBtn.onClick.AddListener(OnPause);

        _codexBtn = AddButton(top, "图鉴");
        var cLe = _codexBtn.gameObject.AddComponent<LayoutElement>();
        cLe.minHeight = 64; cLe.flexibleWidth = 1;
        _codexBtn.onClick.AddListener(ShowCodex);
        }

        BuildPanels(canvasTr);
        SetGameUiVisible(false);
        }

    // 加载游戏界面（HUD）预制体；成功返回 true，缺失/无组件时返回 false（调用方回退到运行时 MakeGO）
    private bool LoadGamePrefab(Transform parent)
    {
        var prefab = Resources.Load<GameObject>("Prefab/GamePanel");
        if (prefab == null) return false;
        var go = Instantiate(prefab, parent);
        var gp = go.GetComponent<GamePanel>();
        if (gp == null) { Destroy(go); return false; }
        _topBar = gp.topBar != null ? gp.topBar.gameObject : null;
        _bottomBar = gp.bottomBar != null ? gp.bottomBar.gameObject : null;
        _boardRoot = gp.boardRoot;
        _boardFromPrefab = true;
        _toastGO = gp.toast != null ? gp.toast.gameObject : null;
        _toast = gp.toastText;
        _scoreLabel = gp.scoreLabel; _bestLabel = gp.bestLabel; _roundLabel = gp.roundLabel;
        _hintBtn = gp.hintBtn; _shuffleBtn = gp.shuffleBtn; _restartBtn = gp.restartBtn;
        _pauseBtn = gp.pauseBtn; _codexBtn = gp.codexBtn;
        _xOffsetSlider = gp.xOffsetSlider; _yOffsetSlider = gp.yOffsetSlider;
        float x0 = (_cfg != null) ? _cfg.layer_dx : 0f;
        float y0 = (_cfg != null) ? _cfg.layer_dy : 0f;
        bool showSliders = (_cfg != null) ? _cfg.show_offset_sliders : false;
        gp.Init(OnHint, OnShuffle, OnRestart, OnPause, ShowCodex,
                X_OFFSET_MIN, X_OFFSET_MAX, x0, v => { _layerDx = v; LayoutTiles(); },
                Y_OFFSET_MIN, Y_OFFSET_MAX, y0, v => { _layerDy = v; LayoutTiles(); },
                showSliders);
        // 游戏内新增按钮：设置、返回主菜单（带黑屏过渡）。优先用预制体字段，缺失时按名字递归查找兜底
        var settingsBtn = gp.settingsBtn ?? FindChildButton(go.transform, "SettingsBtn");
        var backBtn = gp.backBtn ?? FindChildButton(go.transform, "BackBtn");
        if (settingsBtn != null) settingsBtn.onClick.AddListener(ShowSettings);
        if (backBtn != null) backBtn.onClick.AddListener(() => DoTransition(ShowMenu));
        return true;
    }

    // 递归按名字查找子物体上的 Button（用于绑定手动添加到预制体、但未拖入组件字段的按钮）
    private static Button FindChildButton(Transform root, string name)
    {
        foreach (Transform c in root)
        {
            if (c.name == name)
            {
                var b = c.GetComponent<Button>();
                if (b != null) return b;
            }
            var d = FindChildButton(c, name);
            if (d != null) return d;
        }
        return null;
    }

    // ---------- 局流程 ----------

    private void StartRound()
    {
        PlayBgm(); // 游戏开始播放背景音乐
        _busy = false; // 新一局开始，清除通关/结算时的忙碌锁，恢复输入
        _round = 1;
        _hints = 5 + _currentLevel / 5;
        _shuffles = 5 + _currentLevel / 10;
        _combo = 0;
        BuildBoard();
        UpdateHud();
        FitBoard();
        ShowToast(_levels != null && _currentLevel >= 0 && _currentLevel < _levels.Length ? _levels[_currentLevel].name : "第 1 局");
    }

    private void BuildBoard()
    {
        if (_layoutData == null || _layoutData.tiles == null || _layoutData.tiles.Length == 0)
        {
            Debug.LogError("GameController: 布局数据为空，无法生成牌阵");
            return;
        }

        // 回收所有现有牌到对象池（不销毁实例）
        foreach (var kv in _tiles) ReleaseTile(kv.Value);
        _tiles.Clear();
        _placedView.Clear();

        // 2.5D 层偏移取自配置（默认 x=0 纯垂直、y=5.5 每层上抬），不再运行时可调
        _layerDx = (_cfg != null) ? _cfg.layer_dx : 0f;
        _layerDy = (_cfg != null) ? _cfg.layer_dy : 0f;
        _firstPick = "";
        _asideOffset = (_cfg != null) ? new Vector2(_cfg.tile_width * 0.8f, _cfg.tile_height * 0.8f) : new Vector2(40f, 40f);
        if (_xOffsetSlider != null) _xOffsetSlider.value = _layerDx; // 重开本关时滑条回到配置默认值
        if (_yOffsetSlider != null) _yOffsetSlider.value = _layerDy; // 重开本关时滑条回到配置默认值

        float tw = _cfg.tile_width;
        float th = _cfg.tile_height;

        int seed = (_levels != null && _currentLevel >= 0 && _currentLevel < _levels.Length) ? _levels[_currentLevel].seed : Random.Range(0, int.MaxValue);
        var gen = LevelGenerator.Generate(_layoutData, seed);
        if (!gen.ok)
        {
            Debug.LogError("GameController: 生成失败 -> " + gen.error);
            return;
        }

        _board = new GameBoard();
        _board.Setup(gen.geometry, gen.assignments);
        _board.OnSelectionChanged += OnSelectionChanged;
        _board.OnMatched += OnMatched;
        _board.OnBlocked += OnBlocked;
        _board.OnWin += OnWin;
        _board.OnDeadlock += OnDeadlock;

        // 仅创建牌（含预制体 Shadow/Body/Face 与 TileView），定位交给 LayoutTiles
        foreach (var t in gen.tiles)
        {
            TileView tv = GetTile();
            tv.tileId = t.id;
            tv.faceIndex = t.tileType;
            tv.ResetView();
            tv.transform.SetParent(_boardRoot, false);
            var tr = tv.GetComponent<RectTransform>();
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.pivot = new Vector2(0.5f, 0.5f);
            tr.sizeDelta = new Vector2(tw, th);

            // 阴影层：完全按预制体生成（尺寸/锚点/偏移/颜色都由你微调），代码只回填缺失的牌底图
            Image shadowImg = FindDeep(tv.transform, "Shadow")?.GetComponent<Image>();
            if (shadowImg == null)
            {
                shadowImg = MakeGO("Shadow", tv.transform).AddComponent<Image>();
                shadowImg.color = new Color(0f, 0f, 0f, 0.35f);
                shadowImg.preserveAspect = true;
            }
            if (shadowImg.sprite == null) shadowImg.sprite = _cardFront;
            shadowImg.raycastTarget = false;
            // 牌内绘制顺序（Shadow 在最下）由预制体 RootOrder 决定，代码不再强制

            // 牌底：完全由预制体提供，代码只引用、不新建也不覆盖其图片（去掉 bodyImg 的新增逻辑）
            Image bodyImg = FindDeep(tv.transform, "Body (2)")?.GetComponent<Image>();
            if (bodyImg == null) bodyImg = FindDeep(tv.transform, "Body")?.GetComponent<Image>();
            if (bodyImg == null) bodyImg = FindDeep(tv.transform, "Body_1")?.GetComponent<Image>(); // 回退到 Body_1 层
            if (bodyImg != null) bodyImg.raycastTarget = true;

            // 牌面：牌面 sprite 必须按 tileType 绑定（每张不同），其余完全按预制体
            Image faceImg = FindDeep(tv.transform, "Face")?.GetComponent<Image>();
            if (faceImg == null)
            {
                faceImg = MakeGO("Face", tv.transform).AddComponent<Image>();
                faceImg.preserveAspect = true;
            }
            faceImg.sprite = GetFaceSprite(t.tileType);
            faceImg.raycastTarget = false;

            tv.bodyImage = bodyImg;
            tv.faceImage = faceImg;
            tv.backImage = FindDeep(tv.transform, "Back")?.GetComponent<Image>();
            tv.shadowImage = FindDeep(tv.transform, "Shadow")?.GetComponent<Image>();
            tv.point = FindDeep(tv.transform, "Point");
            tv.SetBaseColor(bodyImg != null ? bodyImg.color : Color.white); // 记住预制体底色，选中取消后恢复
            tv.onClick = OnTileClicked;

            // 图鉴等级 -> 等级背面图 levelN（不同等级花色 Back 颜色不同，共 4 种）
            int lvl = LevelOf(t.tileType < _codexCollected.Length ? _codexCollected[t.tileType] : 0);
            Sprite lvlBack = (lvl >= 0 && lvl < _levelBacks.Length) ? _levelBacks[lvl] : null;

            // 记录新增子层引用（翻转时由 TileView.ToggleFace 按状态统一设置 sprite）
            tv.bodySub1 = FindDeep(tv.transform, "Body_1")?.GetComponent<Image>();
            tv.bodySub2 = FindDeep(tv.transform, "Body_2")?.GetComponent<Image>();
            tv.backSub1 = FindDeep(tv.transform, "Back_1")?.GetComponent<Image>();
            tv.backSub2 = FindDeep(tv.transform, "Back_2")?.GetComponent<Image>();

            // 正面牌底图（front）与等级背面图（levelN = backSprite），供 ToggleFace 按状态设置各层
            tv.frontSprite = bodyImg != null ? bodyImg.sprite : null;
            tv.backSprite = lvlBack;

            tv.ToggleFace(true); // 默认正面：body/body_1/body_2=front，back/back_1/back_2=levelN

            // 随机 10% 的牌初始为反面状态（ToggleFace(false) 后 body/body_1/body_2=levelN）
            if (Random.value < 0.1f) tv.SetFaceDownInitial();

            _tiles[t.id] = tv;
            _placedView.Add((t, tv.gameObject));
        }

        LayoutTiles();
    }

    // 按当前层偏移(_layerDx/_layerDy)重排版面：只更新坐标与绘制顺序，不重建牌。
    // 供滑条实时调用，也供 BuildBoard 首次布局调用；高层牌往右上抬形成 2.5D。
    private void LayoutTiles()
    {
        if (_placedView.Count == 0) return;
        float tw = _cfg.tile_width;
        float th = _cfg.tile_height;
        float ux = tw / 2f;
        float uy = th / 2f;
        float dx = _layerDx;
        float dy = _layerDy;

        float minX = float.MaxValue, minY = float.MaxValue;
        var placed = new List<(TileOut t, float px, float py)>();
        foreach (var (t, _) in _placedView)
        {
            float px = t.x * ux + t.layer * dx;
            float py = t.y * uy - t.layer * dy;
            if (px < minX) minX = px;
            if (py < minY) minY = py;
            placed.Add((t, px, py));
        }

        float maxRight = 0, maxBottom = 0;
        foreach (var p in placed)
        {
            float bx = p.px - minX;
            float by = p.py - minY;
            maxRight = Mathf.Max(maxRight, bx + tw);
            maxBottom = Mathf.Max(maxBottom, by + th);
        }
        _nativeW = maxRight;
        _nativeH = maxBottom;

        // 绘制顺序（决定 UGUI 遮挡 / sibling index）：
        // 1) 先按层高 layer 升序 —— 底层先画，高层整体盖在低层之上（2.5D 叠层 z 轴）；
        // 2) 同层内按网格阅读顺序：先从上到下（py 小=屏幕上方先画），再从左到右（px 小先画）。
        //    下方、右侧的牌后画=覆盖在上，因此右下方向的牌压住左上方向牌的阴影（阴影在右下）。
        placed.Sort((a, b) =>
        {
            int la = a.t.layer, lb = b.t.layer;
            if (la != lb) return la.CompareTo(lb);
            float ay = a.py - minY;   // 布局 y（下为正），小=屏幕上方
            float by = b.py - minY;
            if (ay != by) return ay.CompareTo(by); // 上方先画，下方后画 → 下压上
            float ax = a.px - minX;   // 布局 x（右为正），小=左
            float bx = b.px - minX;
            return ax.CompareTo(bx);  // 左侧先画，右侧后画 → 右压左
        });

        int span = placed.Count + 1;
        var layerCounter = new Dictionary<int, int>();
        foreach (var p in placed)
        {
            float bx = p.px - minX;
            float by = p.py - minY;
            float cx = bx + tw / 2f - _nativeW / 2f;
            float cy = -(by + th / 2f) + _nativeH / 2f;
            var go = _tiles[p.t.id].gameObject;
            var tr = go.GetComponent<RectTransform>();
            tr.anchoredPosition = new Vector2(cx, cy);
            var tvh = go.GetComponent<TileView>();
            if (tvh != null) tvh.homePos = new Vector2(cx, cy); // 记录原位供抬起/放回动画
            // 层号越大 → sibling index 越大 → UGUI 越后绘制（覆盖在低层之上）。
            int ord = layerCounter.TryGetValue(p.t.layer, out int o) ? o : 0;
            layerCounter[p.t.layer] = ord + 1;
            tr.SetSiblingIndex(p.t.layer * span + ord);
        }

        FitBoard();
    }

    private void FitBoard()
    {
        _lastScreenW = Screen.width;
        _lastScreenH = Screen.height;
        if (_boardRoot == null) return;

        // 固定缩放 3（不再按屏幕自适应），牌阵整体放大 3 倍。
        // 仅运行时 MakeGO 回退路径需要；从预制体加载的 BoardRoot 尺寸/位置已由用户在编辑器设好，不再覆盖。
        if (!_boardFromPrefab)
        {
            _boardRoot.localScale = new Vector3(3f, 3f, 1f);
            _boardRoot.anchoredPosition = new Vector2(0, (96f - 80f) / 2f);
        }
    }

    // ---------- 事件处理 ----------

    // 点击编排：第一张只变色（不移动）；第二张也只变色；相同则执行碰撞消除；
    // 不同或第二张不可点（不符合规则）则前一张取消变色。
    private void OnTileClicked(string id)
    {
        if (_busy || _state != GameState.Playing || _board == null) return;
        if (!_board.IsFree(id)) { OnPickBlocked(id); return; }
        var tv = _tiles[id];
        // 反面牌：先翻转为正面（0.5s），翻转期间锁输入；完成后再走正常选牌流程
        if (!tv.IsFaceUp())
        {
            _busy = true;
            tv.FlipToFront(_flipDur, () => { _busy = false; if (_tiles.ContainsKey(id)) OnTileClicked(id); });
            return;
        }
        if (_firstPick == "")
        {
            _firstPick = id;
            SelectTile(id);
            PlaySelect0(); // 点击有效牌：播放选中音效
            return;
        }
        if (_firstPick == id) { DeselectTile(id); RevertIfFaceDown(id); _firstPick = ""; return; }
        string a = _firstPick, b = id;
        if (_board.GetFace(a) == _board.GetFace(b))
        {
            _firstPick = "";
            DeselectTile(a); // 选中牌配对成功，先恢复白色再播放碰撞动画
            DeselectTile(b);
            PlaySelect1(); // 配对成功的第二张牌：播放选中音效
            StartCrash(a, b);
        }
        else
        {
            DeselectTile(a);
            RevertIfFaceDown(a);
            _firstPick = b;
            SelectTile(b);
            PlaySelect0(); // 第二张也是有效牌（成为新第一张），播放选中音效
        }
    }

    // 若本是反面牌（被翻转成正面选中），取消选中时切回反面状态
    private void RevertIfFaceDown(string id)
    {
        if (_tiles.ContainsKey(id))
        {
            var tv = _tiles[id];
            if (tv.wasFaceDown && tv.IsFaceUp()) tv.FlipToBack(_flipDur);
        }
    }

    private void SelectTile(string id) { if (_tiles.ContainsKey(id)) _tiles[id].SetSelected(true); }
    private void DeselectTile(string id) { if (_tiles.ContainsKey(id)) _tiles[id].SetSelected(false); }
    private void ReturnFirst() { if (_firstPick != "") { RevertIfFaceDown(_firstPick); DeselectTile(_firstPick); _firstPick = ""; } }

    // 第二张不符合点击规则：前面的牌取消变色，当前牌闪红提示
    private void OnPickBlocked(string id)
    {
        ReturnFirst();
        PlayFail(); // 点击无效牌：播放失败音效
        if (_tiles.ContainsKey(id)) StartCoroutine(FlashBlocked(_tiles[id]));
    }

    // 配对成功：抬起 -> 移到中央点左右目标点 -> 一起相撞 -> 音效+缩小 -> 飞向图鉴按钮回收
    private void StartCrash(string a, string b)
    {
        _busy = true;
        _board.RemovePair(a, b); // 清数据 + 触发 OnMatched（计分 + 图鉴收集）
        StartCoroutine(CrashSequence(a, b));
    }

    private IEnumerator CrashSequence(string a, string b)
    {
        var ta = _tiles[a];
        var tb = _tiles[b];
        ta.transform.SetAsLastSibling(); // 提到最高层，避免被其它牌遮挡（UGUI 同级后绘制者在上）
        tb.transform.SetAsLastSibling();
        var ra = ta.GetComponent<RectTransform>().anchoredPosition;
        var rb = tb.GetComponent<RectTransform>().anchoredPosition;
        var mid = (ra + rb) * 0.5f;
        float X = (_cfg != null) ? _cfg.crashSideOffset : 40f;
        var L = mid + new Vector2(-X, 0);
        var R = mid + new Vector2(X, 0);
        bool aLeft = ra.x <= rb.x;
        // 目标点约束：左目标点不得比左侧牌更靠左、右目标点不得比右侧牌更靠右，
        // 避免牌反向越过自身（左侧牌在左目标点右侧时，左目标点取左侧牌的 X；右侧同理）
        float leftX = aLeft ? ra.x : rb.x;
        float rightX = aLeft ? rb.x : ra.x;
        if (leftX < L.x) L.x = leftX-15f;
        if (rightX > R.x) R.x = rightX+15f;
        var aTarget = aLeft ? L : R;
        var bTarget = aLeft ? R : L;
        float liftDur = _cfg != null ? _cfg.liftDur : 0.15f;
        float collideDur = _cfg != null ? _cfg.collideDur : 0.14f;
        float shrinkScale = _cfg != null ? _cfg.shrinkScale : 0.3f;
        float shrinkDur = _cfg != null ? _cfg.shrinkDur : 0.14f;
        float flyDur = _cfg != null ? _cfg.flyDur : 0.45f;
        float pauseDur = _cfg != null ? _cfg.pauseDur : 0.5f;

        // 阶段1：抬起（隐藏阴影）并移到各自左右目标点
        ta.Lift(); ta.MoveTo(aTarget, liftDur);
        tb.Lift(); tb.MoveTo(bTarget, liftDur);
        yield return new WaitForSeconds(liftDur);
        yield return new WaitForSeconds(pauseDur); // 移到目标点后停顿，再相撞

        // 阶段2：一起相撞到中央点（严格等待两牌移动协程完成，时长 = collideDur）
        bool ca2 = false, cb2 = false;
        ta.MoveTo(mid, collideDur, () => ca2 = true);
        tb.MoveTo(mid, collideDur, () => cb2 = true);
        yield return new WaitUntil(() => ca2 && cb2);

        // 阶段3：碰撞瞬间播放音效并平滑缩小
        PlayPeng();
        ta.TweenScale(shrinkScale, shrinkDur);
        tb.TweenScale(shrinkScale, shrinkDur);
        yield return new WaitForSeconds(shrinkDur);

        // 阶段4：缩小后飞向图鉴按钮位置后回收（对象池）
        Vector2 codexLocal = _boardRoot.InverseTransformPoint(_codexBtn.transform.position);
        ta.MoveTo(codexLocal, flyDur, () => ReleaseTileById(a));
        tb.MoveTo(codexLocal, flyDur, () => ReleaseTileById(b));
        yield return new WaitForSeconds(flyDur);

        if (_board.IsWin()) OnWin();
        else if (!_board.HasValidMove()) OnDeadlock();
        if (_state == GameState.Playing) _busy = false;
    }

    private void OnSelectionChanged(string id, bool on)
    {
        if (_tiles.ContainsKey(id)) _tiles[id].SetSelected(on);
    }

    private void OnMatched(string a, string b)
    {
        float now = Time.time;
        if (_combo > 0 && (now - _lastMatchTime) <= COMBO_SEC) _combo++;
        else _combo = 1;
        _lastMatchTime = now;
        int mult = Mathf.Min(_combo, 5);
        int gain = BASE_SCORE * mult;
        _score += gain;
        if (_score > _best) { _best = _score; SaveManager.SaveBest(_best); }
        UpdateHud();
        ShowToast("+" + gain + "  连击x" + mult);
        AddCodex(_board.GetFace(a), 2); // 收集 2 张同花色牌
    }

    private void OnBlocked(string id)
    {
        if (!_tiles.ContainsKey(id)) return;
        var tv = _tiles[id];
        StartCoroutine(FlashBlocked(tv));
    }

    private void OnWin()
    {
        _busy = true;
        _score += ROUND_BONUS;
        if (_score > _best) { _best = _score; SaveManager.SaveBest(_best); }
        SaveManager.SaveLevelBest(_currentLevel, _score);
        if (_currentLevel + 1 < _levels.Length) SaveManager.UnlockLevel(_currentLevel + 1);
        UpdateHud();
        ShowResult(true);
    }

    // 卡死（无解）时不直接判失败：弹提示并允许玩家洗牌（救活）或重开本关
    private void OnDeadlock()
    {
        if (_deadlockOverlay != null) return;   // 已弹提示，避免重复
        ShowDeadlockPrompt();
    }

    private void ShowDeadlockPrompt()
    {
        if (_uiRoot == null) { ShowToast("已无可消除，请洗牌或重开"); return; }
        var ov = UiTools.MakePanel("DeadlockOverlay");
        ov.transform.SetParent(_uiRoot, false);
        // 全屏半透明 Image 默认拦截射线，提示框显示期间底层不可点，只能操作框内按钮
        UiTools.AddText(ov.transform, "已无可消除的牌，卡死了！\n请点【洗牌】重排或【重开】本关", 28, Color.white);

        var row = UiTools.MakeGO("Btns", ov.transform);
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 24; hlg.childAlignment = TextAnchor.MiddleCenter;

        var bShuffle = UiTools.AddButton(row.transform, "洗牌");
        var leS = bShuffle.gameObject.AddComponent<LayoutElement>();
        leS.minHeight = 64; leS.preferredHeight = 64; leS.flexibleWidth = 1;
        bShuffle.onClick.AddListener(() => { CloseDeadlock(); _busy = false; DoShuffle(); });

        var bRestart = UiTools.AddButton(row.transform, "重开");
        var leR = bRestart.gameObject.AddComponent<LayoutElement>();
        leR.minHeight = 64; leR.preferredHeight = 64; leR.flexibleWidth = 1;
        bRestart.onClick.AddListener(() => { CloseDeadlock(); _busy = false; OnRestart(); });

        _deadlockOverlay = ov;
    }

    private void CloseDeadlock()
    {
        if (_deadlockOverlay != null) { Destroy(_deadlockOverlay); _deadlockOverlay = null; }
    }

    // ---------- 提示 / 洗牌 / 重开 ----------

    private void OnHint()
    {
        if (_busy || _board == null) return;
        if (_hints <= 0) { ShowAdPrompt(true); return; }
        var pair = FindHintPair();
        if (pair.Count == 0) { ShowToast("当前无可消对子"); return; }
        _hints--;
        UpdateHud();
        foreach (var id in pair) if (_tiles.ContainsKey(id)) _tiles[id].SetHint(true);
        StartCoroutine(ClearHint(pair));
    }

    private List<string> FindHintPair()
    {
        var free = new List<string>();
        foreach (var id in _board.PresentIds()) if (_board.IsFree(id)) free.Add(id);
        var byFace = new Dictionary<int, string>();
        foreach (var id in free)
        {
            int f = _board.GetFace(id);
            if (byFace.ContainsKey(f)) return new List<string> { byFace[f], id };
            byFace[f] = id;
        }
        return new List<string>();
    }

    private void OnShuffle()
    {
        if (_busy || _board == null) return;
        if (_shuffles <= 0) { ShowAdPrompt(false); return; }
        _shuffles--;
        UpdateHud();
        DoShuffle();
    }

    private void DoShuffle()
    {
        _busy = true;
        ReturnFirst();
        _board.ClearSelection();
        foreach (var kv in _tiles) kv.Value.SetSelected(false);

        var ids = _board.PresentIds();
        var faces = new List<int>();
        foreach (var id in ids) faces.Add(_board.GetFace(id));
        Shuffle(faces);
        for (int i = 0; i < ids.Count; i++) _board.SetFace(ids[i], faces[i]);

        if (!_board.HasValidMove())
        {
            var free = new List<string>();
            foreach (var id in ids) if (_board.IsFree(id)) free.Add(id);
            if (free.Count >= 2) _board.SetFace(free[1], _board.GetFace(free[0]));
        }

        foreach (var id in ids)
        {
            if (!_tiles.ContainsKey(id)) continue;
            int fi = _board.GetFace(id);
            _tiles[id].SetFaceSprite(GetFaceSprite(fi));
        }

        ShowToast("已洗牌");
        _busy = false;
    }

    private void OnRestart()
    {
        if (_busy) return;
        StartLevel(_currentLevel);
    }

    private static void Shuffle(List<int> arr)
    {
        var rnd = new System.Random();
        for (int i = arr.Count - 1; i > 0; i--)
        {
            int j = rnd.Next(i + 1);
            int t = arr[i];
            arr[i] = arr[j];
            arr[j] = t;
        }
    }

    // ---------- 协程动画 ----------

    private IEnumerator RemoveTile(string id)
    {
        if (!_tiles.ContainsKey(id)) yield break;
        var tv = _tiles[id];
        _tiles.Remove(id);
        var rt = tv.GetComponent<RectTransform>();
        float dur = 0.18f;
        float t = 0;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = 1f - t / dur;
            rt.localScale = new Vector3(k, k, 1);
            tv.SetAlpha(k);
            yield return null;
        }
        Destroy(tv.gameObject);
    }

    private IEnumerator FlashBlocked(TileView tv)
    {
        Color red = new Color(1f, 0.4f, 0.4f);
        if (tv.bodyImage != null) tv.bodyImage.color = red;
        if (tv.bodySub1 != null) tv.bodySub1.color = red;
        if (tv.bodySub2 != null) tv.bodySub2.color = red;
        yield return new WaitForSeconds(0.25f);
        if (tv != null && tv.bodyImage != null) tv.bodyImage.color = Color.white;
        if (tv != null && tv.bodySub1 != null) tv.bodySub1.color = Color.white;
        if (tv != null && tv.bodySub2 != null) tv.bodySub2.color = Color.white;
    }

    private IEnumerator ClearHint(List<string> pair)
    {
        yield return new WaitForSeconds(1.2f);
        foreach (var id in pair) if (_tiles.ContainsKey(id)) _tiles[id].SetHint(false);
    }

    private IEnumerator WaitThenNextRound()
    {
        yield return new WaitForSeconds(1.2f);
        StartRound();
        _busy = false;
    }

    // ---------- HUD / Toast ----------

    private void UpdateHud()
    {
        if (_scoreLabel != null) _scoreLabel.text = "分数 " + _score;
        if (_bestLabel != null) _bestLabel.text = "最高 " + _best;
        if (_roundLabel != null) _roundLabel.text = (_levels != null && _currentLevel >= 0 && _currentLevel < _levels.Length) ? _levels[_currentLevel].name : "第 " + _round + " 局";
        if (_hintBtn != null)
        {
            var txt = _hintBtn.GetComponentInChildren<Text>();
            if (txt != null) txt.text = _hints.ToString();
        }
        if (_shuffleBtn != null)
        {
            var st = _shuffleBtn.GetComponentInChildren<Text>();
            if (st != null) st.text = _shuffles.ToString();
        }
    }

    private void ShowToast(string msg)
    {
        if (_toastGO == null || _toast == null) return;
        _toast.text = msg;
        _toastGO.SetActive(true);
        StopCoroutine("HideToast");
        StartCoroutine("HideToast");
    }

    private IEnumerator HideToast()
    {
        yield return new WaitForSeconds(1.2f);
        if (_toastGO != null) _toastGO.SetActive(false);
    }

    // ---------- 状态机 / 面板 ----------

    private void SetGameUiVisible(bool v)
    {
        if (_topBar != null) _topBar.SetActive(v);
        if (_bottomBar != null) _bottomBar.SetActive(v);
        if (_boardRoot != null) _boardRoot.gameObject.SetActive(v);
    }

    private void HideAllPanels()
    {
        SetPanelVisible(_menuPanel, false);
        SetPanelVisible(_levelPanel, false);
        SetPanelVisible(_pausePanel, false);
        SetPanelVisible(_resultPanel, false);
        SetPanelVisible(_settingsPanel, false);
        SetPanelVisible(_codexPanel, false);   // 修复：图鉴此前漏隐藏，导致开局就显示
    }

    // 用 CanvasGroup 的 alpha 控制面板显隐（而非 SetActive），便于淡入淡出且对象保持存活
    private void SetPanelVisible(GameObject panel, bool visible)
    {
        if (panel == null) return;
        var cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) { panel.SetActive(visible); return; }
        cg.alpha = visible ? 1 : 0;
        cg.interactable = visible;
        cg.blocksRaycasts = visible;
    }

    // 确保面板根有 CanvasGroup；缺失时补加（不覆盖用户已调整的 prefab），并初始隐藏
    private static void EnsureCanvasGroup(GameObject panel)
    {
        if (panel == null) return;
        var cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();
        cg.alpha = 0; cg.interactable = false; cg.blocksRaycasts = false;
    }

    // 黑色蒙版：所有界面切换时黑入/黑出过渡。挂在 Canvas 下并置顶，盖住全部 UI（含 HUD/棋盘）
    private void EnsureFade()
    {
        if (_fade != null) return;
        var canvas = GetComponentInParent<Canvas>();
        var parent = (canvas != null) ? canvas.transform : (_uiRoot != null ? _uiRoot : transform);
        _fade = new GameObject("FadeMask", typeof(RectTransform));
        _fade.transform.SetParent(parent, false);
        var tr = _fade.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
        var img = _fade.AddComponent<Image>();
        img.color = Color.black; img.raycastTarget = true;
        _fadeCg = _fade.AddComponent<CanvasGroup>();
        _fadeCg.alpha = 0; _fadeCg.interactable = false; _fadeCg.blocksRaycasts = false;
        _fade.transform.SetAsLastSibling();   // 置顶，覆盖所有界面
    }

    // 界面切换：黑入 → 执行 swap（切换面板/状态）→ 黑出。过渡期间蒙版拦截点击，避免打断
    private void DoTransition(System.Action swap)
    {
        if (_transitioning) { swap?.Invoke(); return; }
        StartCoroutine(Transition(swap));
    }

    private System.Collections.IEnumerator Transition(System.Action swap)
    {
        _transitioning = true;
        if (_fadeCg != null) _fadeCg.blocksRaycasts = true;
        yield return FadeTo(1f, 0.18f);
        swap?.Invoke();
        yield return new WaitForSeconds(0.02f);
        yield return FadeTo(0f, 0.22f);
        if (_fadeCg != null) _fadeCg.blocksRaycasts = false;
        _transitioning = false;
    }

    private System.Collections.IEnumerator FadeTo(float target, float dur)
    {
        if (_fadeCg == null) yield break;
        float a = _fadeCg.alpha;
        float t = 0;
        while (t < dur)
        {
            t += Time.deltaTime;
            _fadeCg.alpha = Mathf.Lerp(a, target, Mathf.Clamp01(t / dur));
            yield return null;
        }
        _fadeCg.alpha = target;
    }

    private GameObject MakePanel(Transform parent, string name)
    {
        var go = MakeGO(name, parent);
        var tr = go.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.7f);
        var lg = go.AddComponent<VerticalLayoutGroup>();
        lg.spacing = 20; lg.childAlignment = TextAnchor.MiddleCenter;
        lg.padding = new RectOffset(40, 40, 40, 40);
        return go;
    }

    private void PanelButton(GameObject panel, string label, UnityEngine.Events.UnityAction cb)
    {
        var b = AddButton(panel, label);
        var le = b.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 72; le.preferredHeight = 72; le.flexibleWidth = 1;
        b.onClick.AddListener(cb);
    }

    // ---------- 设置界面（参考设计稿：音乐/音效/震动开关 + 画质三档 + 语音 + 返回） ----------

    private void ShowSettings()
    {
        if (_settingsPanel == null) return;
        _settingsReturnState = _state;
        HideAllPanels();
        SetPanelVisible(_settingsPanel, true);
    }

    // 设置返回：从游戏内进入则回到 GamePanel（仅关闭设置面板，HUD 一直可见）；从主菜单进入则回主菜单
    private void OnSettingsBack()
    {
        SetPanelVisible(_settingsPanel, false);
        if (_settingsReturnState == GameState.Playing)
            SetGameUiVisible(true);
        else
            ShowMenu();
    }

    // 加载设置面板预制体并实例化显示（预制体需由 Tools/生成设置面板预制体 生成；UI 已内置于 prefab）
    private void LoadSettingsPrefab(Transform parent)
    {
        var prefab = Resources.Load<GameObject>("Prefab/SettingsPanel");
        if (prefab == null)
        {
            Debug.LogError("[Settings] 未找到 Resources/SettingsPanel.prefab，请运行菜单 Tools / 生成设置面板预制体");
            _settingsPanel = null;
            return;
        }
        _settingsPanel = Instantiate(prefab, parent);
        EnsureCanvasGroup(_settingsPanel);
        var sp = _settingsPanel.GetComponent<SettingsPanel>();
        if (sp != null) sp.Init(_settings,
            onBack: OnSettingsBack,
            onAudio: ApplyAudioSettings,
            onQuality: ApplyQualitySetting,
            onSave: () => SaveManager.SaveSettings(_settings));
    }

    private void BuildPanels(Transform parent)
    {
        _uiRoot = parent;
        LoadMenuPrefab(parent);
        LoadLevelPrefab(parent);
        LoadPausePrefab(parent);
        LoadResultPrefab(parent);
        LoadCodexPrefab(parent);
        LoadSettingsPrefab(parent);
        LoadAdPromptPrefab(parent);

        HideAllPanels();
        EnsureFade();
        SetPanelVisible(_menuPanel, true);
    }

    private void LoadMenuPrefab(Transform parent)
    {
        var prefab = Resources.Load<GameObject>("Prefab/MenuPanel");
        if (prefab == null) { Debug.LogError("[UI] 未找到 Prefab/MenuPanel，请运行 Tools/生成 UI 预制体"); _menuPanel = null; return; }
        _menuPanel = Instantiate(prefab, parent);
        EnsureCanvasGroup(_menuPanel);
        _menuPanelC = _menuPanel.GetComponent<MenuPanel>();
        if (_menuPanelC != null) _menuPanelC.Init(
            () => DoTransition(() => StartLevel(Mathf.Min(SaveManager.LoadMaxUnlocked(), Mathf.Max(0, _levels.Length - 1)))),
            ShowLevelSelect, ShowSettings, ShowCodex);
    }

    private void LoadLevelPrefab(Transform parent)
    {
        var prefab = Resources.Load<GameObject>("Prefab/LevelPanel");
        if (prefab == null) { Debug.LogError("[UI] 未找到 Prefab/LevelPanel，请运行 Tools/生成 UI 预制体"); _levelPanel = null; return; }
        _levelPanel = Instantiate(prefab, parent);
        EnsureCanvasGroup(_levelPanel);
        _levelPanelC = _levelPanel.GetComponent<LevelSelectPanel>();
        if (_levelPanelC != null) _levelPanelC.Init(ShowMenu);
    }

    private void LoadPausePrefab(Transform parent)
    {
        var prefab = Resources.Load<GameObject>("Prefab/PausePanel");
        if (prefab == null) { Debug.LogError("[UI] 未找到 Prefab/PausePanel，请运行 Tools/生成 UI 预制体"); _pausePanel = null; return; }
        _pausePanel = Instantiate(prefab, parent);
        EnsureCanvasGroup(_pausePanel);
        _pausePanelC = _pausePanel.GetComponent<PausePanel>();
        if (_pausePanelC != null) _pausePanelC.Init(OnResume, OnRetry, OnToMenu);
    }

    private void LoadResultPrefab(Transform parent)
    {
        var prefab = Resources.Load<GameObject>("Prefab/ResultPanel");
        if (prefab == null) { Debug.LogError("[UI] 未找到 Prefab/ResultPanel，请运行 Tools/生成 UI 预制体"); _resultPanel = null; return; }
        _resultPanel = Instantiate(prefab, parent);
        EnsureCanvasGroup(_resultPanel);
        _resultPanelC = _resultPanel.GetComponent<ResultPanel>();
        if (_resultPanelC != null) _resultPanelC.Init(OnNextLevel, OnRetry, OnToMenu, () => StartLevel(0));
    }

    private void LoadCodexPrefab(Transform parent)
    {
        var prefab = Resources.Load<GameObject>("Prefab/CodexPanel");
        if (prefab == null) { Debug.LogError("[UI] 未找到 Prefab/CodexPanel，请运行 Tools/生成 UI 预制体"); _codexPanel = null; return; }
        _codexPanel = Instantiate(prefab, parent);
        EnsureCanvasGroup(_codexPanel);
        _codexPanelC = _codexPanel.GetComponent<CodexPanel>();
        if (_codexPanelC != null) _codexPanelC.Init(CloseCodex);
    }

    private void LoadAdPromptPrefab(Transform parent)
    {
        var prefab = Resources.Load<GameObject>("Prefab/AdPromptPanel");
        if (prefab == null) { Debug.LogError("[UI] 未找到 Prefab/AdPromptPanel，请运行 Tools/生成广告提示框预制体"); _adPromptPanel = null; return; }
        _adPromptPanel = Instantiate(prefab, parent);
        EnsureCanvasGroup(_adPromptPanel);
        _adPromptPanelC = _adPromptPanel.GetComponent<AdPromptPanel>();
        if (_adPromptPanelC != null) _adPromptPanelC.Init();
    }

    // 提示/洗牌次数用尽时弹出广告框；确认则播放激励视频（占位）并 +1 次
    private void ShowAdPrompt(bool isHint)
    {
        if (_adPromptPanelC == null) { ShowToast(isHint ? "提示次数用尽" : "洗牌次数用尽"); return; }
        _adPromptPanelC.Show(isHint,
            onConfirm: () => { CloseAdPrompt(); ShowRewardedAd(() => { if (isHint) _hints++; else _shuffles++; UpdateHud(); }); },
            onCancel: CloseAdPrompt);
        SetPanelVisible(_adPromptPanel, true);
    }

    private void CloseAdPrompt()
    {
        SetPanelVisible(_adPromptPanel, false);
    }

    // 激励视频广告占位：之后接入真实 SDK（AdMob / Unity Ads 等），看完广告回调 onReward
    private void ShowRewardedAd(System.Action onReward)
    {
        Debug.Log("[Ad] 激励视频广告占位：模拟观看完成，发放奖励");
        if (onReward != null) onReward();
    }

    private void ShowMenu()
    {
        _state = GameState.Menu;
        HideAllPanels();
        SetGameUiVisible(false);
        SetPanelVisible(_menuPanel, true);
    }

    private void ShowLevelSelect()
    {
        _state = GameState.LevelSelect;
        HideAllPanels();
        SetGameUiVisible(false);
        if (_levelPanelC != null)
        {
            string[] names = new string[_levels.Length];
            for (int i = 0; i < _levels.Length; i++) names[i] = _levels[i].name;
            _levelPanelC.BuildList(names, SaveManager.LoadMaxUnlocked(), idx => DoTransition(() => StartLevel(idx)));
        }
        SetPanelVisible(_levelPanel, true);
    }

    private void StartLevel(int idx)
    {
        if (_levels == null || idx < 0 || idx >= _levels.Length) { ShowMenu(); return; }
        _currentLevel = idx;
        _layoutData = _levels[idx].layout;
        _score = 0;
        _state = GameState.Playing;
        HideAllPanels();
        SetGameUiVisible(true);
        StartRound();
    }

    private void OnPause()
    {
        if (_state != GameState.Playing) return;
        _state = GameState.Paused;
        SetPanelVisible(_pausePanel, true);
    }

    private void OnResume()
    {
        _state = GameState.Playing;
        SetPanelVisible(_pausePanel, false);
    }

    private void OnToMenu()
    {
        ShowMenu();
    }

    private void OnRetry()
    {
        StartLevel(_currentLevel);
    }

    private void OnNextLevel()
    {
        if (_currentLevel + 1 < _levels.Length) DoTransition(() => StartLevel(_currentLevel + 1));
        else ShowMenu();
    }

    private void ShowResult(bool win)
    {
        _state = GameState.Result;
        bool hasNext = win && (_currentLevel + 1 < _levels.Length);
        bool isLast = win && (_currentLevel + 1 >= _levels.Length);
        string info = "得分 " + _score + "\n最高 " + _best;
        if (_levels != null && _currentLevel >= 0 && _currentLevel < _levels.Length)
            info += "\n" + _levels[_currentLevel].name;
        if (!win) info += "\n无解，本关失败";
        if (_resultPanelC != null) _resultPanelC.Show(win, info, hasNext, isLast);
        HideAllPanels();
        SetGameUiVisible(false);
        SetPanelVisible(_resultPanel, true);
    }

    // ---------- 关卡加载（Resources/level/levelN.json） ----------

    private class LevelEntry
    {
        public string name;
        public int targetScore;
        public int seed;
        public LayoutData layout;
    }

    private void LoadLevels()
    {
        var assets = Resources.LoadAll<TextAsset>("level");
        if (assets == null || assets.Length == 0)
        {
            Debug.LogError("GameController: 未找到 Resources/level/ 下的关卡文件");
            _levels = new LevelEntry[0];
            return;
        }
        var parsed = new List<(int order, LevelFile f)>();
        foreach (var a in assets)
        {
            var f = JsonUtility.FromJson<LevelFile>(a.text);
            if (f == null) continue;
            parsed.Add((ExtractLevelOrder(a.name), f));
        }
        parsed.Sort((x, y) => x.order.CompareTo(y.order));
        var list = new List<LevelEntry>();
        foreach (var (order, f) in parsed)
        {
            var e = new LevelEntry();
            e.name = string.IsNullOrEmpty(f.name) ? ("第" + order + "关") : f.name;
            e.targetScore = f.targetScore;
            e.seed = f.seed;
            if (f.tiles != null && f.tiles.Length > 0)
                e.layout = new LayoutData { tiles = f.tiles };
            else
                e.layout = LayoutFactory.MakeTower(f.cols, f.rows, f.layers);
            list.Add(e);
        }
        _levels = list.ToArray();
    }

    private static int ExtractLevelOrder(string fileName)
    {
        int n = 0;
        foreach (char c in fileName)
            if (c >= '0' && c <= '9') n = n * 10 + (c - '0');
        return n;
    }

    // 递归查找子物体（Transform.Find 只查直接子级，预制体新增嵌套容器后会找不到）
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

    private static void SetChildSprite(Transform root, string name, Sprite s)
    {
        if (s == null) return;
        var img = FindDeep(root, name)?.GetComponent<Image>();
        if (img != null) img.sprite = s;
    }
    private static void SetChildColor(Transform root, string name, Color c)
    {
        var img = FindDeep(root, name)?.GetComponent<Image>();
        if (img != null) img.color = c;
    }

    // 自建最小可用 Slider（不依赖任何预制体），用于调试层偏移等实时调参
    private Slider AddSlider(Transform parent, string name, float min, float max, float val, UnityEngine.Events.UnityAction<float> cb)
    {
        var go = MakeGO(name, parent);
        var tr = go.GetComponent<RectTransform>();
        tr.sizeDelta = new Vector2(240, 36);
        var bg = MakeGO("Background", go.transform);
        var bgr = bg.GetComponent<RectTransform>();
        bgr.anchorMin = Vector2.zero; bgr.anchorMax = Vector2.one; bgr.offsetMin = Vector2.zero; bgr.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>(); bgImg.color = new Color(1f, 1f, 1f, 0.18f);
        var fillArea = MakeGO("FillArea", go.transform);
        var far = fillArea.GetComponent<RectTransform>();
        far.anchorMin = new Vector2(0f, 0.25f); far.anchorMax = new Vector2(1f, 0.75f); far.offsetMin = Vector2.zero; far.offsetMax = Vector2.zero;
        var fill = MakeGO("Fill", fillArea.transform);
        var filr = fill.GetComponent<RectTransform>();
        filr.anchorMin = Vector2.zero; filr.anchorMax = Vector2.one; filr.offsetMin = Vector2.zero; filr.offsetMax = Vector2.zero;
        var fillImg = fill.AddComponent<Image>(); fillImg.color = new Color(1f, 0.84f, 0.40f);
        var handle = MakeGO("Handle", go.transform);
        var hr = handle.GetComponent<RectTransform>();
        hr.sizeDelta = new Vector2(24, 36); hr.anchorMin = new Vector2(0f, 0.5f); hr.anchorMax = new Vector2(0f, 0.5f); hr.pivot = new Vector2(0.5f, 0.5f);
        var hImg = handle.AddComponent<Image>(); hImg.color = Color.white;
        var slider = go.AddComponent<Slider>();
        slider.minValue = min; slider.maxValue = max; slider.value = val;
        slider.fillRect = filr;
        slider.handleRect = hr;
        slider.targetGraphic = hImg;
        slider.onValueChanged.AddListener(cb);
        return slider;
    }

    // ---------- 对象池 ----------

    private TileView GetTile()
    {
        foreach (var p in _pool) if (p != null && !p.gameObject.activeSelf) { p.gameObject.SetActive(true); return p; }
        GameObject go = _tilePrefab != null ? Instantiate(_tilePrefab, _boardRoot) : MakeGO("Tile", _boardRoot);
        var tv = go.GetComponent<TileView>();
        if (tv == null) tv = go.AddComponent<TileView>();
        return tv;
    }

    private void ReleaseTile(TileView tv)
    {
        if (tv == null) return;
        tv.gameObject.SetActive(false);
        if (!_pool.Contains(tv)) _pool.Add(tv);
    }

    private void ReleaseTileById(string id)
    {
        if (!_tiles.ContainsKey(id)) return;
        var tv = _tiles[id];
        _tiles.Remove(id);
        _placedView.RemoveAll(x => x.go == tv.gameObject);
        ReleaseTile(tv);
    }

    private void PlayPeng()
    {
        if (_audio != null && _peng != null) _audio.PlayOneShot(_peng);
        if (_settings != null && _settings.vibration && Application.isMobilePlatform) Handheld.Vibrate(); // 震动开关（仅移动端生效）
    }
    private void PlayFail() { if (_audio != null && _fail != null) _audio.PlayOneShot(_fail); }
    private void PlaySelect0() { if (_audio != null && _select0 != null) _audio.PlayOneShot(_select0); }
    private void PlaySelect1() { if (_audio != null && _select1 != null) _audio.PlayOneShot(_select1); }

    // 游戏开始循环播放背景音乐（若已在播放则不重复触发）
    private void PlayBgm()
    {
        if (_bgmSrc == null || _bgm == null) return;
        if (_bgmSrc.clip != _bgm) { _bgmSrc.clip = _bgm; _bgmSrc.loop = true; }
        if (!_bgmSrc.isPlaying) _bgmSrc.Play();
    }

    // 应用音频开关：音乐只作用于 bgm 音源，音效作用于所有 PlayOneShot
    private void ApplyAudioSettings()
    {
        if (_settings == null) return;
        if (_bgmSrc != null) _bgmSrc.mute = !_settings.music;
        if (_audio != null) _audio.mute = !_settings.sfx;
    }

    // 应用画面质量档位（0低/1中/2高）
    private void ApplyQualitySetting()
    {
        if (_settings == null) return;
        int q = Mathf.Clamp(_settings.quality, 0, 2);
        if (QualitySettings.names != null && QualitySettings.names.Length > 0)
            QualitySettings.SetQualityLevel(Mathf.Min(q, QualitySettings.names.Length - 1), true);
    }

    // ---------- 图鉴 ----------

    private void EnsureCodexSize()
    {
        if (_codexCollected.Length == _faceSprites.Count) return;
        var tmp = new int[_faceSprites.Count];
        for (int i = 0; i < tmp.Length && i < _codexCollected.Length; i++) tmp[i] = _codexCollected[i];
        _codexCollected = tmp;
    }

    private void AddCodex(int face, int n)
    {
        if (face < 0 || face >= _codexCollected.Length) return;
        _codexCollected[face] += n;
        SaveManager.SaveCodex(_codexCollected);
    }

    private static int LevelOf(int count) => count >= 1000 ? 3 : (count >= 100 ? 2 : (count >= 10 ? 1 : 0));
    private static Color CodexColor(int lvl) => CODE_COLORS[Mathf.Clamp(lvl, 0, CODE_COLORS.Length - 1)];

    private void ShowCodex()
    {
        if (_state != GameState.Playing && _state != GameState.Menu) return;
        _codexReturnState = _state;
        _state = GameState.Paused;
        // 从主菜单打开时先隐藏主菜单，避免图鉴面板下透出菜单
        if (_codexReturnState == GameState.Menu && _menuPanel != null) SetPanelVisible(_menuPanel, false);
        if (_codexPanelC != null)
        {
            var faces = new System.Collections.Generic.List<Sprite>();
            for (int f = 0; f < _faceSprites.Count; f++) faces.Add(GetFaceSprite(f));
            _codexPanelC.Refresh(faces, _codexCollected, _levelBacks, CODE_COLORS);
        }
        SetPanelVisible(_codexPanel, true);
    }

    private void CloseCodex()
    {
        SetPanelVisible(_codexPanel, false);
        _state = _codexReturnState;
        // 从主菜单打开时恢复主菜单显示
        if (_codexReturnState == GameState.Menu && _menuPanel != null) SetPanelVisible(_menuPanel, true);
    }


}
