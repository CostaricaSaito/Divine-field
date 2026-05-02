using UnityEngine;
using UnityEngine.UI;

public class AlphaHitTest : MonoBehaviour
{
    void Start()
    {
        GetComponent<Image>().alphaHitTestMinimumThreshold = 0.1f;
    }
}