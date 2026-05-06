using UnityEngine;

/// <summary>
/// フェード付きシーン切り替えの共通入口。<see cref="SceneTransitionManager"/> 周りの重複を避けます。
/// </summary>
public static class SceneFadeNavigation
{
    /// <summary>
    /// <paramref name="sceneName"/> が空でなく、<see cref="SceneTransitionManager"/> が利用可能なときだけフェード遷移します。
    /// </summary>
    /// <returns>遷移処理を開始できたとき true。</returns>
    public static bool TryFadeToScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;

        if (SceneTransitionManager.I == null || !SceneTransitionManager.I.gameObject.activeInHierarchy)
        {
            Debug.LogWarning(
                "SceneFadeNavigation: SceneTransitionManager が利用できないため、シーン遷移をスキップしました: " + sceneName);
            return false;
        }

        SceneTransitionManager.I.FadeToScene(sceneName);
        return true;
    }
}
