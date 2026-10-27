using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// 游戏主界面（HUD）预制体组件，挂在 GamePanel 预制体根。
/// 由 UiPrefabGenerator.BuildGame 生成，字段在生成时直接绑定；Init 绑定按钮/滑条回调。
/// GameController 优先从 Prefab/GamePanel 加载此预制体，缺失时回退到运行时 MakeGO 构建。
/// </summary>
public class GamePanel : MonoBehaviour
{
    public RectTransform topBar, bottomBar, boardRoot, toast;
    public Text scoreLabel, bestLabel, roundLabel, toastText, xLabel, yLabel;
    public Button hintBtn, shuffleBtn, restartBtn, pauseBtn, codexBtn, settingsBtn, backBtn;
    public Slider xOffsetSlider, yOffsetSlider;

    public void Init(UnityAction onHint, UnityAction onShuffle, UnityAction onRestart,
                     UnityAction onPause, UnityAction onCodex,
                     float xMin, float xMax, float xVal, UnityAction<float> onX,
                     float yMin, float yMax, float yVal, UnityAction<float> onY,
                     bool showSliders)
    {
        hintBtn?.onClick.AddListener(onHint);
        shuffleBtn?.onClick.AddListener(onShuffle);
        restartBtn?.onClick.AddListener(onRestart);
        pauseBtn?.onClick.AddListener(onPause);
        codexBtn?.onClick.AddListener(onCodex);
        if (xOffsetSlider != null)
        {
            xOffsetSlider.minValue = xMin; xOffsetSlider.maxValue = xMax; xOffsetSlider.value = xVal;
            xOffsetSlider.onValueChanged.AddListener(onX);
        }
        if (yOffsetSlider != null)
        {
            yOffsetSlider.minValue = yMin; yOffsetSlider.maxValue = yMax; yOffsetSlider.value = yVal;
            yOffsetSlider.onValueChanged.AddListener(onY);
        }
        SetSlidersVisible(showSliders);
    }

    public void SetSlidersVisible(bool v)
    {
        if (xOffsetSlider != null) xOffsetSlider.gameObject.SetActive(v);
        if (yOffsetSlider != null) yOffsetSlider.gameObject.SetActive(v);
        if (xLabel != null) xLabel.gameObject.SetActive(v);
        if (yLabel != null) yLabel.gameObject.SetActive(v);
    }
}
