using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonDisabler : MonoBehaviour
{
    private Button button;

    void Awake()
    {
        button = GetComponent<Button>();
    }

    void OnEnable()
    {
        // 画面に戻ってきたとき、毎回ボタンを有効にする
        if (button != null)
        {
            button.interactable = true;
        }
    }

    public void DisableAfterClick()
    {
        if (button != null)
        {
            button.interactable = false;
        }
    }
}