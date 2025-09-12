using UnityEngine;
using UnityEngine.Rendering.Universal;

public class LightGrow : MonoBehaviour
{
    public Light2D pointLight;
    public float targetRadius = 5f;
    public float growthSpeed = 1f;

    void Start()
    {
        if (pointLight == null)
        {
            Debug.LogError("Light2D‚ªİ’è‚³‚ê‚Ä‚¢‚Ü‚¹‚ñI");
        }
        else
        {
            Debug.Log("Light2D‰Šú”¼Œa: " + pointLight.pointLightOuterRadius);
        }
    }

    void Update()
    {
        if (pointLight != null)
        {
            if (pointLight.pointLightOuterRadius < targetRadius)
            {
                pointLight.pointLightOuterRadius += growthSpeed * Time.deltaTime;
                Debug.Log("Œ»İ‚Ì”¼Œa: " + pointLight.pointLightOuterRadius);
            }
        }
    }
}