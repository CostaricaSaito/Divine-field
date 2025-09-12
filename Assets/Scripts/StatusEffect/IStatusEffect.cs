public interface IStatusEffect
{

    StatusEffectType EffectType { get; }
    void ApplyEffect(PlayerStatus target);
    void OnTurnStart(PlayerStatus target);
    void OnRemove(PlayerStatus target);

    int ModifyDamage(int originalDamage);


    bool IsExpired(); // 状態異常が終了しているかどうか

    string GetEffectName(); // UIなどで表示するための名前（例：「毒」など）

    string GetDescription(); // 説明文（例：「毎ターンHPが1減少する」など）

}