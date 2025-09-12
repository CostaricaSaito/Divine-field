using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonSEPlayer : MonoBehaviour
{
    [Header("効果音（クリック時）")]
    public AudioClip clickSE;

    private Button button;

    void Start()
    {
        button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(PlayClick);
        }
        else
        {
            Debug.LogWarning("Button コンポーネントが見つかりませんでした。");
        }
    }

    public void PlayClick()
    {
        if (SEPlayer.I != null && clickSE != null)
        {
            SEPlayer.I.Play(clickSE);
        }
        else
        {
            Debug.LogWarning("SEPlayerが存在しないか、clickSEが未設定です。");
        }
    }
}
