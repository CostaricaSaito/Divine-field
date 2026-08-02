using System.Threading;
using System.Threading.Tasks;
using Coffee.UIEffects;
using TMPro;
using UnityEngine;

/// <summary>
/// 大魔法 HP バリア被ダメ演出（BarriarDamage.prefab）。
/// 残 HP カウントダウン、破壊時は Dissolve と「バリア破壊」表示。
/// </summary>
public class BarriarDamagePopup : MonoBehaviour
{
    private const string DamageSePath = "Assets/SE/ガラスが割れる3.mp3";
    private const string BreakSePath = "Assets/SE/ガラスが割れる2.mp3";
    private const float DisplayTimeScale = 1.5f;
    private const float ValueStepSec = BattleStatCountRules.ValueStepSec;
    private const float HoldAfterCountSec = 0.35f * DisplayTimeScale;
    private const float BreakExtraHoldSec = 1.5f;
    private const float MinNoChangeHoldSec = 0.3f * DisplayTimeScale;
    private const float PostBreakTweenHoldSec = 0.15f * DisplayTimeScale;

    private TMP_Text _valueText;
    private GameObject _valueRoot;
    private GameObject _labelRoot;
    private GameObject _destructionRoot;
    private UIEffectTweener _backgroundTweener;
    private UIEffect _backgroundEffect;
    private bool _refsCached;

    private void Awake() => CacheRefs();

    /// <returns>演出のおおよその秒数（待機用）。</returns>
    public async Task<float> PlayAsync(
        int valueBefore,
        int valueAfter,
        bool barrierBroken,
        PlayerStatus target,
        CancellationToken ct)
    {
        CacheRefs();
        ResetVisualState();
        SetDisplayedValue(Mathf.Max(0, valueBefore));

        if (barrierBroken)
        {
            if (target != null)
                target.archMagicBarrierBreakFxPlayed = true;

            SoundEffectPlayer.I?.Play(DamageSePath);
            await CountdownAsync(valueBefore, 0, ct);
            float destroySec = await PlayDestructionAsync(ct);
            Destroy(gameObject);
            return destroySec;
        }

        if (valueAfter < valueBefore)
        {
            SoundEffectPlayer.I?.Play(DamageSePath);
            await CountdownAsync(valueBefore, valueAfter, ct);
            await Task.Delay(Mathf.RoundToInt(HoldAfterCountSec * 1000f), ct);
            Destroy(gameObject);
            return Mathf.Abs(valueBefore - valueAfter) * ValueStepSec + HoldAfterCountSec;
        }

        await Task.Delay(Mathf.RoundToInt(MinNoChangeHoldSec * 1000f), ct);
        Destroy(gameObject);
        return MinNoChangeHoldSec;
    }

    private void CacheRefs()
    {
        if (_refsCached) return;

        _valueRoot = FindChild(root: transform, "BarriarValue")?.gameObject;
        _valueText = _valueRoot != null ? _valueRoot.GetComponent<TMP_Text>() : null;
        _labelRoot = FindChild(transform, "BarrarLabel")?.gameObject;
        _destructionRoot = FindChild(transform, "BarriarDestruction")?.gameObject;

        var bg = FindChild(transform, "BarriarBackground");
        if (bg != null)
        {
            _backgroundTweener = bg.GetComponent<UIEffectTweener>();
            _backgroundEffect = bg.GetComponent<UIEffect>();
        }

        _refsCached = true;
    }

    private static Transform FindChild(Transform root, string childName)
    {
        if (root == null) return null;
        var direct = root.Find(childName);
        if (direct != null) return direct;

        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChild(root.GetChild(i), childName);
            if (found != null) return found;
        }
        return null;
    }

    private void ResetVisualState()
    {
        if (_valueRoot != null) _valueRoot.SetActive(true);
        if (_labelRoot != null) _labelRoot.SetActive(true);
        if (_destructionRoot != null) _destructionRoot.SetActive(false);

        if (_backgroundEffect != null)
            _backgroundEffect.transitionRate = 0f;

        if (_backgroundTweener != null)
        {
            _backgroundTweener.wrapMode = UIEffectTweener.WrapMode.Once;
            _backgroundTweener.playOnEnable = UIEffectTweener.PlayOnEnable.None;
            _backgroundTweener.Stop();
            _backgroundTweener.ResetTime();
        }
    }

    private void SetDisplayedValue(int value)
    {
        if (_valueText != null)
            _valueText.text = Mathf.Max(0, value).ToString();
    }

    private async Task CountdownAsync(int from, int to, CancellationToken ct)
    {
        int current = from;
        int step = to <= current ? -1 : 1;
        if (current == to) return;

        while (current != to && !ct.IsCancellationRequested)
        {
            current += step;
            SetDisplayedValue(current);
            await Task.Delay(Mathf.RoundToInt(ValueStepSec * 1000f), ct);
        }
    }

    private async Task<float> PlayDestructionAsync(CancellationToken ct)
    {
        if (_valueRoot != null) _valueRoot.SetActive(false);
        if (_labelRoot != null) _labelRoot.SetActive(false);
        if (_destructionRoot != null) _destructionRoot.SetActive(true);

        SoundEffectPlayer.I?.Play(BreakSePath);

        if (_backgroundEffect != null)
            _backgroundEffect.transitionRate = 0f;

        float duration = 1f;
        if (_backgroundTweener != null)
        {
            _backgroundTweener.wrapMode = UIEffectTweener.WrapMode.Once;
            _backgroundTweener.playOnEnable = UIEffectTweener.PlayOnEnable.None;
            _backgroundTweener.PlayForward(true);
            duration = _backgroundTweener.totalTime;

            while (_backgroundTweener.isTweening && !ct.IsCancellationRequested)
                await Task.Yield();
        }
        else
        {
            await Task.Delay(Mathf.RoundToInt(duration * 1000f), ct);
        }

        await Task.Delay(Mathf.RoundToInt((PostBreakTweenHoldSec + BreakExtraHoldSec) * 1000f), ct);
        return duration + PostBreakTweenHoldSec + BreakExtraHoldSec;
    }
}
