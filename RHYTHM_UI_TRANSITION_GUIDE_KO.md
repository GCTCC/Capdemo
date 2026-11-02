# 리듬게임 HUD 및 씬 전환 적용 안내

## 이번 수정의 디자인 기준

- 첨부 UI의 먹색 배경, 아이보리 글자, 짙은 적색 선택 강조를 공통 색으로 사용했습니다.
- 기존 플레이필드의 청록/분홍 네온은 입력 안내와 판정 강조에만 사용해 화면이 산만해지지 않게 했습니다.
- HUD는 플레이필드를 가리지 않도록 화면 상단과 하단 가장자리에 배치했습니다.

## 플레이 화면 HUD

- 좌측 위: 진입 모드와 곡 제목
- 중앙 위: 콤보
- 우측 위: 점수와 현재 비트
- 화면 중앙: PERFECT/GREAT/GOOD/MISS 판정
- 하단: A, S, D, J, K, L 입력 상태
- 최상단: 곡 진행도
- 플레이필드: 판정선·Far Gate·양쪽 기둥은 Cyan으로 통일하고, 가운데 손 영역 구분선은 내부선과 같은 색으로 자연스럽게 표시
- 노트: 레인 경계 안쪽을 거의 채우며, 탭 노트와 롱노트 모두 끝이 둥근 형태 사용
- 생성 위치: 세로 레인과 노트 메시 모두 Far Gate를 넘지 않고 게이트 선에서 시작

`RhythmGameUI.cs`에서 색, 크기와 배치를 조정할 수 있습니다.

## 곡과 앨범커버 등록

`Assets/Resources/RhythmSongs`에 확장자를 제외한 파일 이름을 똑같이 맞춥니다.

```text
MySong.ogg
MySong.json
MySong.png
```

- JSON이 있으면 해당 채보를 사용합니다.
- JSON이 없으면 음악을 재생하면서 랜덤 채보를 사용합니다.
- PNG/JPG가 없으면 자동 생성된 `NO COVER` 화면을 사용합니다.
- 커버 이미지의 Texture Type은 `Default` 또는 `Sprite (2D and UI)`를 모두 지원합니다.

## 싱글모드 연결

현재 `HubScene`의 `SinglePlay` 카드에는 클릭 진입이 이미 연결되어 있습니다. 카드를 누르면 `RhythmScene`의 곡 선택 로비가 열립니다.

다른 씬에 버튼을 더 만들 때는 다음 순서로 연결합니다.

1. 버튼 또는 Raycast Target이 켜진 Image 오브젝트를 선택합니다.
2. `Add Component`에서 `RhythmSceneLauncher`를 추가합니다.
3. 일반 `Button`을 쓸 때는 `On Click()`에 `LaunchSingleMode()`를 연결합니다.
4. 이미지 카드 자체를 클릭 대상으로 쓸 때는 `Launch On Pointer Click`을 켭니다.
5. `Song Name`을 비워 두면 리듬 씬에서 곡 선택 로비가 열립니다.
6. 특정 곡을 바로 시작하려면 `Song Name`에 OGG의 확장자를 제외한 이름을 입력합니다.

## 스토리모드 연결

스토리에서 리듬게임으로 넘어가야 하는 이벤트에 `RhythmSceneLauncher > LaunchStoryMode()`를 연결합니다.

- `Song Name`: 스토리에서 플레이할 곡 이름
- `Return Scene Name`: 비워 두면 현재 스토리 씬으로 돌아갑니다.
- 곡 완료 또는 일시정지 메뉴의 `스토리로 돌아가기`를 누르면 복귀합니다.

현재 프로젝트의 `StoryManager`는 빌드에서 항상 첫 컷부터 시작하도록 작성되어 있습니다. 따라서 씬 복귀 자체는 작동하지만, 리듬게임 직전 대사 다음 줄부터 정확히 이어지는 기능은 스토리 진행 위치 저장 기능을 추가한 뒤 연결해야 합니다. 이 항목은 HUD/전환과 분리해 두었습니다.

## 전환 시간 조정

`RhythmScene`의 `Rhythm Game Prototype` 오브젝트에서 `RhythmSceneTransition` 값을 조정합니다.

| 항목 | 기본값 | 역할 |
|---|---:|---|
| Cover Fade In Duration | 0.55 | 앨범커버 등장 시간 |
| Cover Hold Duration | 0.90 | 앨범커버 유지 시간 |
| Cover Fade Out Duration | 0.70 | 앨범커버 퇴장 시간 |
| Play Field Fade In Duration | 1.00 | 플레이필드 등장 시간 |
| Scene Fade Duration | 0.75 | 로비/종료 전환 시간 |

전환은 `Time.unscaledDeltaTime`을 사용하므로 일시정지 상태에서도 정상적으로 진행됩니다.

## 수정된 주요 파일

- `RhythmGameUI.cs`: 플레이 HUD 디자인
- `SongLobbyMenu.cs`: 곡 선택 화면과 커버 미리보기
- `PauseMenu.cs`: 통일된 일시정지 화면
- `ChartAutoLoader.cs`: 동명 앨범커버 검색
- `RhythmSceneTransition.cs`: 커버 페이드아웃과 플레이필드 페이드인
- `RhythmLaunchContext.cs`: 싱글/스토리 진입 정보 전달
- `RhythmSceneLauncher.cs`: 다른 씬에서 리듬 씬을 여는 진입점
- `RhythmGameController.cs`: 로비, 전환, 플레이와 복귀 흐름 연결
- `RhythmClock.cs`: 곡 진행률과 종료 감지
- `RhythmPlayField.cs`: 레인 세로선을 Far Gate 앞쪽 가장자리에서 종료하고 노트 출발 기준을 Far Gate로 통일
- `NoteManager.cs`: 생성 순간 노트 메시가 Far Gate 뒤로 돌출되지 않도록 원근 메시 범위 제한
- `RhythmScene.unity`: 전환 컴포넌트 적용
- `EditorBuildSettings.asset`: Hub/Tuto/Rhythm 씬 등록

## 확인 순서

1. Unity가 스크립트 컴파일을 마칠 때까지 기다립니다.
2. `RhythmScene`에서 Play를 눌러 곡 선택 화면의 색과 커버를 확인합니다.
3. 곡을 누르고 `커버 등장 → 커버 퇴장 → 플레이필드 등장` 순서를 확인합니다.
4. 플레이 중 ESC를 눌러 일시정지 화면과 복귀 버튼을 확인합니다.
5. 싱글모드 버튼과 스토리 이벤트를 각각 연결한 뒤 왕복 전환을 확인합니다.
