public interface IStatusEffect
{

    StatusEffectType EffectType { get; }
    void ApplyEffect(PlayerStatus target);
    void OnTurnStart(PlayerStatus target);
    void OnRemove(PlayerStatus target);

    int ModifyDamage(int originalDamage);

    /// <summary>与えるダメージ用（衰弱など）。受け手に渡す直前の値に対して順に適用する。</summary>
    int ModifyOutgoingDamage(int outgoingDamage);

    bool IsExpired(); // ó‘ÔˆÙí‚ªI—¹‚µ‚Ä‚¢‚é‚©‚Ç‚¤‚©

    string GetEffectName(); // UI‚È‚Ç‚Å•\Ž¦‚·‚é‚½‚ß‚Ì–¼‘Oi—áFu“Åv‚È‚Çj

    string GetDescription(); // à–¾•¶i—áFu–ˆƒ^[ƒ“HP‚ª1Œ¸­‚·‚év‚È‚Çj

}