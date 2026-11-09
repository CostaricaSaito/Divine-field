using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Moves hand CardUI roots (ATK/DEF labels follow as children) with LeanTween.
/// </summary>
public sealed class HandSortPresenter
{
    public readonly struct CardMoveJob
    {
        public readonly RectTransform Card;
        public readonly Vector2 StartAnchored;
        public readonly Vector2 EndAnchored;

        public CardMoveJob(RectTransform card, Vector2 startAnchored, Vector2 endAnchored)
        {
            Card = card;
            StartAnchored = startAnchored;
            EndAnchored = endAnchored;
        }
    }

    private readonly float _durationSeconds;

    public HandSortPresenter(float durationSeconds = 1f)
    {
        _durationSeconds = Mathf.Max(0.01f, durationSeconds);
    }

    public async Task PlayReorderCardMoveAsync(IReadOnlyList<CardMoveJob> jobs, CancellationToken ct)
    {
        if (jobs == null || jobs.Count == 0)
            return;

        var tweenIds = new List<int>();
        try
        {
            for (int i = 0; i < jobs.Count; i++)
            {
                if (ct.IsCancellationRequested) return;

                var job = jobs[i];
                if (job.Card == null) continue;

                if ((job.StartAnchored - job.EndAnchored).sqrMagnitude < 0.25f)
                    continue;

                job.Card.anchoredPosition = job.StartAnchored;

                var rt = job.Card;
                int id = LeanTween.value(rt.gameObject, job.StartAnchored, job.EndAnchored, _durationSeconds)
                    .setEase(LeanTweenType.easeOutCubic)
                    .setOnUpdateVector2(v => rt.anchoredPosition = v)
                    .id;
                tweenIds.Add(id);
            }

            if (tweenIds.Count == 0) return;

            int ms = Mathf.Max(1, Mathf.RoundToInt(_durationSeconds * 1000f));
            await Task.Delay(ms, ct);

            for (int i = 0; i < jobs.Count; i++)
            {
                var job = jobs[i];
                if (job.Card == null) continue;
                job.Card.anchoredPosition = job.EndAnchored;
            }
        }
        finally
        {
            for (int i = 0; i < tweenIds.Count; i++)
                LeanTween.cancel(tweenIds[i]);
        }
    }

    public static Vector3 GetRectTransformWorldCenter(RectTransform rt)
    {
        if (rt == null) return Vector3.zero;
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return (c[0] + c[1] + c[2] + c[3]) * 0.25f;
    }
}
