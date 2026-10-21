using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 单张牌的视图：牌底(card_front) + 牌面(icon)。实现点击与选中/提示高亮。
/// </summary>
public class TileView : MonoBehaviour, IPointerClickHandler
{
    public string tileId;
    public int faceIndex;

    public Image bodyImage;
    public Image faceImage;

    public Action<string> onClick;

    private bool _selected;
    private bool _hint;
    private Color _baseColor = Color.white;

    // 记录预制体里微调好的牌底颜色，选中高亮取消后恢复为该底色
    public void SetBaseColor(Color c) { _baseColor = c; }

    public void SetSelected(bool v)
    {
        _selected = v;
        UpdateTint();
    }

    public void SetHint(bool v)
    {
        _hint = v;
        UpdateTint();
    }

    private void UpdateTint()
    {
        if (_selected) bodyImage.color = new Color(1.0f, 0.84f, 0.40f); // 金色
        else if (_hint) bodyImage.color = new Color(0.40f, 1.0f, 1.0f);  // 青色
        else bodyImage.color = _baseColor; // 恢复预制体底色
    }

    public void SetFaceSprite(Sprite tex)
    {
        if (faceImage != null) faceImage.sprite = tex;
    }

    public void SetAlpha(float a)
    {
        if (bodyImage != null) bodyImage.color = new Color(bodyImage.color.r, bodyImage.color.g, bodyImage.color.b, a);
        if (faceImage != null) faceImage.color = new Color(faceImage.color.r, faceImage.color.g, faceImage.color.b, a);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        onClick?.Invoke(tileId);
    }
}
