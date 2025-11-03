using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using System.Linq;

/// <summary>
/// バトルの戦闘処理を担当するクラス
/// 
/// 【役割】
/// - カードの使用処理（手札からの削除、UI破棄）
/// - 戦闘解決（ダメージ計算、命中判定）
/// - 即時効果の処理（回復、特殊効果等）
/// - 戦闘結果の通知
/// 
/// 【責任範囲】
/// - カード使用時の手札・UI管理
/// - 攻撃・防御のダメージ計算
/// - 命中率の判定
/// - 戦闘アニメーション・効果音の制御
/// 
/// 【他のクラスとの関係】
/// - BattleManager: 戦闘処理の実行要求
/// - BattleUIManager: 戦闘結果の表示
/// - CardDealer: カードUIの管理
/// 
/// 【注意事項】
/// - 状態管理は行わない（BattleStateMachineに委譲）
/// - UI表示は指示のみ（BattleUIManagerに委譲）
/// - ビジネスロジックの判定は行わない
/// </summary>
public class BattleProcessor : MonoBehaviour
{
    //========================
    // シングルトン管理
    //========================

    public static BattleProcessor I; // シングルトンインスタンス
    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
    }

    //========================
    // 依存関係（Initializeで設定）
    //========================

    [Header("ステータス参照")]
    public PlayerStatus playerStatus;
    public PlayerStatus enemyStatus;

    [Header("音響")]
    public AudioSource audioSource;
    public AudioClip damageSE;

    private CardDealer cardDealer; // UI管理用（カードの一時表示等）

    //========================
    // 初期化
    //========================

    /// <summary>
    /// 初期化処理
    /// 
    /// 【処理内容】
    /// 各システムへの参照を設定し、戦闘処理の準備を行う
    /// </summary>
    /// <param name="playerStatus">プレイヤーのステータス</param>
    /// <param name="enemyStatus">敵のステータス</param>
    /// <param name="statusUI">ステータスUI</param>
    /// <param name="cardDealer">カードディーラー</param>
    public void Initialize(
        PlayerStatus playerStatus,
        PlayerStatus enemyStatus,
        BattleStatusUI statusUI,
        CardDealer cardDealer)
    {
        this.playerStatus = playerStatus;
        this.enemyStatus = enemyStatus;
        this.cardDealer = cardDealer;
    }

    //========================
    // カード使用処理
    //========================

    /// <summary>
    /// カードを使用する（裏向きにする）
    /// 
    /// 【処理内容】
    /// 1. カードを裏向きにする
    /// 2. カードUIを無効化
    /// 3. 使用ログの出力
    /// 
    /// 【使用例】
    /// battleProcessor.UseCard(attackCard, playerHand);
    /// </summary>
    /// <param name="card">使用するカード</param>
    /// <param name="hand">手札リスト</param>
    public void UseCard(CardData card, List<CardData> hand)
    {
        if (card == null || hand == null)
        {
            Debug.LogWarning("[BattleProcessor] カードまたは手札がnullです");
            return;
        }

        // カードを裏向きにする
        if (card.cardUI != null)
        {
            card.cardUI.Setup(null, cardDealer?.CardBackSprite);
            card.cardUI.button.interactable = false;
        }

        Debug.Log($"[BattleProcessor] カード使用: {card.cardName}");
    }

    //========================
    // 即時効果処理
    //========================

    /// <summary>
    /// 即時効果を解決する
    /// 
    /// 【処理内容】
    /// 1. 回復効果の適用
    /// 2. 状態異常の適用
    /// 3. 特殊効果の処理
    /// 4. ステータス更新
    /// 
    /// 【使用例】
    /// await battleProcessor.ResolveImmediateEffectAsync(healCard, playerStatus, enemyStatus);
    /// </summary>
    /// <param name="card">使用したカード</param>
    /// <param name="user">使用者</param>
    /// <param name="target">対象</param>
    /// <returns>処理完了まで待機</returns>
    public Task ResolveImmediateEffectAsync(CardData card, PlayerStatus user, PlayerStatus target)
    {
        if (card == null || user == null)
        {
            Debug.LogWarning("[BattleProcessor] カードまたは使用者がnullです");
            return Task.CompletedTask;
        }

        Debug.Log($"[BattleProcessor] 即時効果解決開始: {card.cardName}");

        // 回復効果の適用
        if (card.recoveryAmount > 0)
        {
            ApplyRecovery(card, user);
        }

        // 状態異常の適用（将来的に実装）
        if (card.canApplyStatusEffect && target != null)
        {
            // TODO: 状態異常処理を実装
            Debug.Log($"[BattleProcessor] 状態異常適用予定: {card.cardName}");
        }

        // 特殊効果の処理（将来的に拡張）
        ProcessSpecialEffects(card, user, target);

        // ステータス更新
        UpdateStatusDisplay();

        Debug.Log($"[BattleProcessor] 即時効果解決完了: {card.cardName}");
        return Task.CompletedTask;
    }

    //========================
    // 戦闘解決処理
    //========================

    /// <summary>
    /// 戦闘を解決する（複数カード対応）
    /// 
    /// 【処理内容】
    /// 1. 攻撃力・防御力の計算
    /// 2. 命中判定
    /// 3. ダメージ計算（状態異常考慮）
    /// 4. ダメージ適用
    /// 5. 戦闘結果の表示
    /// 
    /// 【使用例】
    /// await battleProcessor.ResolveCombatAsync(attackCards, defenseCard, attacker, defender, defenderHand);
    /// </summary>
    /// <param name="attackCards">攻撃カードリスト（複数選択対応）</param>
    /// <param name="defenseCard">防御カード</param>
    /// <param name="attacker">攻撃者</param>
    /// <param name="defender">防御者</param>
    /// <param name="defenderHand">防御者の手札</param>
    /// <returns>戦闘解決完了まで待機</returns>
    public async Task ResolveCombatAsync(List<CardData> attackCards, CardData defenseCard, PlayerStatus attacker, PlayerStatus defender, List<CardData> defenderHand)
    {
        if (attackCards == null || attackCards.Count == 0 || attacker == null || defender == null)
        {
            Debug.LogWarning("[BattleProcessor] 戦闘解決に必要なパラメータがnullです");
            return;
        }

        // 攻撃カード名をログ出力
        string attackCardNames = string.Join(" + ", attackCards.Select(c => c.cardName));
        Debug.Log($"[BattleProcessor] ===== 戦闘解決開始 =====");
        Debug.Log($"[BattleProcessor] 攻撃: {attackCardNames}");
        Debug.Log($"[BattleProcessor] 防御: {defenseCard?.cardName ?? "なし"}");
        Debug.Log($"[BattleProcessor] 攻撃者: {attacker.DisplayName} vs 防御者: {defender.DisplayName}");

        // 攻撃力・防御力の計算
        int attackPower = CalculateTotalAttackPower(attackCards, attacker);
        int defensePower = CalculateTotalDefensePower(defenseCard, defender);
        
        Debug.Log($"[BattleProcessor] 計算結果 - 攻撃力: {attackPower}, 防御力: {defensePower}");

        // 命中判定（最初の攻撃カードを使用）
        bool hit = CheckHit(attackCards[0], defenseCard);
        if (!hit)
        {
            Debug.Log($"[BattleProcessor] 攻撃が外れました: {attackCardNames}");
            PlayDamageSE();
            // ミスポップアップを表示
            BattleUIManager.I?.ShowMissPopup(defender);
            return;
        }

        // ダメージ計算
        int baseDamage = attackPower - defensePower;
        int finalDamage = baseDamage; // 状態異常による修正は将来的に実装
        finalDamage = Mathf.Max(0, finalDamage); // 負のダメージは0に

        Debug.Log($"[BattleProcessor] ===== ダメージ計算 =====");
        Debug.Log($"[BattleProcessor] 基本ダメージ: {attackPower} - {defensePower} = {baseDamage}");
        Debug.Log($"[BattleProcessor] 最終ダメージ: {finalDamage}");

        // ⑤ダメージポップアップ前の0.5秒インターバル
        await Task.Delay(500);
        Debug.Log("[BattleProcessor] ダメージポップアップ前、0.5秒待機");

        // ダメージ適用
        if (finalDamage > 0)
        {
            ApplyDamage(defender, finalDamage);
            Debug.Log($"[BattleProcessor] ダメージ適用完了: {finalDamage} → {defender.DisplayName}");
            // ダメージポップアップを表示
            BattleUIManager.I?.ShowDamagePopup(finalDamage, defender);
        }
        else
        {
            Debug.Log($"[BattleProcessor] ダメージ0: 攻撃力{attackPower} - 防御力{defensePower} = {baseDamage}");
            // ダメージ0の場合もポップアップを表示
            BattleUIManager.I?.ShowDamagePopup(0, defender);
        }

        // 戦闘結果の表示
        PlayDamageSE();
        // HP/MP/GPと手札枚数を更新（手札枚数は変わらないため、常に現在の値を参照）
        UpdateStatusDisplay();

        // 戦闘終了判定
        if (IsDead(attacker) || IsDead(defender))
        {
            Debug.Log($"[BattleProcessor] 戦闘終了: どちらかが死亡");
        }

        // ダメージポップアップ表示後のインターバル（相手の防御カード選択開始まで）
        await Task.Delay(500);
        Debug.Log("[BattleProcessor] ダメージポップアップ表示後、0.5秒待機");

        Debug.Log($"[BattleProcessor] 戦闘解決完了");
    }

    //========================
    // 内部処理メソッド
    //========================

    /// <summary>
    /// 複数カードの合計攻撃力を計算する
    /// </summary>
    private int CalculateTotalAttackPower(List<CardData> attackCards, PlayerStatus attacker)
    {
        if (attackCards == null || attackCards.Count == 0 || attacker == null) 
        {
            Debug.LogWarning("[BattleProcessor] 攻撃力計算: 無効なパラメータ");
            return 0;
        }
        
        Debug.Log($"[BattleProcessor] ===== 攻撃力計算開始 =====");
        Debug.Log($"[BattleProcessor] 攻撃者: {attacker.DisplayName}");
        Debug.Log($"[BattleProcessor] 攻撃カード数: {attackCards.Count}");
        
        int totalAttackPower = 0;
        for (int i = 0; i < attackCards.Count; i++)
        {
            var card = attackCards[i];
            if (card != null)
            {
                totalAttackPower += card.attackPower;
                Debug.Log($"[BattleProcessor] [{i+1}] {card.cardName}: ATK {card.attackPower} (累計: {totalAttackPower})");
            }
            else
            {
                Debug.LogWarning($"[BattleProcessor] [{i+1}] カードがnullです");
            }
        }
        
        Debug.Log($"[BattleProcessor] ===== 最終攻撃力: {totalAttackPower} =====");
        return totalAttackPower;
    }
    
    /// <summary>
    /// 攻撃力を計算する（単一カード用）
    /// </summary>
    private int CalculateAttackPower(CardData card, PlayerStatus attacker)
    {
        if (card == null || attacker == null) return 0;
        return card.attackPower;
    }

    /// <summary>
    /// 防御力を計算する（複数カード対応）
    /// </summary>
    private int CalculateTotalDefensePower(CardData card, PlayerStatus defender)
    {
        if (card == null || defender == null) return 0;
        return card.defensePower;
    }

    /// <summary>
    /// 防御力を計算する（複数カード対応）
    /// </summary>
    private int CalculateTotalDefensePower(List<CardData> cards, PlayerStatus defender)
    {
        if (cards == null || cards.Count == 0 || defender == null) return 0;
        
        int totalDefense = 0;
        foreach (var card in cards)
        {
            if (card != null)
            {
                totalDefense += card.defensePower;
            }
        }
        
        Debug.Log($"[BattleProcessor] ===== 防御力計算開始 =====");
        Debug.Log($"[BattleProcessor] 防御者: {defender.DisplayName}");
        Debug.Log($"[BattleProcessor] 防御カード数: {cards.Count}");
        
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            if (card != null)
            {
                Debug.Log($"[BattleProcessor] [{i + 1}] {card.cardName}: DEF {card.defensePower} (累計: {totalDefense})");
            }
        }
        
        Debug.Log($"[BattleProcessor] ===== 最終防御力: {totalDefense} =====");
        return totalDefense;
    }

    /// <summary>
    /// 命中判定を行う
    /// </summary>
    private bool CheckHit(CardData attackCard, CardData defenseCard)
    {
        if (attackCard == null) return false;

        int hitRate = attackCard.hitRate;
        
        // 現在のカードはすべて命中率100%のため、防御カードによる命中率減少は無効化
        // if (defenseCard != null)
        // {
        //     // 防御カードがある場合は命中率を下げる（将来的に拡張）
        //     hitRate = Mathf.Max(0, hitRate - 10);
        // }

        int roll = Random.Range(0, 100);
        bool result = roll < hitRate;
        
        Debug.Log($"[BattleProcessor] 命中判定: 命中率{hitRate}%, 乱数{roll}, 結果{(result ? "命中" : "ミス")}");
        return result;
    }

    /// <summary>
    /// ダメージを適用する
    /// </summary>
    private void ApplyDamage(PlayerStatus target, int damage)
    {
        if (target == null) return;

        target.currentHP = Mathf.Max(0, target.currentHP - damage);
        Debug.Log($"[BattleProcessor] ダメージ適用: {damage} → {target.DisplayName} (HP: {target.currentHP})");
    }

    /// <summary>
    /// 回復を適用する
    /// </summary>
    private void ApplyRecovery(CardData card, PlayerStatus target)
    {
        if (card == null || target == null) return;

        int amount = card.recoveryAmount;

        // HP回復
        if (card.healsHP)
        {
            int oldHP = target.currentHP;
            target.currentHP = Mathf.Min(target.maxHP, target.currentHP + amount);
            int actualRecovery = target.currentHP - oldHP;
            
            if (actualRecovery > 0)
            {
                Debug.Log($"[BattleProcessor] HP回復適用: {actualRecovery} → {target.DisplayName} (HP: {target.currentHP})");
                BattleUIManager.I?.ShowHealPopup(actualRecovery, "HP", target);
                // HP回復効果音を再生
                SoundEffectPlayer.I?.Play("Assets/SE/power09(DFHP回復).wav");
            }
        }

        // MP回復
        if (card.healsMP)
        {
            int oldMP = target.currentMP;
            target.currentMP = Mathf.Min(target.maxMP, target.currentMP + amount);
            int actualRecovery = target.currentMP - oldMP;
            
            if (actualRecovery > 0)
            {
                Debug.Log($"[BattleProcessor] MP回復適用: {actualRecovery} → {target.DisplayName} (MP: {target.currentMP})");
                BattleUIManager.I?.ShowHealPopup(actualRecovery, "MP", target);
                // MP回復効果音を再生
                SoundEffectPlayer.I?.Play("Assets/SE/決定ボタンを押す25.mp3");
            }
        }

        // GP回復
        if (card.healsGP)
        {
            int oldGP = target.currentGP;
            target.currentGP = Mathf.Min(target.maxGP, target.currentGP + amount);
            int actualRecovery = target.currentGP - oldGP;
            
            if (actualRecovery > 0)
            {
                Debug.Log($"[BattleProcessor] GP回復適用: {actualRecovery} → {target.DisplayName} (GP: {target.currentGP})");
                BattleUIManager.I?.ShowHealPopup(actualRecovery, "GP", target);
                // GP回復効果音を再生
                SoundEffectPlayer.I?.Play("Assets/SE/レジスターで精算.mp3");
            }
        }
    }

    /// <summary>
    /// 特殊効果を処理する（将来的に拡張）
    /// </summary>
    private void ProcessSpecialEffects(CardData card, PlayerStatus user, PlayerStatus target)
    {
        // 将来的に特殊効果の処理をここに追加
    }

    /// <summary>
    /// カードUIを破棄する
    /// </summary>
    private void DestroyCardUI(CardData card)
    {
        if (card?.cardUI != null)
        {
            Destroy(card.cardUI.gameObject);
            card.cardUI = null;
        }
    }

    /// <summary>
    /// ステータス表示を更新する
    /// </summary>
    private void UpdateStatusDisplay()
    {
        BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
    }

    /// <summary>
    /// ダメージSEを再生する
    /// </summary>
    private void PlayDamageSE()
    {
        if (audioSource && damageSE)
        {
            audioSource.PlayOneShot(damageSE);
        }
    }

    /// <summary>
    /// 死亡判定
    /// </summary>
    private bool IsDead(PlayerStatus status)
    {
        return status != null && status.currentHP <= 0;
    }

    /// <summary>
    /// 戦闘を解決する（複数防御カード対応）
    /// </summary>
    /// <param name="attackCards">攻撃カードリスト</param>
    /// <param name="defenseCards">防御カードリスト</param>
    /// <param name="attacker">攻撃者</param>
    /// <param name="defender">防御者</param>
    /// <param name="defenderHand">防御者の手札</param>
    /// <returns>戦闘解決完了まで待機</returns>
    public async Task ResolveCombatAsync(List<CardData> attackCards, List<CardData> defenseCards, PlayerStatus attacker, PlayerStatus defender, List<CardData> defenderHand)
    {
        if (attackCards == null || attackCards.Count == 0 || attacker == null || defender == null)
        {
            Debug.LogWarning("[BattleProcessor] 戦闘解決に必要なパラメータがnullです");
            return;
        }

        // 攻撃カード名をログ出力
        string attackCardNames = string.Join(" + ", attackCards.Select(c => c.cardName));
        string defenseCardNames = defenseCards != null && defenseCards.Count > 0 ? string.Join(" + ", defenseCards.Select(c => c.cardName)) : "なし";
        Debug.Log($"[BattleProcessor] ===== 戦闘解決開始（複数防御カード対応） =====");
        Debug.Log($"[BattleProcessor] 攻撃: {attackCardNames}");
        Debug.Log($"[BattleProcessor] 防御: {defenseCardNames}");
        Debug.Log($"[BattleProcessor] 攻撃者: {attacker.DisplayName} vs 防御者: {defender.DisplayName}");

        // 攻撃力・防御力の計算
        int attackPower = CalculateTotalAttackPower(attackCards, attacker);
        int defensePower = CalculateTotalDefensePower(defenseCards, defender);
        
        Debug.Log($"[BattleProcessor] 計算結果 - 攻撃力: {attackPower}, 防御力: {defensePower}");

        // 命中判定（最初の攻撃カードを使用）
        bool hit = CheckHit(attackCards[0], defenseCards?.FirstOrDefault());
        if (!hit)
        {
            Debug.Log($"[BattleProcessor] 攻撃が外れました: {attackCardNames}");
            PlayDamageSE();
            // ミスポップアップを表示
            BattleUIManager.I?.ShowMissPopup(defender);
            return;
        }

        // ダメージ計算
        int baseDamage = attackPower - defensePower;
        int finalDamage = baseDamage; // 状態異常による修正は将来的に実装
        finalDamage = Mathf.Max(0, finalDamage); // 負のダメージは0に

        Debug.Log($"[BattleProcessor] ===== ダメージ計算 =====");
        Debug.Log($"[BattleProcessor] 基本ダメージ: {attackPower} - {defensePower} = {baseDamage}");
        Debug.Log($"[BattleProcessor] 最終ダメージ: {finalDamage}");

        // ⑤ダメージポップアップ前の0.5秒インターバル
        await Task.Delay(500);
        Debug.Log("[BattleProcessor] ダメージポップアップ前、0.5秒待機");

        // ダメージ適用
        if (finalDamage > 0)
        {
            ApplyDamage(defender, finalDamage);
            Debug.Log($"[BattleProcessor] ダメージ適用完了: {finalDamage} → {defender.DisplayName}");
            // ダメージポップアップを表示
            BattleUIManager.I?.ShowDamagePopup(finalDamage, defender);
        }
        else
        {
            Debug.Log($"[BattleProcessor] ダメージ0: 攻撃力{attackPower} - 防御力{defensePower} = {baseDamage}");
            // ダメージ0の場合もポップアップを表示
            BattleUIManager.I?.ShowDamagePopup(0, defender);
        }

        // 戦闘結果の表示
        PlayDamageSE();
        // HP/MP/GPと手札枚数を更新（手札枚数は変わらないため、常に現在の値を参照）
        UpdateStatusDisplay();

        // ダメージポップアップ表示後のインターバル（相手の防御カード選択開始まで）
        await Task.Delay(500);
        Debug.Log("[BattleProcessor] ダメージポップアップ表示後、0.5秒待機");

        Debug.Log($"[BattleProcessor] 戦闘解決完了");
        return;
    }
}