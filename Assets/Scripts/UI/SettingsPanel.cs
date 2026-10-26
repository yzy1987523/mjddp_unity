using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 设置面板组件：挂在 SettingsPanel 预制体根上。
/// 预制体已包含完整 UI 层级（遮罩 / 米色卡片 / 标题 / 音乐·音效·震动开关 / 画质三档 / 语音 / 返回）。
/// 本组件只负责查找并绑定这些子对象、回写 SaveManager.SettingsData，不在运行时生成 UI。
/// 子对象命名约定（由 SettingsPanelGenerator 生成时保持一致）：
///   SettingsCard
///     Row_音乐 / Row_音效 / Row_震动   -> Track(Button+Image) -> Handle(Image)
///     Row_Quality -> Btn_0 / Btn_1 / Btn_2
///     Row_Lang   -> LangBtn(Button)
///   BackBtn
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    private SaveManager.SettingsData _data;
    private Action _onBack;
    private Action _onAudio;
    private Action _onQuality;
    private Action _onSave;

    private Image _musicTrack, _sfxTrack, _vibTrack;
    private RectTransform _musicHandle, _sfxHandle, _vibHandle;
    private Button[] _qualityBtns = new Button[3];
    private Button _langBtn;
    private Text _langText;

    /// <summary>由 GameController 在实例化预制体后调用：查找子对象并绑定回调。</summary>
    public void Init(SaveManager.SettingsData data, Action onBack, Action onAudio, Action onQuality, Action onSave)
    {
        _data = data; _onBack = onBack; _onAudio = onAudio; _onQuality = onQuality; _onSave = onSave;
        Bind();
    }

    private void Bind()
    {
        var card = transform.Find("SettingsCard");
        if (card == null) { Debug.LogWarning("[Settings] 预制体缺少 SettingsCard，请重新运行 Tools/生成设置面板预制体"); return; }

        (_musicTrack, _musicHandle) = BindToggle(card, "Row_音乐", () => _data.music, v => { _data.music = v; _onSave(); _onAudio(); });
        (_sfxTrack, _sfxHandle) = BindToggle(card, "Row_音效", () => _data.sfx, v => { _data.sfx = v; _onSave(); _onAudio(); });
        (_vibTrack, _vibHandle) = BindToggle(card, "Row_震动", () => _data.vibration, v => { _data.vibration = v; _onSave(); });

        var qrow = card.Find("Row_Quality");
        if (qrow != null)
        {
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                _qualityBtns[i] = qrow.Find("Btn_" + i).GetComponent<Button>();
                if (_qualityBtns[i] != null)
                    _qualityBtns[i].onClick.AddListener(() => { _data.quality = idx; _onSave(); _onQuality(); RefreshQuality(); });
            }
            RefreshQuality();
        }

        var lrow = card.Find("Row_Lang");
        if (lrow != null)
        {
            _langBtn = lrow.Find("LangBtn").GetComponent<Button>();
            _langText = _langBtn != null ? _langBtn.GetComponentInChildren<Text>() : null;
            if (_langBtn != null) _langBtn.onClick.AddListener(() => { _data.language = (_data.language + 1) % 2; _onSave(); RefreshLang(); });
            RefreshLang();
        }

        var back = transform.Find("BackBtn");
        if (back != null) { var b = back.GetComponent<Button>(); if (b != null) b.onClick.AddListener(() => _onBack()); }
    }

    private (Image, RectTransform) BindToggle(Transform card, string rowName, Func<bool> get, Action<bool> set)
    {
        Image track = null; RectTransform handle = null;
        var row = card.Find(rowName);
        if (row == null) return (null, null);
        var tr = row.Find("Track");
        if (tr == null) return (null, null);
        track = tr.GetComponent<Image>();
        var btn = tr.GetComponent<Button>();
        handle = tr.Find("Handle")?.GetComponent<RectTransform>();
        Action refresh = () =>
        {
            bool on = get();
            if (track != null) track.color = on ? new Color(0.45f, 0.58f, 0.35f) : new Color(0.78f, 0.74f, 0.66f);
            if (handle != null) handle.anchoredPosition = new Vector2(on ? 30f : -30f, 0f);
        };
        refresh();
        if (btn != null) btn.onClick.AddListener(() => { set(!get()); refresh(); });
        return (track, handle);
    }

    private void RefreshQuality()
    {
        for (int i = 0; i < 3; i++)
        {
            bool sel = (_data.quality == i);
            if (_qualityBtns[i] == null) continue;
            if (_qualityBtns[i].image != null)
                _qualityBtns[i].image.color = sel ? new Color(0.45f, 0.58f, 0.35f) : new Color(0.85f, 0.80f, 0.68f);
            var t = _qualityBtns[i].GetComponentInChildren<Text>();
            if (t != null) t.color = sel ? Color.white : new Color(0.35f, 0.30f, 0.22f);
        }
    }

    private void RefreshLang()
    {
        if (_langText != null) _langText.text = (_data.language == 0) ? "简体中文 ▾" : "English ▾";
    }
}
