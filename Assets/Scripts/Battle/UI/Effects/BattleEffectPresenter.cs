using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 全画面系の演出を司るサブマネージャ。
///
/// 【主な責務】
/// - 全画面白フラッシュ（反射バウンス・無効化・レアドロー等と共用）
/// - 往生アニメ直後の白フラッシュ → GAMESET 画像登場 → スケール演出 → フェード
///
/// 使用する Canvas は <see cref="BattleUIManager.GetMainUICanvas"/> を参照する。
/// </summary>
public class BattleEffectPresenter : MonoBehaviour
{
    [Header("ゲーム終了：GAMESET 表示")]
    [Tooltip("中間点・フェード前の基準スケール（1.0 = Rect の描画大きさに対する乗数）。")]
    [SerializeField] private float gameSetDisplayScale = 1.1f;
    [Tooltip("出現直後の大きさは「基準のこの倍率」。例:5 で基準の 5 倍。")]
    [SerializeField] private float gameSetStartScaleFactor = 5f;
    [SerializeField] private float gameSetShrinkToBaseDuration = 0.2f;
    [SerializeField] private float gameSetExpandDuration = 1f;
    [Tooltip("中間点からの最終拡大。例:1.5 で基準の 1.5 倍まで。")]
    [SerializeField] private float gameSetEndScaleOfBase = 1.5f;
    [SerializeField] private float gameSetFadeOutDuration = 0.4f;
    [Tooltip("GameSet スケール・フェードのイージング（前半で変化量が大きく、後半はゆるやか＝Out 系推奨）。")]
    [SerializeField] private LeanTweenType gameSetScaleEase = LeanTweenType.easeOutCubic;
    [SerializeField] private LeanTweenType gameSetFadeEase = LeanTweenType.easeOutCubic;

    private GameObject _fullscreenWhiteFlashGo;
    private GameObject _gameSetOverlayGo;

    private const string GameSetSpriteAddress = "Assets/Images/06_UIパーツ/GAMESET.png";
    private const string PostOjyouGameGongSeAddress = "Assets/SE/試合終了のゴング.mp3";

    private Canvas ResolveCanvas() => BattleUIManager.I != null ? BattleUIManager.I.GetMainUICanvas() : null;

    /// <summary>反射の弾き返しと同じ全画面白フラッシュ（ミリ秒）。劣勢時レアドロー等からも利用。</summary>
    public void PlayFullscreenWhiteFlashMs(float durationMs)
    {
        PlayFullscreenColorFlashMs(Color.white, durationMs);
    }

    /// <summary>全画面を指定色で一瞬表示（ミリ秒）。白フラッシュと同じオーバーレイを使用。</summary>
    public void PlayFullscreenColorFlashMs(Color flashColor, float durationMs)
    {
        StartCoroutine(CoFullscreenColorFlashMs(flashColor, durationMs));
    }

    private IEnumerator CoFullscreenColorFlashMs(Color flashColor, float durationMs)
    {
        var canvas = ResolveCanvas();
        if (canvas == null) yield break;

        if (_fullscreenWhiteFlashGo == null)
        {
            var go = new GameObject("FullscreenWhiteFlash");
            go.transform.SetParent(canvas.transform, false);
            go.AddComponent<Image>();
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            _fullscreenWhiteFlashGo = go;
        }

        var img = _fullscreenWhiteFlashGo.GetComponent<Image>();
        if (img != null)
        {
            img.raycastTarget = false;
            img.color = flashColor;
        }

        _fullscreenWhiteFlashGo.transform.SetAsLastSibling();
        _fullscreenWhiteFlashGo.SetActive(true);
        yield return new WaitForSecondsRealtime(durationMs * 0.001f);
        if (_fullscreenWhiteFlashGo != null)
        {
            _fullscreenWhiteFlashGo.SetActive(false);
            if (img != null)
                img.color = Color.white;
        }
    }

    /// <summary>
    /// 往生アニメ終了直後：反射「弾き返し」と同じ全画面白フラッシュ → 中央に GAMESET 大表示＋ゴング SE。一定時間後に画像を消す。
    /// </summary>
    public async Task ShowPostOjyouFlashAndGameSetAsync(CancellationToken ct = default)
    {
        var canvas = ResolveCanvas();
        if (canvas == null) return;

        PlayFullscreenWhiteFlashMs(50f);
        try
        {
            await Task.Delay(50, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_gameSetOverlayGo != null)
        {
            Destroy(_gameSetOverlayGo);
            _gameSetOverlayGo = null;
        }

        var h = Addressables.LoadAssetAsync<Sprite>(GameSetSpriteAddress);
        var tcs = new TaskCompletionSource<Sprite>();
        h.Completed += op =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded && op.Result != null)
                tcs.TrySetResult(op.Result);
            else
            {
                Debug.LogWarning("[BattleEffectPresenter] GAMESET スプライトの読み込みに失敗: " + GameSetSpriteAddress);
                tcs.TrySetResult(null);
            }
        };

        Sprite sprite;
        try
        {
            sprite = await tcs.Task;
        }
        catch (Exception)
        {
            sprite = null;
        }

        if (sprite == null) return;

        var go = new GameObject("GameSetOverlay");
        go.transform.SetParent(canvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.1f, 0.1f);
        rt.anchorMax = new Vector2(0.9f, 0.9f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.type = Image.Type.Simple;
        var cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        _gameSetOverlayGo = go;
        go.transform.SetAsLastSibling();

        SoundEffectPlayer.I?.Play(PostOjyouGameGongSeAddress);

        // 出現: 大きさ ~5x・真っ白 → 0.1s で基準 → 1s で基準の 1.5 倍 → フェードアウト
        try
        {
            await AnimateGameSetOverlayAsync(rt, img, cg, ct);
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (_gameSetOverlayGo != null)
            {
                Destroy(_gameSetOverlayGo);
                _gameSetOverlayGo = null;
            }
        }
    }

    private async Task AnimateGameSetOverlayAsync(RectTransform rt, Image img, CanvasGroup canvasGroup, CancellationToken ct)
    {
        if (rt == null || img == null) return;

        GameObject go = rt.gameObject;
        float baseScale = Mathf.Max(0.01f, gameSetDisplayScale);
        float fromScale = baseScale * Mathf.Max(0.1f, gameSetStartScaleFactor);
        float midScale = baseScale;
        float toScale = baseScale * Mathf.Max(0.1f, gameSetEndScaleOfBase);

        img.color = Color.white;
        img.material = null;
        rt.localScale = new Vector3(fromScale, fromScale, 1f);

        float dur0 = Mathf.Max(0.01f, gameSetShrinkToBaseDuration);
        float dur1 = Mathf.Max(0.01f, gameSetExpandDuration);
        var easeS = gameSetScaleEase;

        void ApplyScale(float s)
        {
            if (rt != null) rt.localScale = new Vector3(s, s, 1f);
        }

        try
        {
            await LeanTweenValueFloatWithEaseAsync(
                go, ApplyScale, fromScale, midScale, dur0, easeS, ct);
            if (rt != null)
                rt.localScale = new Vector3(midScale, midScale, 1f);
            await LeanTweenValueFloatWithEaseAsync(
                go, ApplyScale, midScale, toScale, dur1, easeS, ct);
            if (rt != null)
                rt.localScale = new Vector3(toScale, toScale, 1f);
        }
        catch (OperationCanceledException)
        {
            throw;
        }

        if (canvasGroup == null) return;
        try
        {
            float durFade = Mathf.Max(0.01f, gameSetFadeOutDuration);
            await LeanTweenValueFloatWithEaseAsync(
                go, a => { if (canvasGroup != null) canvasGroup.alpha = a; },
                1f, 0f, durFade, gameSetFadeEase, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    /// <summary>LeanTween で float をトゥイーン。easeOut 系は前半の変化が大きく、後半は緩やかに見える。</summary>
    private static async Task LeanTweenValueFloatWithEaseAsync(
        GameObject go,
        Action<float> onUpdate,
        float from,
        float to,
        float time,
        LeanTweenType ease,
        CancellationToken ct)
    {
        if (go == null || onUpdate == null) return;
        if (time < 0.0001f)
        {
            onUpdate(to);
            return;
        }
        onUpdate(from);
        var tcs = new TaskCompletionSource<bool>();
        var reg = ct.Register(() =>
        {
            if (go != null) LeanTween.cancel(go);
            tcs.TrySetCanceled();
        });
        try
        {
            LeanTween.value(go, onUpdate, from, to, time)
                .setEase(ease)
                .setIgnoreTimeScale(true)
                .setOnComplete(() =>
                {
                    if (!tcs.Task.IsCompleted)
                        tcs.TrySetResult(true);
                });
            await tcs.Task.ConfigureAwait(true);
        }
        finally
        {
            reg.Dispose();
        }
    }
}
