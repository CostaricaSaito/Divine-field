using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

public class BattleProcessor : MonoBehaviour
{
    //========================
    // シングルトン
    //========================

    public static BattleProcessor I; // ← シングルトンインスタンス
    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
    }

    //========================
    // References (set by Initialize)
    //========================

    [Header("バトルステータス")]
    public PlayerStatus playerStatus;
    public PlayerStatus enemyStatus;

    [Header("効果音")]
    public AudioSource audioSource;
    public AudioClip damageSE;

    private CardDealer cardDealer;　 // UI演出用（敵カードの一時表示など）
    private BattleManager battleManager;

    // 攻撃シーケンス完了通知（BattleManagerが購読する）
    public event System.Action OnAttackSequenceCompleted;

    // 初期化処理（ステータスや参照を受け取る）
    public void Initialize(
        PlayerStatus playerStatus,
        PlayerStatus enemyStatus,
        BattleStatusUI statusUI, // 未使用：互換性保持のため受け取りだけ
        BattleManager battleManager,
        CardDealer cardDealer,
        Canvas uiCanvas = null) // 未使用：UIはBattleUIManagerで管理
    {
        this.playerStatus = playerStatus;
        this.enemyStatus = enemyStatus;
        this.battleManager = battleManager;
        this.cardDealer = cardDealer;
    }


    //====================================================
    // Public: 共通ユースケース
    //====================================================

    // カード使用処理（手札から削除・演出・効果適用）ただし効果そのもの（攻撃/回復）は各Resolve系で行う。
    public void UseCard(CardData card, List<CardData> hand)
    {
        if (card == null || hand == null || !hand.Contains(card))
        {
            Debug.LogWarning("[BattleProcessor] カードが無効、または手札に存在しません");
            return;
        }

        Debug.Log($"[BattleProcessor]カード使用: {card.cardName}");
        DestroyCardUI(card);
        hand.Remove(card);

        hand.Remove(card);

    }


    // 即時効果（防御を伴わない回復/自己バフなど行為）を解決する。呼び出し側で UseCard を済ませてから呼ぶ想定。
    public async Task ResolveImmediateEffectAsync(CardData card, PlayerStatus user, PlayerStatus opponent)
    {
        if (card == null || user == null) return;

        // 例：回復系（CardDataのフラグに従う）
        if (card.cardType == CardType.Recovery || card.isRecovery)
        {
            int amt = Mathf.Max(0, card.recoveryAmount);

            if (card.healsHP && amt > 0)
            {
                user.currentHP = Mathf.Min(user.currentHP + amt, user.maxHP);
                // TODO: HP回復ポップアップやSE（任意）
            }
            if (card.healsMP && amt > 0)
            {
                user.currentMP = Mathf.Min(user.currentMP + amt, user.maxMP);
                // TODO: MP回復ポップアップやSE（任意）
            }
            if (card.healsGP && amt > 0)
            {
                user.currentGP = Mathf.Min(user.currentGP + amt, user.maxGP);
                // TODO: GP回復ポップアップやSE（任意）
            }
        }

        // ここで軽い演出の待ち時間を入れたい場合
        await Task.Yield(); // 実質ノーウェイト（将来アニメを入れるなら Task.Delay に差し替え）
    }


    // カード効果の適用（現在は攻撃のみ処理）
    private void ApplyCardEffect(CardData card)
    {
        switch (card.cardType)
        {
            case CardType.Attack:
                {
                    if (enemyStatus == null || enemyStatus.IsDead()) return;

                    int roll = Random.Range(0, 100);
                    if (roll >= card.hitRate)
                    {
                        Debug.Log($"[Battle] MISS {card.cardName}（命中率 {card.hitRate}%, Roll: {roll}）");
                        return;
                    }

                    break;
                }
        }
    }


    /// <summary>
    /// 攻撃 vs 防御 を解決（命中→軽減→ダメージ→状態異常→UI）。
    /// defenderHand には“防御側の手札”を渡す（防御カードの消費/UI破棄に使用）。
    /// </summary>
    public async Task ResolveCombatAsync(
        CardData attackCard,
        CardData defenseCard,
        PlayerStatus attacker,
        PlayerStatus defender,
        List<CardData> defenderHand)
    {
        if (attackCard == null || attacker == null || defender == null)
        {
            Debug.LogWarning("[BattleProcessor] ResolveCombatAsync: 引数が不足/無効");
            return;
        }

        // --- 命中判定（0-99のRollで、roll >= 命中率ならミス）---
        int roll = Random.Range(0, 100);
        if (roll >= Mathf.Clamp(attackCard.hitRate, 0, 100))
        {
            Debug.Log($"[Battle] MISS {attackCard.cardName}（命中率 {attackCard.hitRate}%, Roll: {roll}）");
            BattleUIManager.I?.ShowMissPopup(defender);
            BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
            await Task.Delay(500);
            OnAttackSequenceCompleted?.Invoke();
            return;
        }

        // --- 防御カードの軽減値 ---
        int def = (defenseCard != null) ? Mathf.Max(0, defenseCard.defensePower) : 0;

        // 防御カードを消費（防御側の手札から）
        if (defenseCard != null && defenderHand != null && defenderHand.Contains(defenseCard))
        {
            DestroyCardUI(defenseCard);
            defenderHand.Remove(defenseCard);
        }

        // --- ダメージ計算 ---
        int raw = Mathf.Max(0, attackCard.attackPower);
        int final = Mathf.Max(0, raw - def);

        // --- ダメージ適用 ---
        defender.TakeDamage(final);
        PlayDamageSE();
        BattleUIManager.I?.ShowDamagePopup(final, defender);

        // --- 状態異常（攻撃カード側の設定に応じて）---
        TryApplyStatusEffect(attackCard, defender);

        // --- UI更新・完了通知 ---
        BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
        OnAttackSequenceCompleted?.Invoke();

        await Task.Delay(800); // 余韻
    }
           

    //====================================================
    // Private helpers
    //====================================================

    private void DestroyCardUI(CardData card)
    {
        if (card?.cardUI != null)
        {
            Destroy(card.cardUI.gameObject);
            card.cardUI = null;
        }
    }

    private void PlayDamageSE()
    {
        if (audioSource != null && damageSE != null)
            audioSource.PlayOneShot(damageSE);
    }

    // カードに設定された確率で状態異常を付与する処理
    private void TryApplyStatusEffect(CardData card, PlayerStatus defender)
    {
        if (card == null || defender == null) return;
        if (!card.canApplyStatusEffect || card.statusEffectChance <= 0) return;

        int roll = Random.Range(0, 100);
        if (roll < card.statusEffectChance && enemyStatus != null)
        {
            enemyStatus.AddStatusEffect(StatusEffectType.Weaken);
            Debug.Log($"[BattleProcessor] 状態異常付与成功（{card.statusEffectChance}% / Roll:{roll}）");
        }
    }

}
