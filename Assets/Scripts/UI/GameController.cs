using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// 完整游戏主控制器（Godot game_session.gd 的 C# 移植）。
/// 无限随机模式：随机生成可解牌阵，全消即过关并自动进下一局，分数跨局累加。
/// 不限时；提示（限次）高亮一对可消牌；死局自动洗牌，也可手动洗牌。
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
    private Dictionary<int, Sprite> _faceSprites = new Dictionary<int, Sprite>();
    private LayoutData _layoutData;
    private ConfigData _cfg = new ConfigData();

    private int _score;
    private int _best;
    private int _round;
    private int _combo;
    private float _lastMatchTime;
    private int _hints;
    private bool _busy;

    private Text _scoreLabel;
    private Text _bestLabel;
    private Text _roundLabel;
    private Text _toast;
    private GameObject _toastGO;
    private Button _hintBtn;
    private Button _shuffleBtn;
    private Button _restartBtn;
    private RectTransform _boardRoot;
    private Font _font;

    [Header("2.5D 层偏移滑条（运行时控制，可留空由代码自动创建）")]
    public Slider layerDxSlider;
    public Slider layerDySlider;
    private Text _dxLabel;
    private Text _dyLabel;
    private float _layerDx;
    private float _layerDy;
    private List<(TileOut t, GameObject go)> _placedView = new List<(TileOut t, GameObject go)>();

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
        BuildUI();
        LoadResources();
        _best = SaveManager.LoadBest();
        StartRound();
    }

    private void Update()
    {
        if (Screen.width != _lastScreenW || Screen.height != _lastScreenH)
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

        var layoutAsset = Resources.Load<TextAsset>("mahjong-layout");
        if (layoutAsset != null) _layoutData = JsonUtility.FromJson<LayoutData>(layoutAsset.text);
        else Debug.LogError("GameController: 未找到 Resources/mahjong-layout.json");

        var cfgAsset = Resources.Load<TextAsset>("ui_config");
        if (cfgAsset != null) _cfg = JsonUtility.FromJson<ConfigData>(cfgAsset.text);

        _tilePrefab = Resources.Load<GameObject>("TilePrefab");
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

        // 顶栏：分数 / 最高分 / 局数
        var top = MakeGO("TopBar", canvasTr);
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

        // 抬升量滑条（运行时控制 2.5D 层偏移：层越高抬得越多）。场景未挂则自动创建。
        float defDx = (_cfg != null && _cfg.layer_dx != 0f) ? _cfg.layer_dx : 5f;
        float defDy = (_cfg != null && _cfg.layer_dy != 0f) ? _cfg.layer_dy : 10f;
        _layerDx = defDx;
        _layerDy = defDy;
        if (layerDxSlider == null)
        {
            layerDxSlider = CreateSlider(canvasTr, "层偏移X", 0f, 40f, defDx, out _dxLabel,
                v => { _layerDx = v; if (_dxLabel != null) _dxLabel.text = "层偏移X " + v.ToString("F0"); LayoutTiles(); });
            PositionSlider(layerDxSlider, 0f, 0.5f, 20f);
        }
        else
            layerDxSlider.onValueChanged.AddListener(v => { _layerDx = v; LayoutTiles(); });
        if (layerDySlider == null)
        {
            layerDySlider = CreateSlider(canvasTr, "层偏移Y", 0f, 40f, defDy, out _dyLabel,
                v => { _layerDy = v; if (_dyLabel != null) _dyLabel.text = "层偏移Y " + v.ToString("F0"); LayoutTiles(); });
            PositionSlider(layerDySlider, 0.5f, 1f, -20f);
        }
        else
            layerDySlider.onValueChanged.AddListener(v => { _layerDy = v; LayoutTiles(); });
    }

    // ---------- 局流程 ----------

    private void StartRound()
    {
        _round++;
        _hints = HINTS_PER_ROUND;
        _combo = 0;
        BuildBoard();
        UpdateHud();
        FitBoard();
        ShowToast("第 " + _round + " 局");
    }

    private void BuildBoard()
    {
        if (_layoutData == null || _layoutData.tiles == null || _layoutData.tiles.Length == 0)
        {
            Debug.LogError("GameController: 布局数据为空，无法生成牌阵");
            return;
        }

        var children = new List<GameObject>();
        for (int i = 0; i < _boardRoot.childCount; i++) children.Add(_boardRoot.GetChild(i).gameObject);
        foreach (var c in children) Destroy(c.gameObject);
        _tiles.Clear();
        _placedView.Clear();

        float tw = _cfg.tile_width;
        float th = _cfg.tile_height;

        var gen = LevelGenerator.Generate(_layoutData, Random.Range(0, int.MaxValue));
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
            GameObject tileGO;
            if (_tilePrefab != null)
                tileGO = Instantiate(_tilePrefab, _boardRoot);
            else
                tileGO = MakeGO(t.id, _boardRoot);
            tileGO.name = t.id;
            var tr = tileGO.GetComponent<RectTransform>();
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.pivot = new Vector2(0.5f, 0.5f);
            tr.sizeDelta = new Vector2(tw, th);

            // 阴影层：完全按预制体生成（尺寸/锚点/偏移/颜色都由你微调），代码只回填缺失的牌底图
            Image shadowImg = tileGO.transform.Find("Shadow")?.GetComponent<Image>();
            if (shadowImg == null)
            {
                shadowImg = MakeGO("Shadow", tileGO.transform).AddComponent<Image>();
                shadowImg.color = new Color(0f, 0f, 0f, 0.35f);
                shadowImg.preserveAspect = true;
            }
            if (shadowImg.sprite == null) shadowImg.sprite = _cardFront;
            shadowImg.raycastTarget = false;
            // 牌内绘制顺序（Shadow 在最下）由预制体 RootOrder 决定，代码不再强制

            // 牌底：完全按预制体生成，代码仅回填缺失的牌底图
            Image bodyImg = tileGO.transform.Find("Body")?.GetComponent<Image>();
            if (bodyImg == null)
            {
                bodyImg = MakeGO("Body", tileGO.transform).AddComponent<Image>();
                bodyImg.color = Color.white;
                bodyImg.preserveAspect = true;
            }
            if (bodyImg.sprite == null) bodyImg.sprite = _cardFront;
            bodyImg.raycastTarget = true;

            // 牌面：牌面 sprite 必须按 tileType 绑定（每张不同），其余完全按预制体
            Image faceImg = tileGO.transform.Find("Face")?.GetComponent<Image>();
            if (faceImg == null)
            {
                faceImg = MakeGO("Face", tileGO.transform).AddComponent<Image>();
                faceImg.preserveAspect = true;
            }
            faceImg.sprite = GetFaceSprite(t.tileType);
            faceImg.raycastTarget = false;

            var tv = tileGO.GetComponent<TileView>();
            if (tv == null) tv = tileGO.AddComponent<TileView>();
            tv.tileId = t.id;
            tv.faceIndex = t.tileType;
            tv.bodyImage = bodyImg;
            tv.faceImage = faceImg;
            tv.SetBaseColor(bodyImg.color); // 记住预制体底色，选中取消后恢复
            tv.onClick = OnTileClicked;

            _tiles[t.id] = tv;
            _placedView.Add((t, tileGO));
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

        // 绘制顺序（决定 UGUI 遮挡）：
        // 1) 先按层高 layer 升序 —— 高层牌整体盖住低层牌（叠层 z 轴）；
        // 2) 同层内，让 右(x 大) + 下(y 小) 的牌排在后面（后绘制=覆盖在上），
        //    这样右下方向的牌压住左上方向牌的阴影（阴影在牌的右下方）。
        placed.Sort((a, b) =>
        {
            int la = a.t.layer, lb = b.t.layer;
            if (la != lb) return la.CompareTo(lb);
            float ax = a.px - minX + tw * 0.5f - _nativeW * 0.5f; // 屏幕 x（右为正）
            float bx = b.px - minX + tw * 0.5f - _nativeW * 0.5f;
            if (ax != bx) return ax.CompareTo(bx);                // 右优先后画
            float ay = -(a.py - minY + th * 0.5f) + _nativeH * 0.5f; // 屏幕 y（上为正）
            float by = -(b.py - minY + th * 0.5f) + _nativeH * 0.5f;
            return by.CompareTo(ay);                              // 下(cy 小)优先后画
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
            // 层号越大 → sibling index 越大 → UGUI 越后绘制（覆盖在低层之上）。
            int ord = layerCounter.TryGetValue(p.t.layer, out int o) ? o : 0;
            layerCounter[p.t.layer] = ord + 1;
            tr.SetSiblingIndex(p.t.layer * span + ord);
        }

        FitBoard();
    }

    // 运行时创建一个标准 UGUI Slider（含 Background/Fill/Handle 子结构）。场景已挂则不会走到这里。
    private Slider CreateSlider(Transform parent, string name, float min, float max, float val, out Text label, UnityEngine.Events.UnityAction<float> cb)
    {
        var go = MakeGO(name, parent);
        var slider = go.AddComponent<Slider>();

        var bg = MakeGO("Background", go.transform);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.3f, 0.3f, 0.3f, 0.6f);
        var bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;

        var fillArea = MakeGO("FillArea", go.transform);
        var faRT = fillArea.GetComponent<RectTransform>();
        faRT.anchorMin = new Vector2(0f, 0.25f); faRT.anchorMax = new Vector2(1f, 0.75f);
        faRT.offsetMin = Vector2.zero; faRT.offsetMax = Vector2.zero;
        var fill = MakeGO("Fill", fillArea.transform);
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.2f, 0.6f, 1f);
        var fillRT = fill.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero; fillRT.offsetMax = Vector2.zero;
        slider.fillRect = fillRT;

        var handleArea = MakeGO("HandleSlideArea", go.transform);
        var haRT = handleArea.GetComponent<RectTransform>();
        haRT.anchorMin = new Vector2(0f, 0.5f); haRT.anchorMax = new Vector2(1f, 0.5f);
        haRT.offsetMin = Vector2.zero; haRT.offsetMax = Vector2.zero;
        var handle = MakeGO("Handle", handleArea.transform);
        var handleImg = handle.AddComponent<Image>();
        handleImg.color = Color.white;
        var handleRT = handle.GetComponent<RectTransform>();
        handleRT.anchorMin = new Vector2(0.5f, 0.5f); handleRT.anchorMax = new Vector2(0.5f, 0.5f);
        handleRT.sizeDelta = new Vector2(20, 20);
        slider.handleRect = handleRT;

        slider.targetGraphic = bgImg;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = val;
        slider.onValueChanged.AddListener(cb);

        label = MakeGO("Label", go.transform).AddComponent<Text>();
        label.text = name + " " + val.ToString("F0");
        label.fontSize = 18;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        if (_font != null) label.font = _font;
        var lblRT = label.gameObject.GetComponent<RectTransform>();
        lblRT.anchorMin = new Vector2(0f, 1f); lblRT.anchorMax = new Vector2(1f, 1f);
        lblRT.sizeDelta = new Vector2(0, 18); lblRT.anchoredPosition = new Vector2(0, 6);

        return slider;
    }

    // 把滑条定位到屏幕底部两側（左半/右半），高度 40，置于底栏按钮上方。
    private void PositionSlider(Slider slider, float aMinX, float aMaxX, float padX)
    {
        var rt = slider.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(aMinX, 0f);
        rt.anchorMax = new Vector2(aMaxX, 0f);
        rt.pivot = new Vector2(aMinX, 0f);
        rt.sizeDelta = new Vector2(-Mathf.Abs(padX), 40f);
        rt.anchoredPosition = new Vector2(padX, 130f);
    }

    private void FitBoard()
    {
        _lastScreenW = Screen.width;
        _lastScreenH = Screen.height;
        if (_boardRoot == null || _nativeW <= 0 || _nativeH <= 0) return;

        float topH = 80f, botH = 96f, pad = _cfg.padding;
        float availW = Screen.width - pad * 2f;
        float availH = Screen.height - topH - botH - pad;
        if (availW <= 0 || availH <= 0) return;

        float s = Mathf.Min(availW / _nativeW, availH / _nativeH) * _cfg.zoom;
        s = Mathf.Max(s, 0.05f);
        _boardRoot.localScale = new Vector3(s, s, 1);
        _boardRoot.anchoredPosition = new Vector2(0, (botH - topH) / 2f);
    }

    // ---------- 事件处理 ----------

    private void OnTileClicked(string id)
    {
        if (_busy || _board == null) return;
        _board.Click(id);
    }

    private void OnSelectionChanged(string id, bool on)
    {
        if (_tiles.ContainsKey(id)) _tiles[id].SetSelected(on);
    }

    private void OnMatched(string a, string b)
    {
        StartCoroutine(RemoveTile(a));
        StartCoroutine(RemoveTile(b));

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
        UpdateHud();
        ShowToast("本局通关! +" + ROUND_BONUS);
        StartCoroutine(WaitThenNextRound());
    }

    private void OnDeadlock()
    {
        ShowToast("无解，自动洗牌");
        DoShuffle();
    }

    // ---------- 提示 / 洗牌 / 重开 ----------

    private void OnHint()
    {
        if (_busy || _board == null) return;
        if (_hints <= 0) { ShowToast("提示次数用尽"); return; }
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
        DoShuffle();
    }

    private void DoShuffle()
    {
        _busy = true;
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
        StartRound();
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
        if (tv.bodyImage != null) tv.bodyImage.color = new Color(1f, 0.4f, 0.4f);
        yield return new WaitForSeconds(0.25f);
        if (tv != null && tv.bodyImage != null) tv.bodyImage.color = Color.white;
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
        if (_roundLabel != null) _roundLabel.text = "第 " + _round + " 局";
        if (_hintBtn != null)
        {
            var txt = _hintBtn.GetComponentInChildren<Text>();
            if (txt != null) txt.text = "提示(" + _hints + ")";
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
}
