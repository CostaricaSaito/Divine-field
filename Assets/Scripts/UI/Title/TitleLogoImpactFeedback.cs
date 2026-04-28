using System.Collections;
using UnityEngine;

/// <summary>
/// 剣着弾など：指定 <see cref="RectTransform"/> の <b>縦方向のみ</b>の揺れ（減衰付きランダムオフセット）。
/// ランダム揺れで「着地のガタッ」とした体感に寄せています。
/// ロゴの親を指定すると元画像も揺れます。元は静止させたい場合はロゴの兄弟（空 Rect）だけを揺らすなどしてください。
/// </summary>
[DisallowMultipleComponent]
public sealed class TitleLogoImpactFeedback : MonoBehaviour
{
    [Header("画面揺れ（縦のみ）")]
    [SerializeField] private RectTransform shakeTarget;
    [SerializeField] [Min(0.01f)] private float shakeDuration = 0.28f;
    [SerializeField] [Min(0f)] private float shakeMagnitude = 14f;
    [SerializeField] private bool useUnscaledTime = true;

    private Vector2 _shakeBase;
    private Coroutine _impactRoutine;

    /// <summary>Y 揺れを再生（進行中なら打ち切ってやり直し）。</summary>
    public void PlayImpact()
    {
        if (!isActiveAndEnabled || !Application.isPlaying) return;
        if (shakeTarget == null) return;

        if (_impactRoutine != null)
            StopCoroutine(_impactRoutine);
        _impactRoutine = StartCoroutine(CoImpact());
    }

    private IEnumerator CoImpact()
    {
        _shakeBase = shakeTarget.anchoredPosition;
        var elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            var dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsed += dt;

            var damp = 1f - Mathf.Clamp01(elapsed / shakeDuration);
            var m = shakeMagnitude * damp;
            shakeTarget.anchoredPosition = _shakeBase + new Vector2(0f, Random.Range(-1f, 1f) * m);

            yield return null;
        }

        shakeTarget.anchoredPosition = _shakeBase;
        _impactRoutine = null;
    }
}
