using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// 广告提示框组件（挂在 AdPromptPanel 预制体根）。
/// 当提示/洗牌次数用尽时弹出，提示观看广告可 +1 次；含"确认观看广告"与"返回"两个按钮。
/// Init 在加载时取引用；Show 每次弹出时设置文案并绑定当次回调（确认会调用广告 SDK 占位逻辑）。
/// </summary>
public class AdPromptPanel : MonoBehaviour
{
    private Text _title;
    private Text _info;
    private Button _confirm;
    private Button _cancel;

    public void Init()
    {
        _title = transform.Find("Title")?.GetComponent<Text>();
        _info = transform.Find("Info")?.GetComponent<Text>();
        _confirm = transform.Find("Btn_Confirm")?.GetComponent<Button>();
        _cancel = transform.Find("Btn_Cancel")?.GetComponent<Button>();
    }

    // isHint: true=提示不足，false=洗牌不足。每次弹出重新绑定回调，避免串味
    public void Show(bool isHint, UnityAction onConfirm, UnityAction onCancel)
    {
        if (_title != null) _title.text = isHint ? "提示次数不足" : "洗牌次数不足";
        if (_info != null) _info.text = isHint
            ? "观看广告可获得 1 次提示"
            : "观看广告可获得 1 次洗牌";
        _confirm?.onClick.RemoveAllListeners();
        _cancel?.onClick.RemoveAllListeners();
        _confirm?.onClick.AddListener(onConfirm);
        _cancel?.onClick.AddListener(onCancel);
    }
}
