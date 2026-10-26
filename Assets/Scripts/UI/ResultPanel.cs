using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// 结算面板组件（挂在 ResultPanel 预制体根）。预制体含：Title(Text) + Info(Text) +
/// Btn_Next(Button) + Btn_Retry(Button) + Btn_Menu(Button)。Init 绑定回调；
/// Show 设置文本与按钮显隐。最后一关通关时 Btn_Next 复用为"从头再玩"（循环）。
/// </summary>
public class ResultPanel : MonoBehaviour
{
    private Text _title;
    private Text _info;
    private Button _next;
    private UnityAction _onNext;
    private UnityAction _onReplayAll;

    public void Init(UnityAction onNext, UnityAction onRetry, UnityAction onMenu, UnityAction onReplayAll = null)
    {
        _title = transform.Find("Title")?.GetComponent<Text>();
        _info = transform.Find("Info")?.GetComponent<Text>();
        _next = transform.Find("Btn_Next")?.GetComponent<Button>();
        _onNext = onNext;
        _onReplayAll = onReplayAll;
        transform.Find("Btn_Retry")?.GetComponent<Button>()?.onClick.AddListener(onRetry);
        transform.Find("Btn_Menu")?.GetComponent<Button>()?.onClick.AddListener(onMenu);
    }

    // win: 是否过关；hasNext: 是否还有下一关；isLast: 是否最后一关（已通关全部 -> 循环再玩）
    public void Show(bool win, string info, bool hasNext, bool isLast)
    {
        if (_title != null) _title.text = win ? (isLast ? "全部通关!" : "过关!") : "失败";
        if (_info != null) _info.text = info;
        if (_next != null)
        {
            bool showNext = win && (hasNext || isLast);
            _next.gameObject.SetActive(showNext);
            _next.onClick.RemoveAllListeners();
            if (showNext)
            {
                var t = _next.GetComponentInChildren<Text>();
                if (t != null) t.text = hasNext ? "下一关" : "从头再玩";
                var act = hasNext ? _onNext : _onReplayAll;
                if (act != null) _next.onClick.AddListener(act);
            }
        }
    }
}
