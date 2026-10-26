using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 单张牌的视图：牌底(Body) + 牌面(Face) + 背面(Back) + 阴影(Shadow)，
/// 由 Point 容器控制 rotation.y 翻转。实现点击、选中/提示高亮、
/// 提起/放下、正反面切换、翻转动画，以及供碰撞流程编排的通用移动/缩放原语。
/// 所有实例由对象池复用（TileView.ResetView 复位状态）。
/// </summary>
public class TileView : MonoBehaviour, IPointerClickHandler
{
    public string tileId;
    public int faceIndex;

    public Image bodyImage;   // 正面牌底（与 Back 互换 sprite 实现正反面）
    public Image faceImage;   // 牌面图案（正面显示，反面隐藏）
    public Image backImage;   // 背面牌底
    public Image shadowImage; // 阴影（提起时隐藏）
    public Transform point;   // 翻转轴容器（控制 localEulerAngles.y）
    public Image bodySub1, bodySub2, backSub1, backSub2; // 新增子层（背面状态需同步替换图片）
    public Sprite frontSprite; // 正面牌底图（body/body_1/body_2 在正面时使用的图）
    public Sprite backSprite;  // 等级背面图 levelN（back/back_1/back_2 在正面时使用的图）

    public Action<string> onClick;
    public Vector2 homePos;   // LayoutTiles 设置的原位（boardRoot 局部坐标）

    private bool _selected;
    private bool _hint;
    private bool _faceUp = true;
    private bool _flipping;
    public bool wasFaceDown; // 布置时随机设为反面（取消选中时切回）
    private Color _baseColor = Color.white;

    public void SetBaseColor(Color c) { _baseColor = c; }

    public void SetSelected(bool v) { _selected = v; UpdateTint(); }
    public void SetHint(bool v) { _hint = v; UpdateTint(); }

    private void UpdateTint()
    {
        if (bodyImage == null) return;
        Color c = _baseColor;
        if (_selected) c = new Color(1.0f, 0.84f, 0.40f);   // 金色（选中）
        else if (_hint) c = new Color(0.40f, 1.0f, 1.0f);  // 青色（提示）
        bodyImage.color = c;
        if (bodySub1 != null) bodySub1.color = c; // Body_1/Body_2 同步选中/提示色
        if (bodySub2 != null) bodySub2.color = c;
    }

    public void SetFaceSprite(Sprite tex) { if (faceImage != null) faceImage.sprite = tex; }

    public void SetAlpha(float a)
    {
        if (bodyImage != null) bodyImage.color = new Color(bodyImage.color.r, bodyImage.color.g, bodyImage.color.b, a);
        if (faceImage != null) faceImage.color = new Color(faceImage.color.r, faceImage.color.g, faceImage.color.b, a);
        if (backImage != null) backImage.color = new Color(backImage.color.r, backImage.color.g, backImage.color.b, a);
    }

    // ---------- 提起 / 放下 ----------
    public void Lift() { if (shadowImage != null) shadowImage.enabled = false; }
    public void Drop() { if (shadowImage != null) shadowImage.enabled = true; }

    // ---------- 通用移动 / 缩放（供碰撞流程编排） ----------
    public Coroutine MoveTo(Vector2 target, float dur, Action onDone = null) { return StartCoroutine(TweenTo(target, dur, onDone)); }
    public void TweenScale(float s, float dur, Action onDone = null) { StartCoroutine(ScaleTo(s, dur, onDone)); }

    private IEnumerator TweenTo(Vector2 target, float dur, Action onDone = null)
    {
        var tr = GetComponent<RectTransform>();
        Vector2 from = tr.anchoredPosition;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            tr.anchoredPosition = Vector2.Lerp(from, target, k);
            yield return null;
        }
        tr.anchoredPosition = target;
        onDone?.Invoke();
    }

    private IEnumerator ScaleTo(float s, float dur, Action onDone = null)
    {
        var tr = GetComponent<RectTransform>();
        Vector3 from = tr.localScale;
        Vector3 to = new Vector3(s, s, 1f);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            tr.localScale = Vector3.Lerp(from, to, k);
            yield return null;
        }
        tr.localScale = to;
        onDone?.Invoke();
    }

    // ---------- 对象池复用 / 重开时复位视图状态 ----------
    public void ResetView()
    {
        StopAllCoroutines();
        transform.localScale = Vector3.one;
        SetAlpha(1);
        _selected = false;
        _hint = false;
        _flipping = false;
        wasFaceDown = false;
        if (bodyImage != null) bodyImage.color = _baseColor;
        if (bodySub1 != null) bodySub1.color = _baseColor;
        if (bodySub2 != null) bodySub2.color = _baseColor;
        if (point != null) point.localEulerAngles = Vector3.zero;
        ToggleFace(true); // 回到正面
        if (shadowImage != null) shadowImage.enabled = true;
        if (faceImage != null) faceImage.enabled = true;
    }

    // ---------- 正反面 ----------
    // toFront=true: 正面（body/body_1/body_2=front，back/back_1/back_2=levelN）
    // toFront=false: 反面（相反）
    public void ToggleFace(bool toFront)
    {
        _faceUp = toFront;
        if (faceImage != null) faceImage.enabled = toFront;
        Sprite bodyUse = toFront ? frontSprite : backSprite;   // 正面态用 front，反面态用 levelN
        Sprite backUse = toFront ? backSprite : frontSprite;   // 反面态用 front
        if (bodyImage != null) bodyImage.sprite = bodyUse;
        if (bodySub1 != null) bodySub1.sprite = bodyUse;
        if (bodySub2 != null) bodySub2.sprite = bodyUse;
        if (backImage != null) backImage.sprite = backUse;
        if (backSub1 != null) backSub1.sprite = backUse;
        if (backSub2 != null) backSub2.sprite = backUse;
    }

    public bool IsFaceUp() { return _faceUp; }

    // ---------- 翻转（提起 -> 半程切换 -> 落下） ----------
    public void Flip()
    {
        if (_flipping) return;
        StartCoroutine(FlipRoutine());
    }

    private IEnumerator FlipRoutine()
    {
        _flipping = true;
        Lift();
        yield return RotateY(0f, 90f, 0.12f);
        ToggleFace(!_faceUp);
        yield return RotateY(90f, 0f, 0.12f);
        Drop();
        _flipping = false;
    }

    private IEnumerator RotateY(float from, float to, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float y = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
            if (point != null) point.localEulerAngles = new Vector3(0f, y, 0f);
            yield return null;
        }
        if (point != null) point.localEulerAngles = new Vector3(0f, to, 0f);
    }

    // 布置关卡时直接设为反面（无动画），并标记 wasFaceDown 以便取消选中时切回
    public void SetFaceDownInitial()
    {
        ToggleFace(false);
        wasFaceDown = true;
    }

    // 翻转到正面（指定总时长，分两段各一半），完成后回调
    public void FlipToFront(float dur, Action onDone = null)
    {
        if (_faceUp) { onDone?.Invoke(); return; }
        if (_flipping) { onDone?.Invoke(); return; }
        StartCoroutine(FlipRoutineTo(true, dur, onDone));
    }

    // 翻转到反面（指定总时长，分两段各一半），完成后回调
    public void FlipToBack(float dur, Action onDone = null)
    {
        if (!_faceUp) { onDone?.Invoke(); return; }
        if (_flipping) { onDone?.Invoke(); return; }
        StartCoroutine(FlipRoutineTo(false, dur, onDone));
    }

    private IEnumerator FlipRoutineTo(bool toFront, float dur, Action onDone)
    {
        _flipping = true;
        Lift();
        float half = Mathf.Max(0.01f, dur * 0.5f);
        yield return RotateY(0f, 90f, half);
        ToggleFace(toFront);
        yield return RotateY(90f, 0f, half);
        Drop();
        _flipping = false;
        onDone?.Invoke();
    }

    public void OnPointerClick(PointerEventData eventData) { onClick?.Invoke(tileId); }
}
