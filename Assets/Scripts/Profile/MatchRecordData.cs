using System;

/// <summary>
/// 1試合分の記録（直近50試合用）。Unity JsonUtility 向けにフィールドのみ。
/// </summary>
[Serializable]
public class MatchRecordData
{
    public int outcome;
    public string summonId;
    /// <summary>記録時刻（UTC）。<see cref="DateTime.ToBinary"/>。</summary>
    public long utcBinary;

    public static MatchRecordData Create(MatchOutcome outcome, string summonId, DateTime utc)
    {
        return new MatchRecordData
        {
            outcome = (int)outcome,
            summonId = summonId ?? string.Empty,
            utcBinary = utc.ToBinary(),
        };
    }
}
