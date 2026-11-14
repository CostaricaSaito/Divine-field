using Coffee.UIEffects;
using TMPro;
using UnityEngine;

/// <summary>
/// Slide-in, per-character pop, then rise + fade for one notification banner.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleNotificationView : MonoBehaviour
{
    private const float SlideInSeconds = 0.5f;
    private const float HoldSeconds = 1.5f;
    private const float FadeSeconds = 0.5f;
    private const float ExitRise = 80f;
    private const float PopScale = 1.75f;
    private const float PopSeconds = 0.12f;

    private enum Phase
    {
        SlideIn,
        Hold,
        Exit,
    }

    private RectTransform _rect;
    private TMP_Text _label;
    private CanvasGroup _group;
    private Phase _phase;
    private float _phaseTime;
    private Vector2 _start;
    private Vector2 _rest;
    private Vector2 _exit;
    private int _charCount;
    private int _shown;
    private float _charInterval;
    private float[] _appearAt;
    private bool _playing;
    private bool _wasGlyphPopping;

    public void Play(
        string message,
        Vector2 startAnchored,
        Vector2 restAnchored,
        AudioClip soundClip = null,
        float soundVolumeScale = 0f)
    {
        _rect = transform as RectTransform;
        _label = GetComponentInChildren<TMP_Text>(true);
        if (_rect == null || _label == null || string.IsNullOrEmpty(message))
        {
            Destroy(gameObject);
            return;
        }

        StopPreviewTweener();

        _group = GetComponent<CanvasGroup>();
        if (_group == null)
            _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 1f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        _label.richText = false;
        _label.enableWordWrapping = false;
        _label.overflowMode = TextOverflowModes.Overflow;
        _label.text = message;
        _label.maxVisibleCharacters = 0;
        _label.OnPreRenderText -= HandlePreRenderText;
        _label.OnPreRenderText += HandlePreRenderText;

        _charCount = message.Length;
        _charInterval = _charCount > 0 ? SlideInSeconds / _charCount : 0f;
        _appearAt = new float[_charCount];
        _shown = 0;
        _start = startAnchored;
        _rest = restAnchored;
        _exit = restAnchored + new Vector2(0f, ExitRise);
        _phase = Phase.SlideIn;
        _phaseTime = 0f;
        _playing = true;
        _wasGlyphPopping = false;
        _rect.anchoredPosition = _start;

        RevealUpTo(1);
        SoundEffectCatalogSO.Get()?.PlayBattleNotification(soundClip, soundVolumeScale);
    }

    private void OnEnable()
    {
        if (_playing && _label != null)
            _label.OnPreRenderText += HandlePreRenderText;
    }

    private void OnDisable()
    {
        if (_label != null)
            _label.OnPreRenderText -= HandlePreRenderText;
    }

    private void LateUpdate()
    {
        if (!_playing || _rect == null) return;

        float dt = Time.unscaledDeltaTime;
        _phaseTime += dt;

        switch (_phase)
        {
            case Phase.SlideIn:
                float inU = EaseOutCubic(Mathf.Clamp01(_phaseTime / SlideInSeconds));
                _rect.anchoredPosition = Vector2.LerpUnclamped(_start, _rest, inU);
                int count = _charInterval <= 0f
                    ? _charCount
                    : Mathf.Clamp(Mathf.FloorToInt(_phaseTime / _charInterval) + 1, 0, _charCount);
                RevealUpTo(count);
                if (_phaseTime >= SlideInSeconds)
                {
                    _rect.anchoredPosition = _rest;
                    RevealUpTo(_charCount);
                    _phase = Phase.Hold;
                    _phaseTime = 0f;
                }
                break;

            case Phase.Hold:
                if (_phaseTime >= HoldSeconds)
                {
                    _phase = Phase.Exit;
                    _phaseTime = 0f;
                }
                break;

            case Phase.Exit:
                float outU = EaseInOutCubic(Mathf.Clamp01(_phaseTime / FadeSeconds));
                _rect.anchoredPosition = Vector2.LerpUnclamped(_rest, _exit, outU);
                if (_group != null)
                    _group.alpha = 1f - outU;
                if (_phaseTime >= FadeSeconds)
                {
                    _playing = false;
                    Destroy(gameObject);
                    return;
                }
                break;
        }

        bool glyphPopping = IsAnyGlyphPopping();
        if (glyphPopping)
            _label.SetVerticesDirty();
        else if (_wasGlyphPopping)
            RestoreTextMeshToDefaultLayout();

        _wasGlyphPopping = glyphPopping;
    }

    private void RevealUpTo(int count)
    {
        if (_label == null || _appearAt == null) return;
        count = Mathf.Clamp(count, 0, _charCount);
        if (count <= _shown) return;

        float now = Time.unscaledTime;
        for (int i = _shown; i < count; i++)
            _appearAt[i] = now;
        _shown = count;
        _label.maxVisibleCharacters = _shown;
        _label.SetVerticesDirty();
    }

    private bool IsAnyGlyphPopping()
    {
        if (_appearAt == null || _shown <= 0) return false;
        float now = Time.unscaledTime;
        for (int i = 0; i < _shown; i++)
        {
            if (now - _appearAt[i] < PopSeconds)
                return true;
        }
        return false;
    }

    private void HandlePreRenderText(TMP_TextInfo textInfo)
    {
        if (!_playing || textInfo == null || _appearAt == null) return;

        float now = Time.unscaledTime;
        int limit = Mathf.Min(_shown, textInfo.characterCount);
        for (int i = 0; i < limit; i++)
        {
            float age = now - _appearAt[i];
            float scale = 1f;
            if (age < PopSeconds)
            {
                float t = Mathf.Clamp01(age / PopSeconds);
                scale = Mathf.Lerp(PopScale, 1f, EaseOutCubic(t));
            }

            ScaleCharacter(textInfo, i, scale);
        }
    }

    private void RestoreTextMeshToDefaultLayout()
    {
        if (_label == null) return;
        _label.ForceMeshUpdate(true, true);
        _label.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }

    private static void ScaleCharacter(TMP_TextInfo textInfo, int index, float scale)
    {
        var character = textInfo.characterInfo[index];
        if (!character.isVisible) return;

        int materialIndex = character.materialReferenceIndex;
        int vertexIndex = character.vertexIndex;
        if (materialIndex < 0 || materialIndex >= textInfo.meshInfo.Length) return;

        var vertices = textInfo.meshInfo[materialIndex].vertices;
        if (vertices == null || vertexIndex + 3 >= vertices.Length) return;

        Vector3 mid = (vertices[vertexIndex] + vertices[vertexIndex + 2]) * 0.5f;
        for (int v = 0; v < 4; v++)
            vertices[vertexIndex + v] = mid + (vertices[vertexIndex + v] - mid) * scale;
    }

    private void StopPreviewTweener()
    {
        var tweeners = GetComponentsInChildren<UIEffectTweener>(true);
        for (int i = 0; i < tweeners.Length; i++)
        {
            var tweener = tweeners[i];
            if (tweener == null) continue;
            tweener.playOnEnable = UIEffectTweener.PlayOnEnable.None;
            tweener.enabled = false;
        }
    }

    private static float EaseOutCubic(float t)
    {
        float u = 1f - Mathf.Clamp01(t);
        return 1f - u * u * u;
    }

    private static float EaseInOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        if (t < 0.5f)
            return 4f * t * t * t;
        float u = -2f * t + 2f;
        return 1f - (u * u * u) / 2f;
    }
}
