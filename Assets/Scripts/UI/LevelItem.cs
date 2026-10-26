using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// 关卡列表项预制体组件（挂在 LevelItem 预制体根，根即 Button）。
/// 预制体结构：根 Button（背景 Image）+ 子 Text。Bind 设置文本 / 解锁态 / 点击回调。
/// </summary>
public class LevelItem : MonoBehaviour
{
    private Button _btn;
    private Text _txt;

    private void Awake()
    {
        _btn = GetComponent<Button>();
        _txt = GetComponentInChildren<Text>();
    }

    public void Bind(int levelIndex, string name, bool locked, UnityAction<int> onPick)
    {
        if (_txt != null) _txt.text = name;
        if (_btn != null)
        {
            _btn.interactable = !locked;
            _btn.onClick.RemoveAllListeners();
            if (!locked) _btn.onClick.AddListener(() => onPick(levelIndex));
        }
    }
}
