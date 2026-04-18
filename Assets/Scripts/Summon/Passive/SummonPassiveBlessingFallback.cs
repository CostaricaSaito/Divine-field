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
            default:
                return null;
        }
    }
}
