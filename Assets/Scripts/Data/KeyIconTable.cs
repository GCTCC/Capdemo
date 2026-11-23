using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// 문구 속 {F} 같은 토큰을 키 아이콘으로 바꾸는 표. Create > CapDemo > 데이터 > 키 아이콘 표 (게임에 하나)
// 토큰 이름은 대소문자를 가리지 않는다. 표에 없는 토큰은 중괄호 안 글자를 그대로 키 모양으로 보여 준다.
[CreateAssetMenu(menuName = "CapDemo/데이터/키 아이콘 표", fileName = "키아이콘표", order = 5)]
public class KeyIconTable : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("중괄호 안 이름 (예: F → 문구에 {F})")]
        public string token = "";
        [Tooltip("키 모양 안에 들어갈 글자 (예: F, SPACE, ↑)")]
        public string label = "";
        [Tooltip("글자 대신 쓸 그림. 비우면 글자")]
        public Sprite icon;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    public IReadOnlyList<Entry> Entries => entries;

    public Entry Find(string token)
    {
        foreach (var e in entries)
            if (string.Equals(e.token, token, StringComparison.OrdinalIgnoreCase)) return e;
        return null;
    }

    // 문구의 {토큰}을 바꾼다. keyText는 키 하나를 어떻게 보일지 정한다(예: TMP 태그로 감싸기).
    // keyText가 없으면 [F]처럼 대괄호로 감싼다. {{ 는 중괄호 글자 하나.
    public string Format(string text, Func<string, Entry, string> keyText = null)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
        var sb = new StringBuilder(text.Length + 16);
        int i = 0;
        while (i < text.Length)
        {
            char ch = text[i];
            if (ch == '{' && i + 1 < text.Length && text[i + 1] == '{')
            {
                sb.Append('{');
                i += 2;
                continue;
            }
            int close = ch == '{' ? text.IndexOf('}', i + 1) : -1;
            if (close < 0)
            {
                sb.Append(ch);
                i++;
                continue;
            }
            string token = text.Substring(i + 1, close - i - 1).Trim();
            var entry = Find(token);
            string label = entry != null && !string.IsNullOrEmpty(entry.label) ? entry.label : token;
            sb.Append(keyText != null ? keyText(label, entry) : "[" + label + "]");
            i = close + 1;
        }
        return sb.ToString();
    }

    // 표가 없을 때도 쓸 수 있게: 진행 관리자가 찾은 표로 바꾸고, 없으면 [F] 모양
    public static string FormatDefault(string text, Func<string, Entry, string> keyText = null)
    {
        var table = Progress.Catalog ? Progress.Catalog.KeyIcons : null;
        if (table) return table.Format(text, keyText);
        var temp = CreateInstance<KeyIconTable>();
        string result = temp.Format(text, keyText);
        DestroyImmediate(temp);
        return result;
    }
}
