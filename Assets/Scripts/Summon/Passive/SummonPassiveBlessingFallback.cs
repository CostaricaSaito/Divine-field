/// <summary>
/// <see cref="SummonData"/> の加護を、アセット名または <see cref="SummonPassiveBlessingMode"/> から生成する。
/// </summary>
public static class SummonPassiveBlessingFallback
{
    public static SummonPassiveBlessing ResolveMode(SummonPassiveBlessingMode mode, string assetBaseName)
    {
        switch (mode)
        {
            case SummonPassiveBlessingMode.None:
                return null;
            case SummonPassiveBlessingMode.Ifrit:
                return new IfritPassiveBlessing();
            case SummonPassiveBlessingMode.Garuda:
                return null;
            case SummonPassiveBlessingMode.Leviathan:
                return new LeviathanPassiveBlessing();
            case SummonPassiveBlessingMode.Diabolos:
                return null;
            case SummonPassiveBlessingMode.Indra:
                return null;
            case SummonPassiveBlessingMode.Shiva:
                return null;
            case SummonPassiveBlessingMode.Arcadias:
                return null;
            case SummonPassiveBlessingMode.Ordin:
                return null;
            case SummonPassiveBlessingMode.AutoByAssetName:
            default:
                return ResolveByAssetName(assetBaseName);
        }
    }

    public static SummonPassiveBlessing ResolveByAssetName(string assetBaseName)
    {
        if (string.IsNullOrEmpty(assetBaseName)) return null;
        switch (assetBaseName)
        {
            case "Ifrit":
                return new IfritPassiveBlessing();
            case "Garuda":
                return null;
            case "Leviathan":
                return new LeviathanPassiveBlessing();
            case "Diabolos":
                return null;
            case "Indra":
                return null;
            case "Siva":
                return null;
            case "Arcadias":
                return null;
            case "Ordin":
                return null;
            default:
                return null;
        }
    }
}
