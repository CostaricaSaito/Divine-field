using System;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

/// <summary>
/// Entry point for the battle-start intro, kept on the CutinManager object so the
/// trigger point inside BattleOpeningCoordinator stays unchanged.
///
/// The visuals live in BattleStartIntroPresentation; the two serialized fields only
/// exist to switch off the legacy scene objects of the previous cut-in.
/// </summary>
public class CutInController : MonoBehaviour
{
    [Header("Legacy cut-in objects (disabled on startup)")]
    [SerializeField] private TMP_Text cutInText;
    [SerializeField] private GameObject lightningPrefab;

    /// <summary>Raised when the intro finished, so the opening sequence can continue.</summary>
    public Action OnCutInComplete;

    private CancellationTokenSource _introCts;

    private void Awake()
    {
        _introCts = new CancellationTokenSource();
    }

    private void Start()
    {
        if (cutInText != null)
        {
            cutInText.text = string.Empty;
            cutInText.gameObject.SetActive(false);
        }

        if (lightningPrefab != null)
            lightningPrefab.SetActive(false);
    }

    private void OnDestroy()
    {
        _introCts?.Cancel();
        _introCts?.Dispose();
        _introCts = null;
    }

    public void PlayCutIn()
    {
        _ = PlayCutInAsync();
    }

    public async Task PlayCutInAsync()
    {
        var ct = _introCts?.Token ?? CancellationToken.None;
        try
        {
            await BattleStartIntroPresentation.RunAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Scene torn down mid-intro.
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CutInController] Battle start intro failed: {ex}");
        }
        finally
        {
            OnCutInComplete?.Invoke();
        }
    }
}
