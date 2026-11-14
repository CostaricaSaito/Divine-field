using System;
using UnityEngine;

/// <summary>
/// 新規追加 SE の参照先（<see cref="AudioClip"/> と個別音量）。<c>Resources/SoundEffectCatalog</c> でロード。
/// </summary>
[CreateAssetMenu(fileName = "SoundEffectCatalog", menuName = "DivineField/Sound Effect Catalog")]
public sealed class SoundEffectCatalogSO : ScriptableObject
{
    public const string DefaultResourcesPath = "SoundEffectCatalog";

    [Serializable]
    public struct Entry
    {
        public AudioClip clip;
        [Min(0f)] public float volumeScale;
    }

    [Header("Prayer")]
    [SerializeField] private Entry prayer;

    [Header("Yurusu (defense with no cards)")]
    [SerializeField] private Entry yurusu;

    [Header("Paradise disease")]
    [SerializeField] private Entry paradiseEcstasy;
    [SerializeField] private Entry paradiseHeavenState;

    [Header("Battle damage (companion to damage popup)")]
    [SerializeField] private Entry battleDamageLow;
    [SerializeField] private Entry battleDamageMid;
    [SerializeField] private Entry battleDamageHigh;

    [Header("Battle notification banner")]
    [SerializeField] private Entry battleNotification;

    private static SoundEffectCatalogSO _cached;

    public static SoundEffectCatalogSO Get()
    {
        if (_cached != null) return _cached;
        _cached = Resources.Load<SoundEffectCatalogSO>(DefaultResourcesPath);
        if (_cached == null)
            Debug.LogWarning($"[SoundEffectCatalogSO] Resources/{DefaultResourcesPath} が見つかりません。");
        return _cached;
    }

    public void PlayPrayer() => PlayEntry(prayer, nameof(prayer));
    public void PlayYurusu() => PlayEntry(yurusu, nameof(yurusu));
    public void PlayParadiseEcstasy() => PlayEntry(paradiseEcstasy, nameof(paradiseEcstasy));
    public void PlayParadiseHeavenState() => PlayEntry(paradiseHeavenState, nameof(paradiseHeavenState));

    /// <summary>
    /// 命中後の同伴 SE。0 は再生しない（「ピコッ」は <see cref="BattleProcessor"/> 側）。
    /// <see cref="DamagePopupSfx.LowDamageMax"/> 以下 / 6〜(<see cref="DamagePopupSfx.HighDamageMin"/>-1) / 以上で段階切替。
    /// </summary>
    public void PlayBattleDamage(int finalDamage)
    {
        if (finalDamage <= 0) return;

        if (finalDamage <= DamagePopupSfx.LowDamageMax)
            PlayEntry(battleDamageLow, nameof(battleDamageLow));
        else if (finalDamage >= DamagePopupSfx.HighDamageMin)
            PlayEntry(battleDamageHigh, nameof(battleDamageHigh));
        else
            PlayEntry(battleDamageMid, nameof(battleDamageMid));
    }

    /// <summary>CardDisplayPanel notification banner SE (catalog default unless overridden).</summary>
    public void PlayBattleNotification(AudioClip overrideClip = null, float overrideVolumeScale = 0f)
    {
        if (overrideClip != null)
        {
            float scale = overrideVolumeScale > 0f ? overrideVolumeScale : 1f;
            SoundEffectPlayer.I?.Play(overrideClip, scale);
            return;
        }

        PlayEntry(battleNotification, nameof(battleNotification));
    }

    private static void PlayEntry(Entry entry, string fieldName)
    {
        if (entry.clip == null)
        {
            Debug.LogWarning($"[SoundEffectCatalogSO] {fieldName}.clip が未設定です。");
            return;
        }

        float scale = entry.volumeScale > 0f ? entry.volumeScale : 1f;
        SoundEffectPlayer.I?.Play(entry.clip, scale);
    }
}
