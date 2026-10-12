/// <summary>残存DEFの1エントリ（属性＋値）。</summary>
public readonly struct PermanentDefContribution
{
    public readonly ElementType Element;
    public readonly int Value;

    public PermanentDefContribution(ElementType element, int value)
    {
        Element = element;
        Value = value;
    }
}
