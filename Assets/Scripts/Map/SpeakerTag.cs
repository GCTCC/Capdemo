using System.Collections.Generic;
using UnityEngine;

// 대사의 화자가 이 캐릭터라고 알려 주는 표시. 플레이어·NPC 프리팹에 붙어 있다.
// 대화창의 "Name Beside Speaker"가 켜져 있으면, 화자 이름이 같은 캐릭터 머리 옆에 이름표(SpeakerNameTag)가 뜬다.
[DisallowMultipleComponent]
public class SpeakerTag : MonoBehaviour
{
    [Tooltip("대사의 화자 칸에 적는 이름과 같게 적는다. 비워 두면 자동: 플레이어는 {player}, NPC는 대사 파일에 가장 많이 나오는 화자 이름")]
    [SerializeField] private string speakerName;
    [Tooltip("이름표 꼬리가 가리킬 점. 캐릭터 그림 맨 위 가운데에서 이만큼 옮긴다(유닛). 씬에서 선택하면 하늘색 점으로 보인다")]
    [SerializeField] private Vector2 headOffset = new Vector2(0.15f, -0.25f);

    private static readonly List<SpeakerTag> all = new List<SpeakerTag>();

    private void OnEnable() => all.Add(this);
    private void OnDisable() => all.Remove(this);

    // 화면에 그릴 때 쓰는 이름 ({player}는 저장된 플레이어 이름으로 바뀐다)
    public string SpeakerName
    {
        get
        {
            if (!string.IsNullOrEmpty(speakerName)) return FormatName(speakerName);
            if (IsPlayer) return FormatName(StoryText.PlayerToken);
            var npc = GetComponent<NpcInteract>();
            return npc ? FormatName(MostCommonSpeaker(npc.Lines)) : "";
        }
    }

    // 이름표 꼬리가 가리킬 월드 좌표
    public Vector3 HeadPosition
    {
        get
        {
            var body = FindBody();
            Vector3 top = body
                ? new Vector3(body.bounds.center.x, body.bounds.max.y, transform.position.z)
                : transform.position + Vector3.up * 1.5f;
            return top + (Vector3)headOffset;
        }
    }

    private bool IsPlayer => GetComponent<PlayerController2D>();

    // 화자 칸 글자를 화면에 쓸 이름으로: {player}를 바꾸고 앞뒤 빈칸·줄바꿈을 지운다(대사 파일에 실수로 들어간 빈칸 때문에 못 찾는 일이 없게)
    public static string FormatName(string raw) => StoryText.Format(raw).Trim();

    // 화자 칸에 {player}가 들어 있으면 주인공 줄이다. 이름 비교 없이 플레이어 캐릭터로 바로 이어 준다
    public static bool IsPlayerLine(string rawSpeaker) =>
        !string.IsNullOrEmpty(rawSpeaker) && rawSpeaker.Contains(StoryText.PlayerToken);

    // 맵에 있는 플레이어 캐릭터(PlayerController2D가 붙은 쪽)
    public static SpeakerTag FindPlayer()
    {
        foreach (var tag in all)
            if (tag && tag.IsPlayer) return tag;
        return null;
    }

    // 이 줄의 화자 캐릭터: 주인공 줄은 플레이어, 아니면 이름으로 찾는다
    public static SpeakerTag FindFor(string rawSpeaker, Transform preferred) =>
        IsPlayerLine(rawSpeaker) ? FindPlayer() : Find(FormatName(rawSpeaker), preferred);

    // 화자 이름이 같은 캐릭터를 찾는다. 같은 이름이 여럿이면 preferred(지금 대화 중인 NPC)를, 없으면 화면 가운데에 가까운 쪽을 고른다
    public static SpeakerTag Find(string name, Transform preferred)
    {
        if (string.IsNullOrEmpty(name)) return null;
        SpeakerTag best = null;
        float bestDistance = float.MaxValue;
        float centerX = Camera.main ? Camera.main.transform.position.x : 0f;
        foreach (var tag in all)
        {
            // 주인공은 FindPlayer로만 찾는다. 이름 없는 다른 인물("???")이 이름 미입력 주인공("???")에게 붙지 않게
            if (!tag || tag.IsPlayer || tag.SpeakerName != name) continue;
            if (preferred && tag.transform == preferred) return tag;
            float distance = Mathf.Abs(tag.transform.position.x - centerX);
            if (distance < bestDistance)
            {
                best = tag;
                bestDistance = distance;
            }
        }
        return best;
    }

    private SpriteRenderer FindBody()
    {
        var body = GetComponent<SpriteRenderer>();
        return body ? body : GetComponentInChildren<SpriteRenderer>();
    }

    private static string MostCommonSpeaker(IReadOnlyList<StoryLine> lines)
    {
        var counts = new Dictionary<string, int>();
        string best = "";
        int bestCount = 0;
        foreach (var line in lines)
        {
            var speaker = line?.Speaker?.Trim();
            if (string.IsNullOrEmpty(speaker) || IsPlayerLine(speaker)) continue;
            counts.TryGetValue(speaker, out int count);
            counts[speaker] = ++count;
            if (count > bestCount)
            {
                best = speaker;
                bestCount = count;
            }
        }
        return best;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.85f, 1f, 0.9f);
        Gizmos.DrawSphere(HeadPosition, 0.08f);
    }
}
