/// <summary>
/// 状態異常などが付与する残存DEF（戦闘中常時／防御カードと加算）。
/// </summary>
public interface IPermanentDefContributor
{
    PermanentDefContribution GetPermanentDefContribution();
}
