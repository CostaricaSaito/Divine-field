using System.Collections;
using UnityEngine;

/// <summary>
/// 剣着地後：ランダムな間隔で <b>縦だけ</b>短い揺れを繰り返します。
/// 各回は上または下へ一方向に振り、元位置へ戻ります（往復で反対側へは行きません）。
/// <see cref="BeginAfterIntro"/> が呼ばれるまでコルーチンは始まりません（<see cref="TitleLogoIntroController"/> から接続）。
/// </summary>
[DisallowMultipleComponent]
public sealed class TitleLogoRandomImpact : MonoBehaviour
{
    [Header("揺れ対象（縦のみ。ロゴの兄弟 Rect 推奨）")]
    [SerializeField] private RectTransform shakeTarget;

    [Header("ランダム間隔（秒）")]
    [SerializeField] [Min(0.1f)] private float minInterval = 3f;
    [SerializeField] [Min(0.1f)] private float maxInterval = 9f;

    [Header("1回の揺れ")]
    [SerializeField] [Min(0.01f)] private float shakeDuration = 0.22f;
    [SerializeField] [Min(0f)] private float shakeMagnitude = 9f;

    [SerializeField] private bool useUnscaledTime = true;

    private Vector2 _shakeRest;
    private Coroutine _loop;
    private bool _introBegan;

    private void OnDisable()
    {
        if (_loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }

        if (_introBegan && shakeTarget != null)
            shakeTarget.anchoredPosition = _shakeRest;
    }

    /// <summary><see cref="TitleLogoIntroController"/> から剣着地後に呼ぶ。</summary>
    public void BeginAfterIntro()
    {
        if (!isActiveAndEnabled || !Application.isPlaying) return;
        if (shakeTarget == null) return;

        _introBegan = true;
        _shakeRest = shakeTarget.anchoredPosition;

        if (_loop != null)
            StopCoroutine(_loop);
        _loop = StartCoroutine(CoRandomVerticalShakes());
    }

    private IEnumerator CoRandomVerticalShakes()
    {
        for (;;)
        {
            if (!isActiveAndEnabled) yield break;

            var w = Random.Range(minInterval, maxInterval);
            if (useUnscaledTime)
                yield return new WaitForSecondsRealtime(w);
            else
                yield return new WaitForSeconds(w);

            if (!isActiveAndEnabled) yield break;
            yield return CoOneVerticalShake();
        }
    }

    private IEnumerator CoOneVerticalShake()
    {
        _shakeRest = shakeTarget.anchoredPosition;
        var signY = Random.value < 0.5f ? -1f : 1f;
        var elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            if (shakeTarget == null) yield break;

            var dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsed += dt;

            var t = Mathf.Clamp01(elapsed / shakeDuration);
            var envelope = Mathf.Sin(Mathf.PI * t);
            shakeTarget.anchoredPosition = _shakeRest + new Vector2(0f, signY * shakeMagnitude * envelope);

            yield return null;
        }

        if (shakeTarget != null)
            shakeTarget.anchoredPosition = _shakeRest;
    }
}
