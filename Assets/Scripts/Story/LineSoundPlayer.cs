using System;
using System.Collections.Generic;
using UnityEngine;

// 대사 한 줄에 붙은 효과음(StoryLine.Sounds)을 타이밍에 맞춰 낸다. StoryManager(챕터)와 DemoDialogue(맵 NPC 대화)가 같이 쓴다.
// 쓰는 순서: Begin(줄 시작) → 출력되는 동안 매 프레임 Tick(지금 보이는 글자 수) → 출력이 끝나면 TypingDone → 다음으로 넘길 때 Next.
// - LineStart: Begin에서 바로
// - AtCharacter / AtText: 그 글자가 화면에 나올 때 (출력을 건너뛰어도 TypingDone에서 남은 것을 낸다)
// - TypingDone: 출력이 다 끝났을 때
// - OnNext: 다음으로 넘길 때. 이 줄에 묶인 반복음(Loop Until: LineEnd)도 이때 멈춘다
// 반복음 중 Loop Until이 CutEnd인 것은 Begin에 넘긴 longScope(컷, 맵에서는 대화 한 번)에 묶인다.
public class LineSoundPlayer
{
    private readonly StoryAudio audio;
    private readonly UnityEngine.Object logContext;
    private readonly string logName;

    private readonly List<(LineSound sound, int at)> pending = new List<(LineSound, int)>();
    private StoryLine line;
    private object longScope;
    private bool typingDone;

    public LineSoundPlayer(StoryAudio audio, UnityEngine.Object logContext, string logName)
    {
        this.audio = audio;
        this.logContext = logContext;
        this.logName = logName;
    }

    // 지금 효과음을 맞추고 있는 줄. 아직 없거나 Next로 끝났으면 null
    public StoryLine Line => line;

    // 줄 시작. findText: 지금 화면 대사에서 단어가 시작하는 글자 위치(0부터, 없으면 -1)
    public void Begin(StoryLine newLine, object scope, Func<string, int> findText)
    {
        line = newLine;
        longScope = scope;
        typingDone = false;
        pending.Clear();
        if (line == null) return;

        foreach (var sound in line.Sounds)
        {
            if (sound == null) continue;
            switch (sound.timing)
            {
                case LineSoundTiming.LineStart:
                    Play(sound);
                    break;
                case LineSoundTiming.AtCharacter:
                    pending.Add((sound, Mathf.Max(sound.atCharacter, 1)));
                    break;
                case LineSoundTiming.AtText:
                    int index = findText != null ? findText(sound.atText) : -1;
                    if (index < 0)
                        Debug.LogWarning($"[{logName}] 효과음 단어 '{sound.atText}'를 대사에서 찾지 못해 대사 시작에 냅니다.", logContext);
                    pending.Add((sound, index + 1));
                    break;
            }
        }
    }

    // 지금 화면에 나온 글자 수만큼 기다리던 글자·단어 효과음을 낸다
    public void Tick(int visibleCharacters)
    {
        if (line == null) return;
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            if (pending[i].at > visibleCharacters) continue;
            Play(pending[i].sound);
            pending.RemoveAt(i);
        }
    }

    // 출력이 다 끝났을 때 (한 줄에 한 번만 낸다)
    public void TypingDone()
    {
        if (line == null || typingDone) return;
        typingDone = true;
        Tick(int.MaxValue); // 출력을 건너뛰었어도 남은 효과음은 낸다
        PlayTiming(LineSoundTiming.TypingDone);
    }

    // 다음으로 넘길 때. 이 줄에 묶인 반복음을 멈추고 줄을 끝낸다
    public void Next()
    {
        if (line == null) return;
        TypingDone();
        PlayTiming(LineSoundTiming.OnNext);
        audio.StopLoops(line);
        line = null;
        pending.Clear();
    }

    private void PlayTiming(LineSoundTiming timing)
    {
        foreach (var sound in line.Sounds)
            if (sound != null && sound.timing == timing)
                Play(sound);
    }

    private void Play(LineSound sound)
    {
        if (!audio) return;
        object scope = sound.loopUntil == SoundLoopUntil.LineEnd ? line : longScope;
        audio.PlaySfx(sound.clip, sound.volume, sound.delay, sound.loop, scope);
    }
}
