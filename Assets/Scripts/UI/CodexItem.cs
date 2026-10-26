using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 图鉴行项预制体组件（挂在 CodexItem 预制体根）。
/// 预制体结构：根（HorizontalLayoutGroup）+ Icon(Image) + Txt(Text) + Color(Image)。
/// Bind 设置花色图标 / 数量 / 等级色块或等级背图。
/// </summary>
public class CodexItem : MonoBehaviour
{
    private Image _icon;
    private Text _txt;
    private Image _color;

    private void Awake()
    {
        _icon = transform.Find("Icon")?.GetComponent<Image>();
        _txt = transform.Find("Txt")?.GetComponent<Text>();
        _color = transform.Find("Color")?.GetComponent<Image>();
    }

    public void Bind(Sprite face, int count, int lvl, Sprite levelBack, Color codeColor)
    {
        if (_icon != null) { _icon.sprite = face; _icon.preserveAspect = true; }
        if (_txt != null) _txt.text = "x" + count + "  Lv" + lvl;
        if (_color != null)
        {
            _color.preserveAspect = true;
            if (levelBack != null) _color.sprite = levelBack;
            else _color.color = codeColor;
        }
    }
}
