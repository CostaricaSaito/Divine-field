using UnityEngine;
using UnityEngine.SceneManagement;

public class SummonConfirmButton : MonoBehaviour
{
    public SummonRingViewer viewer;
    public AudioClip confirmSE;

    public void OnConfirm()
    {
        int selectedIndex = viewer.GetSelectedSummonIndex();
        SummonSelectionManager.I.SetSelectedIndex(selectedIndex);
        SoundEffectPlayer.I.Play(confirmSE);

        viewer.ForceRefresh(); // © ‚±‚ê‚ÅÄ•`‰æI

    }
}