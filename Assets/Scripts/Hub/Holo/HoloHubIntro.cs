using UnityEngine;

// 홀로그램 허브 캔버스에 붙인다. 씬이 시작되면
//   1) 다른 전환 없이 바로 들어왔다면 꺼진 화면이 지직거리며 켜지는 연출을 틀고
//   2) 화면 요소(HoloEntrance)들을 차례로 나타나게 한다.
public class HoloHubIntro : MonoBehaviour
{
    [Tooltip("요소들이 나타나기 시작하기 전 기다리는 시간(초)")]
    [SerializeField, Min(0f)] private float startDelay = 0.25f;

    private void Start()
    {
        // 지직 전환으로 넘어오는 중이거나 검은 페이드 중이면, 화면 켜기는 그쪽이 해 준다
        if (!HoloTransition.IsBusy && !ScreenFader.IsBusy) HoloTransition.PlayBootIn();

        foreach (var entrance in GetComponentsInChildren<HoloEntrance>(true))
            entrance.Play(startDelay);
    }
}
