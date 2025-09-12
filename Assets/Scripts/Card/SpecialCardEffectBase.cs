using UnityEngine;

/// <summary>
/// 特殊カード効果の共通ベースクラス
/// これを継承した ScriptableObject に Activate 処理を記述します
/// </summary>
public abstract class SpecialCardEffectBase : ScriptableObject, ISpecialCardEffect
{
    [TextArea(2, 4)]
    public string effectDescription;  // Inspector表示用の説明（任意）

    /// <summary>
    /// 特殊カードの発動処理
    /// </summary>
    /// <param name="player">このカードを使う側</param>
    /// <param name="enemy">相手プレイヤー</param>
    public abstract void Activate(PlayerStatus player, PlayerStatus enemy);
}