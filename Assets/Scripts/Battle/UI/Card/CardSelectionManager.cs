using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// カード選択の管理を行うクラス
/// </summary>
public class CardSelectionManager : MonoBehaviour
{
    public static CardSelectionManager I;

    // 選択されたカードのリスト
    private readonly List<CardData> selectedCards = new List<CardData>();

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
    }

    /// <summary>
    /// カード選択を追加
    /// </summary>
    public bool AddCardSelection(CardData card)
    {
        if (card == null) return false;

        if (HandReloadController.I != null && HandReloadController.I.IsHandReloadUiBlocking)
            return false;

        if (BattleManager.I != null && BattleManager.I.IsSummonSkillPopupOpen)
            return false;

        // ===== 防御フェーズ：拘束中は防御カードを2枚目まで選べない =====
        if (BattleManager.I != null && BattleManager.I.IsPlayerDefenseInputActive() && IsDefenseCard(card))
        {
            PlayerStatus defender = BattleManager.I.GetPlayerStatus();
            if (defender != null && defender.HasRestraintEffect())
            {
                var already = GetSelectedDefenseCards();
                bool sameAsSelected = false;
                foreach (var a in already)
                {
                    if (a != null && a.GetInstanceID() == card.GetInstanceID())
                    {
                        sameAsSelected = true;
                        break;
                    }
                }
                if (already.Count >= 1 && !sameAsSelected)
                {
                    BattleUIManager.I?.ShowInfoPopupOnCardPanel("体が重い", new Color(0.22f, 0.24f, 0.38f));
                    return false;
                }
            }
        }

        // ===== 防御：物理／魔法反射は攻撃に応じて他カードと併選不可 =====
        if (BattleManager.I != null && BattleManager.I.IsPlayerDefenseInputActive() && IsDefenseCard(card))
        {
            var incoming = BattleManager.I.GetIncomingAttackSnapshotForDefenseUi();

            foreach (var sel in selectedCards)
            {
                if (sel == null) continue;
                if (!BlockingRules.RequiresDefenseNullifyExclusiveLock(sel, incoming)) continue;
                if (card.GetInstanceID() == sel.GetInstanceID()) continue;
                BattleUIManager.I?.ShowInfoPopupOnCardPanel("反射・無効化・打ち払いは他のカードと併用できません", new Color(0.85f, 0.25f, 0.2f));
                return false;
            }

            if (BlockingRules.RequiresDefenseNullifyExclusiveLock(card, incoming))
            {
                foreach (var sel in selectedCards)
                {
                    if (sel == null) continue;
                    if (BlockingRules.RequiresDefenseNullifyExclusiveLock(sel, incoming)) continue;
                    BattleUIManager.I?.ShowInfoPopupOnCardPanel("反射・無効化・打ち払いは他のカードと併用できません", new Color(0.85f, 0.25f, 0.2f));
                    return false;
                }
            }
        }

        // ===== 魔法カードの事前ガード =====
        if (card.cardType == CardType.Magic)
        {
            // 防具マジック等：プレイヤー攻撃手番の Attack では手札候補と同じく IsUsableInAttackPhase。MagicPanel クリック専用の抜け穴を塞ぐ
            // 反射連鎖／打ち払い再防御は State が攻撃系のままのため必ず除外
            if (BattleManager.I != null
                && BattleManager.I.CurrentState == GameState.AttackPhase
                && !BattleManager.I.IsPlayerDefenseInputActive()
                && !BattleManager.I.IsPostDeathSequenceActive
                && BattleManager.I.CurrentTurnOwner == PlayerType.Player
                && !CardRules.IsRecoveryCard(card)
                && !CardRules.IsUsableInAttackPhase(card))
            {
                return false;
            }

            PlayerType magicPoolOwner = BattleManager.I != null
                ? BattleManager.I.CurrentTurnOwner
                : PlayerType.Player;
            PlayerStatus magicUserStatus = magicPoolOwner == PlayerType.Player
                ? BattleManager.I?.GetPlayerStatus()
                : BattleManager.I?.GetEnemyStatus();
            if (magicUserStatus != null && magicUserStatus.IsMagicUseForbidden())
            {
                BattleUIManager.I?.ShowInfoPopupOnCardPanel("魔法が使用できません", new Color(0.95f, 0.22f, 0.2f));
                return false;
            }

            // MP 合算は使用ボタン側で判定（眼精疲労の倍率・複数魔法対応）。防御フェーズはここでも単体不足を弾く。
            bool defenseMagicContext = BattleManager.I != null
                && BattleManager.I.IsPlayerDefenseInputActive()
                && IsDefenseCard(card);
            if (defenseMagicContext && magicUserStatus != null
                && !BlockingRules.CanAffordMagicDefenseMp(card, magicUserStatus))
            {
                BattleUIManager.I?.ShowInfoPopupOnCardPanel("MPが足りない", new Color(0.95f, 0.22f, 0.2f));
                return false;
            }

            if (defenseMagicContext && BlockingRules.IsPhysicalBlockingCard(card))
            {
                var incomingBlock = BattleManager.I.GetIncomingAttackSnapshotForDefenseUi();
                if (incomingBlock == null
                    || !BlockingRules.CanUsePhysicalBlockingAgainstAttack(card, incomingBlock))
                {
                    BattleUIManager.I?.ShowInfoPopupOnCardPanel(
                        "無属性の物理攻撃にのみ使えます", new Color(0.85f, 0.25f, 0.2f));
                    return false;
                }
            }

            // MagicPool 容量チェックは「手札からプールへ載せる」場合のみ。MagicPanel 表示中のカードは既にプール内。
            // プールは手番ごとに分離（プレイヤー満杯でも敵の空きは別）。
            bool onOwnMagicPanel = false;
            if (BattleUIManager.I != null && magicPoolOwner == PlayerType.Player)
                onOwnMagicPanel = BattleUIManager.I.IsPlayerMagicCardUiOnMagicPanel(card);
            else if (BattleUIManager.I != null && magicPoolOwner == PlayerType.Enemy)
                onOwnMagicPanel = BattleUIManager.I.IsEnemyMagicCardUiOnMagicPanel(card);

            bool capacityApplies = card.cardUI == null || !onOwnMagicPanel;
            if (capacityApplies && MagicPoolManager.I != null && !MagicPoolManager.I.CanAddToPool(card, magicPoolOwner))
            {
                BattleUIManager.I?.ShowInfoPopupOnCardPanel("魔法容量不足！", new Color(1f, 0.5f, 0f));
                return false;
            }
        }

        // ===== Standalone（大魔法・顕現等）／グランド枠：他と併用不可。ポップアップは出さず既存をクリアして新カードへ差し替え =====
        if (BattleManager.I != null
            && BattleManager.I.CurrentState == GameState.AttackPhase
            && BattleManager.I.CurrentTurnOwner == PlayerType.Player
            && card.attackPhaseUseRule == AttackPhaseUseRule.Standalone
            && selectedCards.Count > 0)
        {
            ClearAllWithUI();
        }

        if (BattleManager.I != null
            && BattleManager.I.CurrentState == GameState.AttackPhase
            && BattleManager.I.CurrentTurnOwner == PlayerType.Player
            && selectedCards.Count > 0
            && GrandMagicRules.ContainsGrandMagicStyleAttack(selectedCards)
            && !GrandMagicRules.IsGrandMagicStyleAttackCard(card))
        {
            ClearAllWithUI();
        }

        // 詠唱中（PlayerStatus.IsCastingArchMagic）は攻撃カード選択自体を受け付けない（保険）
        if (BattleManager.I != null
            && BattleManager.I.CurrentTurnOwner == PlayerType.Player
            && BattleManager.I.GetPlayerStatus() != null
            && BattleManager.I.GetPlayerStatus().IsCastingArchMagic)
        {
            return false;
        }

        // ===== 攻撃：魔法（Primary）＋ 物理（Attack + Flexible）は同時不可。Primary同士同様、既存を消して新カードへ差し替え =====
        if (BattleManager.I != null
            && BattleManager.I.CurrentState == GameState.AttackPhase
            && BattleManager.I.CurrentTurnOwner == PlayerType.Player
            && !BattleManager.I.IsReflectionChainDefensePending()
            && AttackComboSelectionRules.ConflictsMagicPrimaryWithPhysicalAttackFlexible(selectedCards, card))
        {
            ClearAllWithUI();
        }

        // SelectionRole 等に基づき、併用不可なら既存選択をクリア
        CheckCardConflicts(card);

        // ===== 攻撃フェーズ：組み合わせ専用（先に攻撃カード1枚以上） =====
        if (BattleManager.I != null
            && BattleManager.I.CurrentState == GameState.AttackPhase
            && BattleManager.I.CurrentTurnOwner == PlayerType.Player
            && !BattleManager.I.IsReflectionChainDefensePending()
            && card.attackPhaseUseRule == AttackPhaseUseRule.AddOn)
        {
            var attackSoFar = GetSelectedAttackCards();
            if (!AttackComboSelectionRules.CanPickAttackCardNow(card, attackSoFar))
            {
                BattleUIManager.I?.ShowInfoPopupOnCardPanel("先に攻撃カードを選んでください", new Color(0.85f, 0.35f, 0.15f));
                return false;
            }
        }

        // 同じカードが既に選択されている場合は追加しない（参照が別でも同じ SO なら弾く）
        int pickId = card.GetInstanceID();
        foreach (var c in selectedCards)
        {
            if (c != null && c.GetInstanceID() == pickId)
                return false;
        }

        // カード選択を追加
        selectedCards.Add(card);
        return true;
    }

    /// <summary>
    /// カード選択をキャンセル。AddOn 等の前提が消えたカードも連鎖で外す。
    /// </summary>
    /// <returns>外したカード（最初に指定したカード＋連鎖分）。</returns>
    public List<CardData> CancelCardSelection(CardData card)
    {
        var removed = new List<CardData>();
        if (card == null) return removed;

        int id = card.GetInstanceID();
        if (selectedCards.RemoveAll(c => c != null && c.GetInstanceID() == id) > 0)
            removed.Add(card);

        removed.AddRange(PruneDependentAttackSelectionsAfterCancel());
        return removed;
    }

    private List<CardData> PruneDependentAttackSelectionsAfterCancel()
    {
        if (BattleManager.I == null || BattleManager.I.CurrentState != GameState.AttackPhase)
            return new List<CardData>();

        return AttackComboSelectionRules.PruneInvalidAttackSelections(selectedCards, IsAttackCard);
    }

    /// <summary>
    /// 全選択をクリア
    /// </summary>
    public void ClearAllSelections()
    {
        selectedCards.Clear();
    }

    /// <summary>
    /// 選択されたカードのリストを取得
    /// </summary>
    public List<CardData> GetSelectedCards()
    {
        return selectedCards;
    }

    /// <summary>
    /// 選択された攻撃カードのリストを取得
    /// </summary>
    public List<CardData> GetSelectedAttackCards()
    {
        var attackCards = new List<CardData>();
        foreach (var card in selectedCards)
        {
            if (IsAttackCard(card))
            {
                attackCards.Add(card);
            }
        }
        return attackCards;
    }

    /// <summary>
    /// 選択された防御カードのリストを取得
    /// </summary>
    public List<CardData> GetSelectedDefenseCards()
    {
        var defenseCards = new List<CardData>();
        foreach (var card in selectedCards)
        {
            if (IsDefenseCard(card))
            {
                defenseCards.Add(card);
            }
        }
        return defenseCards;
    }

    /// <summary>
    /// 選択されたカード数
    /// </summary>
    public int SelectedCardCount => selectedCards.Count;

    /// <summary>
    /// 選択されたカードがないかチェック
    /// </summary>
    public bool HasNoSelectedCards()
    {
        return selectedCards.Count == 0;
    }

    /// <summary>
    /// 指定されたカードが選択されているかチェック
    /// </summary>
    public bool IsCardSelected(CardData card)
    {
        if (card == null) return false;
        int id = card.GetInstanceID();
        foreach (var c in selectedCards)
        {
            if (c != null && c.GetInstanceID() == id)
                return true;
        }
        return false;
    }

    // ---- SelectionRole ベースの競合チェック ----

    private SelectionRole GetRoleForCurrentPhase(CardData card)
    {
        if (BattleManager.I != null && BattleManager.I.IsPlayerDefenseInputActive())
            return card.defensePhaseRole;

        return card.attackPhaseRole;
    }

    private bool HasRoleSelected(SelectionRole role)
    {
        foreach (var c in selectedCards)
        {
            if (GetRoleForCurrentPhase(c) == role) return true;
        }
        return false;
    }

    private bool HasMagicCards()
    {
        return selectedCards.Exists(c => c.cardType == CardType.Magic);
    }

    private void ClearAllWithUI()
    {
        ClearAllSelections();
        // TurnEnd 介入中は敵パネルに出した介入攻撃カードを残し、プレイヤー側の表示だけ消す
        if (BattleManager.I != null && BattleManager.I.IsInterventionDefenseWaitActive())
            BattleUIManager.I?.HidePlayerCardDetails();
        else
            BattleUIManager.I?.HideAllCardDetails();
    }

    /// <summary>
    /// カード競合チェック（SelectionRole ベース）。
    /// <see cref="SelectionRole.None"/> のときは <c>switch</c> に入らず既選択をクリアしない（複数枚のまま追加可能）。
    /// </summary>
    private void CheckCardConflicts(CardData newCard)
    {
        if (newCard != null && IsCardSelected(newCard)) return;

        SelectionRole role = GetRoleForCurrentPhase(newCard);

        switch (role)
        {
            case SelectionRole.None:
                break;

            case SelectionRole.Standalone:
            case SelectionRole.Primary:
                if (selectedCards.Count > 0)
                    ClearAllWithUI();
                break;

            case SelectionRole.Addable:
                if (HasRoleSelected(SelectionRole.Standalone))
                {
                    ClearAllWithUI();
                }
                else if (newCard.cardType == CardType.Magic && HasMagicCards())
                {
                    ClearAllWithUI();
                }
                break;

            case SelectionRole.Free:
                break;
        }
    }

    private bool IsAttackCard(CardData card)
    {
        if (card == null) return false;
        if (card.cardType == CardType.Magic && !CardRules.IsRecoveryCard(card))
            return CardRules.IsUsableInAttackPhase(card);
        if (card.cardType == CardType.ArchMagic) return true;
        if (card.cardType == CardType.Special) return true;
        if (card.cardType == CardType.Ultimate) return true;
        return card.cardType == CardType.Attack || CardRules.IsRecoveryCard(card);
    }

    private bool IsDefenseCard(CardData card)
    {
        if (card == null || !IsDefenseSelectionContext()) return false;
        if (IsLocalPlayerChantingArchMagic()) return false;
        return CardRules.IsUsableInDefensePhase(card);
    }

    private static bool IsLocalPlayerChantingArchMagic()
    {
        var bm = BattleManager.I;
        return bm != null
            && bm.GetPlayerStatus() != null
            && bm.GetPlayerStatus().IsCastingArchMagic;
    }

    private static bool IsDefenseSelectionContext()
    {
        return BattleManager.I != null && BattleManager.I.IsPlayerDefenseInputActive();
    }
}