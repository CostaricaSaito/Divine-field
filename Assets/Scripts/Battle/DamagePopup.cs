using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ダメージ／回復／メッセージ用フローティングテキスト。
/// レイアウト（パネル内位置・Rect）は BattleUIManager とプレハブ側。このクラスは主に文言・色・浮き／フェード。
/// </summary>
public class DamagePopup : MonoBehaviour
{
    // --- Inspector 参照（DamagePopUp プレハブで割り当て）---
    // ルートの Image（任意）。闇フォロー時に紫背景へ塗り替える。未割り当てなら GetComponent。
    [SerializeField] private Image panelBackground;
    // valueText … 大きい数字、または「無傷」など1行メッセージ全体（Setup 時）
    [SerializeField] private TMP_Text valueText;
    // labelText … 「ダメージ」の小さい行。Setup では非表示にする。null のプレハブなら未使用。
    [SerializeField] private TMP_Text labelText;

    // --- 演出パラメータ（Inspector からも変更可）---
    // floatSpeed … 上方向に漂う速度。大きいほど速く上に抜ける（ワールド／ローカルは親の向き依存。通常は上へ）。
    public float floatSpeed = 30f;
    // fadeDuration … 何秒かけて透明になるか。大きいほど長く残る。Destroy もこの秒後。
    public float fadeDuration = 1f;

    /// <summary>UI 生成に失敗したときなど、闇フォロー前の待ちに使う既定秒数（<see cref="fadeDuration"/> のデフォルトと一致）。</summary>
    public const float DefaultFadeDurationIfUnknown = 1f;

    private float timer;
    // CanvasGroup … ない場合はフェードなし（透明度は変わらず、そのまま消えるまで表示）。
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        // valueText 未割り当て時の保険：子の TMP を1つ拾う（複数あると意図しないものを掴むので、基本は Inspector で明示推奨）。
        if (valueText == null)
            valueText = GetComponentInChildren<TMP_Text>(true);
        if (panelBackground == null)
            panelBackground = GetComponent<Image>();
    }

    private void Start()
    {
        // フェード用：ルートに無ければ子（内側 Canvas など）を探す。
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = GetComponentInChildren<CanvasGroup>(true);
    }

    /// <summary>
    /// 回復・ミス・病系メッセージ・両替表示など「1ブロックの文字列」向け。
    /// </summary>
    /// <param name="message">そのまま表示する全文。</param>
    /// <param name="fillColor">文字の塗りつぶし色（縁は ApplyFillAndOutline 参照）。</param>
    public void Setup(string message, Color fillColor)
    {
        // 2行レイアウト用ラベルは使わないので消す（表示領域の衝突防止）。
        if (labelText != null)
            labelText.gameObject.SetActive(false);

        if (valueText == null) return;
        valueText.gameObject.SetActive(true);
        valueText.text = message;
        // 長文だけ自動縮小：文字数しきい値を変えれば「いつから縮小するか」が変わる。
        valueText.enableAutoSizing = message != null && message.Length > 12;
        ApplyFillAndOutline(valueText, fillColor);
    }

    /// <summary>
    /// 戦闘ダメージ表示：ダメージありは数字＋「ダメージ」、0 のときは1行（無傷）など。
    /// </summary>
    /// <param name="amount">与ダメ。0 以下は else 側の見た目。</param>
    /// <param name="damageHitsPlayer">true＝プレイヤーが食らう（シアン系）、false＝敵が食らう（赤系）。色は下の fill を編集。</param>
    public void SetupDamage(int amount, bool damageHitsPlayer)
    {
        if (valueText == null) return;

        Color fill;
        if (amount > 0)
        {
            // 数字の見た目：プレハブの fontSize よりここが優先される。
            valueText.enableAutoSizing = false;
            valueText.fontSize = 160f;
            valueText.text = amount.ToString();
            if (labelText != null)
            {
                labelText.gameObject.SetActive(true);
                // 「ダメージ」の文言を変えたい場合はここ（例：「DMG」）。
                labelText.text = "ダメージ";
            }
            // 食らう側がプレイヤーならシアン、敵なら赤。RGB をいじればトーン変更。
            fill = damageHitsPlayer ? new Color(0.25f, 0.95f, 1f) : new Color(0.92f, 0.12f, 0.18f);
            ApplyFillAndOutline(valueText, fill);
            if (labelText != null)
                ApplyFillAndOutline(labelText, fill);
        }
        else
        {
            // 0 ダメージ：ラベルは使わず value だけで表現（2行にしない）。
            if (labelText != null)
                labelText.gameObject.SetActive(false);
            // 表示文言を変えたい場合はここ（例：「ダメージなし！」）。
            valueText.text = "無傷";
            valueText.fontSize = 160f;
            // 黄色系：無傷のトーンを変えたいときはこの Color。
            fill = new Color(1f, 0.92f, 0.15f);
            ApplyFillAndOutline(valueText, fill);
        }
    }

    /// <summary>
    /// 闇属性の第2段（超過ダメージ適用後の残HP分）。背景を紫系にし、数字は通常の SetupDamage と同じ配色。
    /// </summary>
    public void SetupDarkFollowupDamage(int amount, bool damageHitsPlayer)
    {
        if (panelBackground != null)
            panelBackground.color = new Color(0.28f, 0.1f, 0.42f, 0.94f);
        SetupDamage(amount, damageHitsPlayer);
    }

    /// <summary>
    /// 状態異常付与表示：公式名をオートサイズで大きく表示。ラベル行は使わない。
    /// </summary>
    public void SetupStatusAilmentGrant(string ailmentDisplayName, Color panelBackgroundColor, Color textFillColor)
    {
        if (labelText != null)
            labelText.gameObject.SetActive(false);
        if (panelBackground != null)
            panelBackground.color = panelBackgroundColor;
        if (valueText == null) return;
        valueText.gameObject.SetActive(true);
        valueText.text = ailmentDisplayName ?? string.Empty;
        valueText.enableWordWrapping = false;
        valueText.overflowMode = TextOverflowModes.Overflow;
        valueText.enableAutoSizing = true;
        valueText.fontSizeMin = 12f;
        valueText.fontSizeMax = 160f;
        ApplyFillAndOutline(valueText, textFillColor);
    }

    /// <summary>
    /// 文字の「塗り」と TMP のアウトライン（縁）をまとめて適用。見た目の基準はここが強い。
    /// </summary>
    private static void ApplyFillAndOutline(TMP_Text t, Color fill)
    {
        if (t == null) return;
        // 面の色（敵への赤・自分へのシアンなど）。
        t.color = fill;
        t.fontStyle = FontStyles.Bold;
        // アウトライン幅：プレハブで既に太ければそのまま。細すぎるときだけ下限を 0.22 に引き上げ。
        // もっと太い縁にしたいなら 0.22 を大きくする、またはプレハブの Outline を触る。
        if (t.outlineWidth < 0.08f)
            t.outlineWidth = 0.22f;
        // 縁の色：白のまま＝赤文字に白縁。縁も赤くしたい場合は Color.red などに変更。
        t.outlineColor = Color.white;
    }

    private void Update()
    {
        timer += Time.deltaTime;

        // 親の座標系で上方向へ移動。斜めにしたいなら Vector3.up を変えるか、RectTransform.anchoredPosition をいじる方式に変更。
        transform.Translate(Vector3.up * (floatSpeed * Time.deltaTime));

        // フェード：canvasGroup が無いと何もしない（常に不透明のまま消滅まで）。
        if (canvasGroup != null)
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);

        // 表示時間＝fadeDuration 秒後に破棄。長く見せたいなら fadeDuration を伸ばす。
        if (timer >= fadeDuration)
            Destroy(gameObject);
    }
}
