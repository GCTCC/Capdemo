using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

// 한 화면(장면)에 들어가는 일러스트 묶음. Assets/Resources/Illustrations/ 안에 화면마다 하나씩 둔다.
//   Lobby  = 메인 로비 배경 (여러 장, 허브에 들어올 때마다 다음 그림)
//   Ingame = 리듬 인게임 배경 (첫 장)
//   Title  = 타이틀 화면 배경 (첫 장). 동영상(Video)을 넣으면 그림 대신 영상이 반복 재생된다 (그림은 영상 준비 전 첫 순간에 보임)
// 결과 화면 배경·폴라로이드는 원래 쓰던 Resources/ResultV2Settings.asset에 그대로 있다.
//
// 보고 고치는 곳: Tools > CapDemo > 일러스트 관리 (한 창에 다섯 곳이 다 나온다)
// 화면마다 파일을 나눈 이유: 파일 하나를 읽으면 그 안의 그림이 전부 메모리에 올라간다.
// 나눠 두면 각 화면은 자기 그림만 읽는다 (로비에서 폴라로이드 사진까지 올라가지 않음).
// 파일이 없거나 비어 있으면 각 화면은 원래 Inspector에 넣어 둔 그림을 쓴다.
[CreateAssetMenu(fileName = "Illustrations", menuName = "CapDemo/Illustration Set")]
public class IllustrationSet : ScriptableObject
{
    public const string Lobby = "Lobby";
    public const string Ingame = "Ingame";
    public const string Title = "Title";
    public const string Folder = "Illustrations/"; // Resources 기준

    [System.Serializable]
    public class Entry
    {
        public Sprite image;
        [Tooltip("로비 전용: 이 그림에 맞춰 둔 전구 불빛(Lamps)을 켤지")]
        public bool showLamps;
    }

    public List<Entry> entries = new List<Entry>();

    [Tooltip("타이틀 전용: 배경 동영상 (mp4). 비우면 그림만 나온다. 소리는 나오지 않고 계속 반복된다")]
    public VideoClip video;

    // Resources/Illustrations/<name>.asset을 읽는다. 없으면 null
    public static IllustrationSet Load(string name) => Resources.Load<IllustrationSet>(Folder + name);

    // 그림이 들어 있는 칸만 골라 준다
    public List<Entry> Valid()
    {
        var list = new List<Entry>();
        foreach (var e in entries)
            if (e != null && e.image) list.Add(e);
        return list;
    }

    // 첫 번째 그림 (한 장만 쓰는 화면용). 파일이 없거나 비어 있으면 fallback
    public static Sprite FirstOr(string name, Sprite fallback)
    {
        var set = Load(name);
        if (set)
            foreach (var e in set.entries)
                if (e != null && e.image) return e.image;
        return fallback;
    }
}
