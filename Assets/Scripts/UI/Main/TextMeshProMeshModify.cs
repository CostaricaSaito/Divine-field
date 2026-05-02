using UnityEngine;
using TMPro;

[ExecuteInEditMode]
public class TMPTruePerspective : MonoBehaviour
{
    private TMP_Text m_TextComponent;

    [Header("パースの強さ (0〜1)")]
    public float perspectiveAmount = 0.5f; 
    [Header("左側の倍率")]
    public float leftScale = 1.5f;

    void Awake() => m_TextComponent = GetComponent<TMP_Text>();

    void Update()
    {
        m_TextComponent.ForceMeshUpdate();
        TMP_TextInfo textInfo = m_TextComponent.textInfo;
        int characterCount = textInfo.characterCount;
        if (characterCount == 0) return;

        // 全体の幅を計算
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        for (int i = 0; i < characterCount; i++) {
            if (!textInfo.characterInfo[i].isVisible) continue;
            var verts = textInfo.meshInfo[0].vertices;
            int idx = textInfo.characterInfo[i].vertexIndex;
            for (int j = 0; j < 4; j++) {
                minX = Mathf.Min(minX, verts[idx + j].x);
                maxX = Mathf.Max(maxX, verts[idx + j].x);
            }
        }
        float width = maxX - minX;

        for (int i = 0; i < characterCount; i++)
        {
            if (!textInfo.characterInfo[i].isVisible) continue;
            int matIdx = textInfo.characterInfo[i].materialReferenceIndex;
            int vIdx = textInfo.characterInfo[i].vertexIndex;
            Vector3[] vertices = textInfo.meshInfo[matIdx].vertices;

            for (int j = 0; j < 4; j++)
            {
                Vector3 v = vertices[vIdx + j];
                // 0(左端) ～ 1(右端) の割合
                float t = (v.x - minX) / width;

                // 【重要】パースペクティブの計算
                // 単純な Lerp ではなく、分母に t を入れることで「奥行き」を作ります
                float factor = 1.0f / (1.0f + perspectiveAmount * t);
                
                // 左端のスケールを基準に適用
                float finalScale = leftScale * factor;

                v.x = minX + (v.x - minX) * finalScale; // Xの圧縮
                v.y *= finalScale;                     // Yの圧縮
                
                vertices[vIdx + j] = v;
            }
        }
        m_TextComponent.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }
}