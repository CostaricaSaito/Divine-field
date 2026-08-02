using System.Threading;
using System.Threading.Tasks;
using Coffee.UIEffects;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays UIEffect Dissolve on a card sheet GameObject at runtime.
/// </summary>
public static class CardDissolvePlayer
{
    private const float FallbackDurationSec = 0.8f;

    public static async Task PlayAsync(GameObject target, CancellationToken ct)
    {
        if (target == null || ct.IsCancellationRequested) return;

        var graphic = target.GetComponentInChildren<Graphic>(true);
        if (graphic == null)
        {
            await Task.Delay(Mathf.RoundToInt(FallbackDurationSec * 1000f), ct);
            return;
        }

        var host = graphic.gameObject;
        var effect = host.GetComponent<UIEffect>();
        if (effect == null)
            effect = host.AddComponent<UIEffect>();

        ConfigureDissolve(effect);

        var tweener = host.GetComponent<UIEffectTweener>();
        if (tweener == null)
            tweener = host.AddComponent<UIEffectTweener>();

        effect.transitionRate = 0f;
        tweener.wrapMode = UIEffectTweener.WrapMode.Once;
        tweener.playOnEnable = UIEffectTweener.PlayOnEnable.None;
        tweener.Stop();
        tweener.ResetTime();
        tweener.PlayForward(true);

        float duration = tweener.totalTime > 0f ? tweener.totalTime : FallbackDurationSec;
        if (tweener.isTweening)
        {
            while (tweener.isTweening && !ct.IsCancellationRequested)
                await Task.Yield();
        }
        else
        {
            await Task.Delay(Mathf.RoundToInt(duration * 1000f), ct);
        }
    }

    private static void ConfigureDissolve(UIEffect effect)
    {
        if (effect == null) return;

        var filterProp = typeof(UIEffect).GetProperty("transitionFilter");
        if (filterProp != null && filterProp.PropertyType.IsEnum)
            filterProp.SetValue(effect, System.Enum.ToObject(filterProp.PropertyType, 3));

        effect.transitionRate = 0f;
    }
}
