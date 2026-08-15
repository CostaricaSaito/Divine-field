using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Spawns and plays the UltimateReady prefab on the battle popup canvas.
/// </summary>
public static class UltimateReadyPresentation
{
    public const string PrefabResourcePath = "Prefab/UltimateReady";
    public const string SoundEffectPath = "Assets/SE/アルティメットレディ.mp3";
    public const float PreShowWhiteFlashMs = 50f;

    public static async Task PlayAsync(CancellationToken ct = default)
    {
        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogError($"[UltimateReadyPresentation] Resources/{PrefabResourcePath} not found");
            return;
        }

        var canvas = BattleUIManager.I != null
            ? BattleUIManager.I.GetPopupCanvas() ?? BattleUIManager.I.GetMainUICanvas()
            : null;
        if (canvas == null)
        {
            Debug.LogWarning("[UltimateReadyPresentation] No battle canvas available");
            return;
        }

        var instance = Object.Instantiate(prefab, canvas.transform, false);
        var rt = instance.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localScale = Vector3.one;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }

        instance.transform.SetAsLastSibling();

        var view = instance.GetComponent<UltimateReadyPresentationView>();
        if (view == null)
            view = instance.AddComponent<UltimateReadyPresentationView>();

        try
        {
            await view.PlayAsync(ct);
        }
        catch (System.OperationCanceledException)
        {
            if (instance != null)
                Object.Destroy(instance);
            throw;
        }
        finally
        {
            if (instance != null)
                Object.Destroy(instance);
        }
    }
}
