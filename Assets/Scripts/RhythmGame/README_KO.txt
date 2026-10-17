[리듬게임 스크립트 구성]

RhythmGameController.cs
- 리듬게임의 최상위 연결 담당입니다.
- A, S, D, J, K, L 입력을 판정 시스템에 전달합니다.
- 시계, 노트, 점수, 플레이필드, HUD, 일시정지 메뉴를 초기화합니다.

RhythmPlayField.cs
- 플레이필드, 레일, 판정선, 배경 도시, 카메라와 레인 발광을 담당합니다.
- 편집 모드에서도 고정 오브젝트를 Generated Stage 자식으로 생성합니다.
- Rhythm Game Prototype 오브젝트를 선택한 뒤 Inspector의
  Generate / Refresh Stage 버튼을 누르고 Ctrl+S로 씬을 저장하면 됩니다.

RhythmGameUI.cs
- 점수, 콤보, 박자, 판정 문구와 A/S/D/J/K/L 키 안내를 표시합니다.

PauseMenu.cs
- 플레이 중 ESC를 누르면 게임 시계와 Time.timeScale을 멈춥니다.
- ESC를 다시 누르거나 '게임으로 돌아가기' 버튼을 누르면
  '다시 시작까지'와 3, 2, 1을 표시한 뒤 게임을 재개합니다.

RhythmClock.cs
- 음악 시간, BPM, 재생 배율과 일시정지 상태를 관리합니다.

NoteManager.cs
- 채보 또는 랜덤 데이터에 맞춰 노트를 생성하고 이동·제거합니다.

JudgementScoreManager.cs
- 입력 시각을 노트 시각과 비교하고 판정, 점수, 콤보를 계산합니다.

ChartData.cs / ChartAutoLoader.cs
- 음악과 노트 데이터를 보관하고, 같은 이름의 외부 채보가 있으면 불러옵니다.

[처음 확인하는 순서]

1. Assets/Scenes/RhythmScene.unity를 엽니다.
2. Hierarchy에서 Rhythm Game Prototype을 선택합니다.
3. Rhythm Play Field 컴포넌트 아래 Generate / Refresh Stage를 누릅니다.
4. Hierarchy에 Generated Stage가 생긴 것을 확인하고 Ctrl+S로 저장합니다.
5. Play를 눌러 A/S/D/J/K/L 입력과 ESC 일시정지를 확인합니다.
