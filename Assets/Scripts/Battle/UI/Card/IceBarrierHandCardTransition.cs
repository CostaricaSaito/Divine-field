using System.Threading;
using System.Threading.Tasks;
using Coffee.UIEffects;
using UnityEngine;

/// <summary>
/// Shiva ice barrier: fade out hand card face, swap to FROZEN, fade in (no dissolve / destroy VFX).
/// </summary>
public static class IceBarrierHandCardTransition
{
    public const float FadeOutSeconds = 0.35f;
    public const float FadeInSeconds = 0.4f;

    /// <summary>
    /// Face-up card (original instance on UI) fades out, swaps to <paramref name="frozenCard"/>, fades in.
    /// </summary>
    public static async Task ReplaceFaceUpWithFrozenAsync(
        CardUI ui,
        CardData frozenCard,
        Sprite cardBack,
        CardData tempOriginalInstance,
        CancellationToken ct)
    {
        if (ui == null || frozenCard == null) return;

        var cg = GetOrAddCanvasGroup(ui.gameObject);
        float startAlpha = cg.alpha > 0.01f ? cg.alpha : 1f;
        cg.alpha = startAlpha;

        await AnimateAlphaAsync(cg, startAlpha, 0f, FadeOutSeconds, ct);
        if (ct.IsCancellationRequested) return;

        if (tempOriginalInstance != null)
            Object.Destroy(tempOriginalInstance);

        StripDissolveArtifacts(ui.transform);

        ui.Setup(frozenCard, cardBack, playerHandRareBackPresentation: true);
        frozenCard.cardUI = ui;
        ui.Reveal();

        cg.alpha = 0f;
        await AnimateAlphaAsync(cg, 0f, 1f, FadeInSeconds, ct);
        if (ct.IsCancellationRequested) return;

        ApplyFrozenHandPresentation(frozenCard);
    }

    static void ApplyFrozenHandPresentation(CardData frozenCard)
    {
        var bm = BattleManager.I;
        if (bm == null || frozenCard == null) return;

        bm.UpdateTotalATKDEFDisplay();
        BattleUIManager.I?.RefreshMagicCardInteractivity(bm.playerHand);
        bm.RefreshPlayerDefensePhaseInteractivity();

        if (CardRules.IsPassiveHandOnly(frozenCard))
            BattleUIManager.I?.SetCardInteractable(frozenCard, false);
    }

    static CanvasGroup GetOrAddCanvasGroup(GameObject go)
    {
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = go.AddComponent<CanvasGroup>();
        return cg;
    }

    static async Task AnimateAlphaAsync(
        CanvasGroup cg,
        float from,
        float to,
        float durationSec,
        CancellationToken ct)
    {
        if (cg == null) return;
        if (durationSec <= 0.0001f)
        {
            cg.alpha = to;
            return;
        }

        float t = 0f;
        while (t < durationSec && !ct.IsCancellationRequested)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / durationSec);
            cg.alpha = Mathf.Lerp(from, to, u);
            await Task.Yield();
        }

        if (!ct.IsCancellationRequested)
            cg.alpha = to;
    }

    /// <summary>Remove dissolve tweens that leave hand card graphics invisible.</summary>
    public static void StripDissolveArtifacts(Transform root)
    {
        if (root == null) return;

        var effects = root.GetComponentsInChildren<UIEffect>(true);
        for (int i = 0; i < effects.Length; i++)
        {
            var e = effects[i];
            if (e == null) continue;
            e.transitionRate = 0f;
            Object.Destroy(e);
        }

        var tweeners = root.GetComponentsInChildren<UIEffectTweener>(true);
        for (int i = 0; i < tweeners.Length; i++)
        {
            if (tweeners[i] != null)
                Object.Destroy(tweeners[i]);
        }
    }
}
