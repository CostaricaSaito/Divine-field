using DigitalRuby.LightningBolt;
using System.Collections;
using TMPro;
using UnityEngine;


public class CutInController : MonoBehaviour
{
    [Header("CutIn UI")]
    [SerializeField] private RectTransform cutInTextRect;
    [SerializeField] private TMP_Text cutInText;
    [SerializeField] private float fontSize = 64f;
    [SerializeField] private Color fontColor = Color.white;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip cutInSE;

    [Header("設定パラメータ")]
    [SerializeField] private Vector2 startPos = new Vector2(-1000f, 0);  // 画面外スタート
    [SerializeField] private Vector2 endPos = new Vector2(0f, 0);        // 中央表示
    [SerializeField] private Vector2 exitPos = new Vector2(1000f, 0);  // ★右に出ていく位置
    [SerializeField] private float pauseDuration = 1f;
    [SerializeField] private float slideDuration = 1f;                   // 時間（秒）
    [SerializeField] private float rotationAngle = -10f;  // ← 追加！

    [SerializeField] private GameObject lightningPrefab; // ← InspectorでPrefabアサイン
    [SerializeField] private AudioClip thunderSE;

    public System.Action OnCutInComplete; // ←外部に通知するイベント

    void Awake()
    {
        cutInText.fontSize = fontSize;
        cutInText.color = fontColor;
    }
        void Start()
    {
        if (lightningPrefab)
            lightningPrefab.SetActive(false);
    }

    public void PlayCutIn()
    {
        cutInTextRect.anchoredPosition = startPos;
        cutInTextRect.localRotation = Quaternion.Euler(0, 0, rotationAngle); // 斜め表示
        cutInText.gameObject.SetActive(true);
        cutInText.text = "BATTLE OF PRIDE.\n誇りのためにいざ戦え！";

        if (audioSource && cutInSE)
            audioSource.PlayOneShot(cutInSE);

        StartCoroutine(SlideText());

     }

    IEnumerator SlideText()
    {
        float elapsed = 0f;

        // STEP 1: 左→中央にスライドイン
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
                float eased = EaseOutExpo(t);
            cutInTextRect.anchoredPosition = Vector2.Lerp(startPos, endPos, eased);
            yield return null;
        }

        // STEP 2: 一時停止
        yield return new WaitForSeconds(pauseDuration);

        if (lightningPrefab)
        {
            lightningPrefab.SetActive(true); // 表示

            lightningPrefab.transform.position = Vector3.zero; // 画面中央に移動（必要なら修正）

            audioSource.PlayOneShot(thunderSE); // 効果音

            yield return new WaitForSeconds(0.5f); // 0.5秒表示

            lightningPrefab.SetActive(false); // 非表示
        }

        // STEP 3: 中央→右にスライドアウト
        elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            float eased = EaseInExpo(t);
            cutInTextRect.anchoredPosition = Vector2.Lerp(endPos, exitPos, eased);
            yield return null;
        }

        // STEP 4: 非表示にする
        cutInText.gameObject.SetActive(false);

        // STEP 5: INTRO演出終了をBattleManagerにコールバック
        OnCutInComplete?.Invoke(); // ←Intro終了を通知
    }

    // EaseOutExpo（滑らかに減速）
    float EaseOutExpo(float t)
    {
        return t == 1 ? 1 : 1 - Mathf.Pow(2, -10 * t);
    }
    float EaseInExpo(float t)
    {
        return t == 0 ? 0 : Mathf.Pow(2, 10 * (t - 1));
    }

}