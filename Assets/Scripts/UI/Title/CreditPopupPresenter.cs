using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Wires CREDIT prefab back button, scroll layout, and staggered CreditList fade-in.</summary>
[DisallowMultipleComponent]
public sealed class CreditPopupPresenter : MonoBehaviour
{
    const string DefaultCreditListPath = "Scroll View/Viewport/Content/CreditList";
    const string DefaultItemRevealSeAddress = "Assets/SE/カーソル移動1.mp3";

    static readonly string[] ExpandableListChildNames = { "SEList", "TestPlayerList" };

    [SerializeField] private Button backButton;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Transform creditListRoot;
    [SerializeField] private string creditListPath = DefaultCreditListPath;
    [SerializeField] private float itemFadeDurationSeconds = 0.2f;
    [SerializeField] private string itemRevealSeAddress = DefaultItemRevealSeAddress;

    CancellationTokenSource _fadeCts;

    void Awake()
    {
        if (backButton == null)
        {
            var back = transform.Find("BackButton");
            if (back != null)
                backButton = back.GetComponent<Button>();
        }

        if (scrollRect == null)
            scrollRect = GetComponentInChildren<ScrollRect>(true);

        if (creditListRoot == null && !string.IsNullOrEmpty(creditListPath))
            creditListRoot = transform.Find(creditListPath);

        StretchRootToParent();
        DisableOverlayRaycasts();
        PrepareCreditListItemsHidden();

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(Close);
        }
    }

    void Start()
    {
        ResetScrollToTop();
        _fadeCts = new CancellationTokenSource();
        _ = PlayCreditListFadeInAsync(_fadeCts.Token);
    }

    void OnDestroy()
    {
        _fadeCts?.Cancel();
        _fadeCts?.Dispose();
        _fadeCts = null;
    }

    void StretchRootToParent()
    {
        if (transform is not RectTransform root)
            return;
        if (root.parent is not RectTransform)
            return;

        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;
    }

    void DisableOverlayRaycasts()
    {
        DisableRaycastOnTransform(transform.Find("Copyright"));
        DisableRaycastOnTransform(transform.Find("Main CinemaScope/Scope1"));
        DisableRaycastOnTransform(transform.Find("Main CinemaScope/Scope2"));
    }

    static void DisableRaycastOnTransform(Transform target)
    {
        if (target == null)
            return;

        var graphics = target.GetComponentsInChildren<Graphic>(true);
        for (var i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;
    }

    void PrepareCreditListItemsHidden()
    {
        var targets = BuildFadeTargets();
        for (var i = 0; i < targets.Count; i++)
            SetCanvasGroupHidden(targets[i]);
    }

    async Task PlayCreditListFadeInAsync(CancellationToken ct)
    {
        var targets = BuildFadeTargets();
        for (var i = 0; i < targets.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var target = targets[i];
            if (target == null || !target.gameObject.activeInHierarchy)
                continue;

            PlayItemRevealSe();
            var group = GetOrAddCanvasGroup(target);
            await FadeCanvasGroupAsync(group, 0f, 1f, itemFadeDurationSeconds, ct);
            group.interactable = true;
            group.blocksRaycasts = true;
        }
    }

    List<Transform> BuildFadeTargets()
    {
        var targets = new List<Transform>();
        if (creditListRoot == null)
            return targets;

        for (var i = 0; i < creditListRoot.childCount; i++)
            AppendFadeTargets(creditListRoot.GetChild(i), targets);

        return targets;
    }

    static void AppendFadeTargets(Transform section, List<Transform> targets)
    {
        if (section == null)
            return;

        var listChild = FindExpandableListChild(section);
        if (listChild == null)
        {
            targets.Add(section);
            return;
        }

        for (var i = 0; i < section.childCount; i++)
        {
            var child = section.GetChild(i);
            if (child == listChild)
            {
                for (var j = 0; j < listChild.childCount; j++)
                    targets.Add(listChild.GetChild(j));
            }
            else
            {
                targets.Add(child);
            }
        }
    }

    static Transform FindExpandableListChild(Transform section)
    {
        for (var i = 0; i < ExpandableListChildNames.Length; i++)
        {
            var listChild = FindDirectChild(section, ExpandableListChildNames[i]);
            if (listChild != null)
                return listChild;
        }

        return null;
    }

    static Transform FindDirectChild(Transform parent, string childName)
    {
        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name == childName)
                return child;
        }

        return null;
    }

    static void SetCanvasGroupHidden(Transform target)
    {
        var group = GetOrAddCanvasGroup(target);
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    static CanvasGroup GetOrAddCanvasGroup(Transform target)
    {
        var group = target.GetComponent<CanvasGroup>();
        if (group == null)
            group = target.gameObject.AddComponent<CanvasGroup>();
        return group;
    }

    static async Task FadeCanvasGroupAsync(
        CanvasGroup group,
        float from,
        float to,
        float durationSeconds,
        CancellationToken ct)
    {
        if (group == null)
            return;

        if (durationSeconds <= 0f)
        {
            group.alpha = to;
            return;
        }

        group.alpha = from;
        var elapsed = 0f;
        while (elapsed < durationSeconds)
        {
            ct.ThrowIfCancellationRequested();
            elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(elapsed / durationSeconds);
            group.alpha = Mathf.Lerp(from, to, t);
            await Task.Yield();
        }

        group.alpha = to;
    }

    void PlayItemRevealSe()
    {
        if (string.IsNullOrEmpty(itemRevealSeAddress))
            return;

        SoundEffectPlayer.I?.Play(itemRevealSeAddress);
    }

    void ResetScrollToTop()
    {
        if (scrollRect == null || scrollRect.content == null)
            return;

        Canvas.ForceUpdateCanvases();
        scrollRect.velocity = Vector2.zero;
        scrollRect.verticalNormalizedPosition = 1f;
    }

    void Close()
    {
        _fadeCts?.Cancel();
        Destroy(gameObject);
    }
}
