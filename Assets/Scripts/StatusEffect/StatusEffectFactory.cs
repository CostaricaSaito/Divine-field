using UnityEngine;

public static class StatusEffectFactory
{
    public static IStatusEffect Create(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.Weaken:
                return new WeakenEffect();

            // ‘¼‚Ìó‘ÔˆÙí‚à’Ç‰Á
            default:
                Debug.LogWarning($"–¢À‘•‚Ìó‘ÔˆÙí: {type}");
                return null;
        }
    }
}