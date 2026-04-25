using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Resourcesの MPcostPopup：魔法で支払う / 払わない（通常）の2択を返す（マジカルソード用）。
/// 実行時 <see cref="ShowAndWaitAsync"/> がコンポーネントを付与する。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class MPCostPopupUI : MonoBehaviour
{
    public enum Choice
    {
        PayMpForBoost,
        NormalWithoutBoost
    }

    [SerializeField] private Button _magicButton;
    [SerializeField] private Button _normalButton;

    private void Awake()
    {
        if (_magicButton == null || _normalButton == null)
            WireButtonsByName();
    }

    private void WireButtonsByName()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (_magicButton == null && t.name == "MagicAttackButton")
                _magicButton = t.GetComponent<Button>();
            if (_normalButton == null && t.name == "NormalAttackButton")
                _normalButton = t.GetComponent<Button>();
        }
    }

    public static async Task<Choice> ShowAndWaitAsync(CancellationToken cancellationToken)
    {
        if (BattleUIManager.I == null) return Choice.NormalWithoutBoost;
        var canvas = BattleUIManager.I.GetPopupCanvas();
        if (canvas == null) return Choice.NormalWithoutBoost;
        var prefab = Resources.Load<GameObject>("Prefab/MPcostPopup");
        if (prefab == null)
        {
            Debug.LogError("[MPCostPopupUI] Resources/Prefab/MPcostPopup が見つかりません");
            return Choice.NormalWithoutBoost;
        }
        var go = UnityEngine.Object.Instantiate(prefab, canvas.transform, false);
        if (go == null) return Choice.NormalWithoutBoost;
        if (go.GetComponent<MPCostPopupUI>() == null)
            go.AddComponent<MPCostPopupUI>();
        var comp = go.GetComponent<MPCostPopupUI>();
        if (comp._magicButton == null || comp._normalButton == null)
            comp.WireButtonsByName();
        try
        {
            return await comp.RunAndWaitForChoiceAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Choice.NormalWithoutBoost;
        }
        finally
        {
            if (go != null)
                UnityEngine.Object.Destroy(go);
        }
    }

    private async Task<Choice> RunAndWaitForChoiceAsync(CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<Choice>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (_magicButton == null || _normalButton == null)
            return Choice.NormalWithoutBoost;

        void OnMagic() => tcs.TrySetResult(Choice.PayMpForBoost);
        void OnNormal() => tcs.TrySetResult(Choice.NormalWithoutBoost);
        _magicButton.onClick.AddListener(OnMagic);
        _normalButton.onClick.AddListener(OnNormal);
        using (cancellationToken.Register(() => tcs.TrySetCanceled()))
        {
            try
            {
                return await tcs.Task.ConfigureAwait(true);
            }
            finally
            {
                if (_magicButton != null) _magicButton.onClick.RemoveListener(OnMagic);
                if (_normalButton != null) _normalButton.onClick.RemoveListener(OnNormal);
            }
        }
    }
}
