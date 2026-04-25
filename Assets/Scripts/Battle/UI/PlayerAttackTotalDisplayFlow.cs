using UnityEngine;

/// <summary>
/// プレイヤー攻撃の <b>TOTAL ATK 表示</b>を、攻撃力変動の流れに沿って扱うための単一窓口。
/// ここに書くのは <see cref="CardStatsDisplay"/> への委譲と、フロー仕様の一次ドキュメントのみ。
/// 戦闘ダメージの数式そのものは <see cref="BattleProcessor"/> および各 Rule（<see cref="GodrageRules"/> 等）が担当。
/// </summary>
/// <remarks>
/// <b>想定 UI フロー（一般化）</b>
/// <list type="number">
///   <item>攻撃カードを選択：TOTAL は主にそのカードの基礎 ATK 相当（単枚時）。他カード加算のルールは <see cref="CardStatsDisplay"/> 経由。</item>
///   <item>追加・倍化などのカードを追加選択：TOTAL は <b>期待最終</b>（加護前・一部演出前の理論値）に合わせる想定。</item>
///   <item>「決定」直後、カードを再掲出する前／中：一時的に <b>基礎分のみ</b>（例: 6）へ戻す段階がある。修飾の種類ごとに抑制フラグが違うため、
///   マジカルソード＋ゴッドレイジ専用の抑制は <see cref="CardStatsDisplay.SetPlayerMsGodComboCardRevealPhase"/> 等に閉じている。</item>
///   <item>攻撃力変動の <b>演出</b>：緑字・カウントアップ等は <see cref="CardStatsDisplay"/> の <c>Play*RampAsync</c> 系。</item>
///   <item>演出終了後、防御選択（Defense）へ。反射時の相手パネル TOTAL は反射用ラベル経路を参照。</item>
/// </list>
/// <b>ルール A（想定）／現状実装</b>：手札上の <b>選択順で効果解決、ゴッドレイジは最後</b>というより、
/// ダメージ計算は「カード攻撃力（＋条件付き上乗せ）の合算 → ゴッドレイジなら 2 倍」という <b>一括</b>が中心（<see cref="BattleProcessor"/>）。
/// マジカルエクスプロージョンは <b>MP 消費のリスト順</b>のあと、残り MP を 2 倍加算（<see cref="MagicalExplosionRules"/>）。完全な逐次解決 API は未分離。
/// <b>ルール B</b>：召喪加護・リヴァ等は <see cref="SummonPassiveBlessingApplier"/> 経由で数値に常時乗算／抑制（演出なし）。
/// 衰弱は <see cref="PlayerStatus.ApplyOutgoingDamageModifiers"/> 経由（表示上は → 行などで補足。数値 1–5 半減等の厳密条件は各 <see cref="IStatusEffect"/> 実装側）。
/// </remarks>
public static class PlayerAttackTotalDisplayFlow
{
    /// <summary>新しい攻撃シーケンスの冒頭。前回の表示抑制・緑 16 ロック等を掃除する。</summary>
    public static void OnNewPlayerAttackSequenceBegin(CardStatsDisplay d)
    {
        if (d == null) return;
        d.ClearPlayerPreGodRageStackedDisplaySuppressions();
        d.ClearMagicalSwordSubGodRagePlayerAtkDisplayLock();
    }

    /// <summary>
    /// フロー 2 → 3 の狭間：マジカルソード任意 MP 払い等が済んだ直後。
    /// 2 倍予測は維持しつつ、上乗せ分を一時的に表示から外す（例: 6×2=12）。
    /// </summary>
    public static void AfterModifierCommitBeforeCardReveal_InterstitialMsPlusGod(
        CardStatsDisplay d,
        int magicalSwordOptionalPowerBonusIfPaid)
    {
        if (d == null) return;
        d.SetPlayerMsGodComboInterstitialPreCardReveal(magicalSwordOptionalPowerBonusIfPaid);
    }

    /// <summary>フロー 3：カードを 1 枚ずつ掲出する直前。基礎 ATK 相当（例: 6）を出す抑止用。</summary>
    public static void EnterSequentialCardReveal_PrimaryAttackBaseOnly_MsWithGodRage(
        CardStatsDisplay d)
    {
        if (d == null) return;
        d.SetPlayerMsGodComboCardRevealPhase();
    }

    public static void ClearPreRampStateOnPlayerAttackSequenceCancel(CardStatsDisplay d)
    {
        if (d == null) return;
        d.ClearPlayerPreGodRageStackedDisplaySuppressions();
        d.ClearMagicalSwordSubGodRagePlayerAtkDisplayLock();
    }
}
