using UnityEngine;

/// <summary>Still-image backdrop motion. Ignored when a playlist entry uses <see cref="UnityEngine.Video.VideoClip"/>.</summary>
public enum BattleBackgroundStillAnimationMode
{
    [InspectorName("⑥ オフ（デフォルト）")]
    Off = 0,

    [InspectorName("① 左→右パン（横長）")]
    PanLeftToRight = 1,

    [InspectorName("② 右→左パン（横長）")]
    PanRightToLeft = 2,

    [InspectorName("③ 上→下パン（縦長）")]
    PanTopToBottom = 3,

    [InspectorName("④ 下→上パン（縦長）")]
    PanBottomToTop = 4,

    [InspectorName("⑤ 拡大（300%）")]
    Zoom300Percent = 5,
}
