using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Coffee.UIEffects;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays UIEffect Dissolve on every visible graphic under a card sheet root.
/// Transition texture/settings are copied from BarriarDamage.prefab (Transition-Noise00).
/// </summary>
public static class CardDissolvePlayer
{
    private const float FallbackDurationSec = 0.8f;
    private const string BarriarDamagePrefabPath = "Prefab/BarriarDamage";
    private const string BarriarBackgroundChildName = "BarriarBackground";

    private static UIEffect _templateEffect;
    private static UIEffectTweener _templateTweener;

    /// <summary>Dissolve the entire card sheet (BG, artwork, texts, icons).</summary>
    public static async Task PlayAsync(GameObject sheetRoot, CancellationToken ct)
    {
        if (sheetRoot == null || ct.IsCancellationRequested) return;

        CacheDissolveTemplate();
        var hosts = CollectEffectHosts(sheetRoot);
        if (hosts.Count == 0)
        {
            await Task.Delay(Mathf.RoundToInt(FallbackDurationSec * 1000f), ct);
            return;
        }

        var tasks = new List<Task>(hosts.Count);
        foreach (var host in hosts)
            tasks.Add(PlayOnHostAsync(host, ct));

        await Task.WhenAll(tasks);
    }

    /// <summary>Dissolve an already-instantiated <see cref="CardSheetDisplay"/>.</summary>
    public static Task PlayAsync(CardSheetDisplay sheetDisplay, CancellationToken ct)
    {
        if (sheetDisplay == null) return Task.CompletedTask;
        return PlayAsync(sheetDisplay.gameObject, ct);
    }

    private static async Task PlayOnHostAsync(GameObject hostGo, CancellationToken ct)
    {
        if (hostGo == null || ct.IsCancellationRequested) return;

        var effect = hostGo.GetComponent<UIEffect>();
        if (effect == null)
            effect = hostGo.AddComponent<UIEffect>();

        ApplyDissolveSettings(effect);

        var tweener = hostGo.GetComponent<UIEffectTweener>();
        if (tweener == null)
            tweener = hostGo.AddComponent<UIEffectTweener>();

        ApplyTweenerSettings(tweener);

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

    private static List<GameObject> CollectEffectHosts(GameObject sheetRoot)
    {
        var list = new List<GameObject>(12);
        var seen = new HashSet<int>();

        if (sheetRoot == null) return list;

        var graphics = sheetRoot.GetComponentsInChildren<Graphic>(includeInactive: true);
        for (int i = 0; i < graphics.Length; i++)
        {
            var graphic = graphics[i];
            if (graphic == null || !graphic.gameObject.activeInHierarchy) continue;
            int id = graphic.gameObject.GetInstanceID();
            if (!seen.Add(id)) continue;
            list.Add(graphic.gameObject);
        }

        return list;
    }

    private static void CacheDissolveTemplate()
    {
        if (_templateEffect != null) return;

        var prefab = Resources.Load<GameObject>(BarriarDamagePrefabPath);
        if (prefab == null) return;

        var bg = prefab.transform.Find(BarriarBackgroundChildName);
        if (bg == null) return;

        _templateEffect = bg.GetComponent<UIEffect>();
        _templateTweener = bg.GetComponent<UIEffectTweener>();
    }

    private static void ApplyDissolveSettings(UIEffect effect)
    {
        if (effect == null) return;

        if (_templateEffect != null)
        {
            effect.transitionFilter = _templateEffect.transitionFilter;
            effect.transitionTexture = _templateEffect.transitionTexture;
            effect.transitionWidth = _templateEffect.transitionWidth;
            effect.transitionSoftness = _templateEffect.transitionSoftness;
            effect.transitionReverse = _templateEffect.transitionReverse;
            effect.transitionKeepAspectRatio = _templateEffect.transitionKeepAspectRatio;
            effect.transitionColor = _templateEffect.transitionColor;
            effect.transitionColorFilter = _templateEffect.transitionColorFilter;
        }
        else
        {
            var filterProp = typeof(UIEffect).GetProperty("transitionFilter");
            if (filterProp != null && filterProp.PropertyType.IsEnum)
                filterProp.SetValue(effect, System.Enum.ToObject(filterProp.PropertyType, 3));
        }

        effect.transitionRate = 0f;
    }

    private static void ApplyTweenerSettings(UIEffectTweener tweener)
    {
        if (tweener == null || _templateTweener == null) return;

        tweener.duration = _templateTweener.duration;
        tweener.curve = _templateTweener.curve;
        tweener.delay = _templateTweener.delay;
    }
}
