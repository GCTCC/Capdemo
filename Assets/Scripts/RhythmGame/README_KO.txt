[리듬게임 스크립트 구성]

RhythmGameController.cs
- 리듬게임의 최상위 연결 담당입니다.
- A, S, D, J, K, L 입력을 판정 시스템에 전달합니다.
- 로비, 시계, 노트, 점수, 플레이필드, HUD, 일시정지 메뉴를 연결합니다.

SongLobbyMenu.cs
- Resources/RhythmSongs에 음악이 하나 이상 있으면 Play 직후 곡 선택 화면을 표시합니다.
- 동명 JSON이 있는 곡은 '채보 있음', 없는 곡은 '랜덤 채보'로 표시합니다.
- 음악이 전혀 없으면 처음 실행할 때는 로비를 생략하고 무음 랜덤 모드를 시작합니다.

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
- '나가기 / 곡 선택으로' 버튼을 누르면 현재 음악과 노트를 정리하고 로비로 돌아갑니다.

RhythmClock.cs
- 음악 시간, BPM, 재생 배율과 일시정지 상태를 관리합니다.

NoteManager.cs
- 채보 또는 랜덤 데이터에 맞춰 탭·롱노트를 생성하고 이동·제거합니다.
- 롱노트 시작 판정 후 머리를 판정선에 고정하고 끝점까지 몸통을 표시합니다.

JudgementScoreManager.cs
- 입력 시각을 노트 시각과 비교하고 판정, 점수, 콤보를 계산합니다.
- 롱노트 시작점과 키를 떼는 끝점을 각각 판정합니다.

ChartData.cs / ChartAutoLoader.cs
- 음악과 노트 데이터를 보관하고, 같은 이름의 외부 채보가 있으면 불러옵니다.
- 기존 중립 JSON과 GitHub NoteEditor의 LPB/num/block/type 형식을 모두 읽습니다.

[음악 및 NoteEditor 채보 넣기]

1. Assets/Resources/RhythmSongs 폴더에 OGG와 JSON을 넣습니다.
2. 확장자를 제외한 두 파일의 이름을 완전히 같게 맞춥니다.
   예: MoonsideHowl.ogg / MoonsideHowl.json
3. NoteEditor 채보는 maxBlock=6, block=0~5, type=1(탭), type=2(롱노트)를 사용합니다.
4. Play를 누르면 곡 선택 로비가 열립니다.
5. JSON이 없는 음악을 선택하면 해당 음악과 랜덤 채보가 재생됩니다.

[NoteEditor offset 단위]

- NoteEditor의 offset은 밀리초가 아니라 오디오 샘플 개수입니다.
- 게임에서는 JSON offset / 선택한 AudioClip.frequency로 초 단위 값을 계산합니다.
- 예: 44,100Hz 음원에서 offset 7000은 약 0.1587초입니다.
- 양수 offset은 음악 자체를 늦추는 값이 아니라 노트의 판정 시각을 뒤로 밉니다.
- NoteEditor에서 사용한 WAV와 게임에 넣는 OGG의 샘플레이트를 같게 유지하는 것이 좋습니다.
- WAV가 48,000Hz라면 OGG도 48,000Hz로 내보내야 합니다.

[NoteEditor 작업 순서]

1. NoteEditor에서 게임에 사용할 원본 WAV를 엽니다.
2. BPM, Offset, 6라인 채보를 작성한 뒤 JSON으로 저장합니다.
3. 원본 WAV와 같은 샘플레이트로 OGG를 변환합니다.
4. OGG와 JSON의 확장자를 제외한 이름을 완전히 같게 만듭니다.
5. 두 파일을 Assets/Resources/RhythmSongs에 넣습니다.
6. Unity에서 OGG Import Settings의 Load Type은 Decompress On Load,
   Preload Audio Data는 켬, Load In Background는 끔으로 설정하는 것을 권장합니다.
7. Play 후 로비에서 곡을 선택합니다.

- NoteEditor JSON의 offset은 수정하거나 부호를 반대로 바꾸지 않습니다.
- 프로젝트는 NoteEditor와 동일하게 AudioSource.timeSamples를 기준으로 판정합니다.
- 테스트 환경에서 JSON offset 7000보다 14000이 맞았던 차이인 7000샘플을
  Unity 전용 판정 보정값 145.833ms로 별도 적용합니다.
- 따라서 NoteEditor JSON은 원래 값 7000으로 저장하고 Unity용으로 14000으로 바꾸지 않습니다.
- 다른 PC에서 조정해야 한다면 Rhythm Game Prototype 오브젝트의 Rhythm Clock 컴포넌트에서
  Note Timing Calibration Milliseconds만 변경합니다. 양수는 노트를 늦추고 음수는 앞당깁니다.

[음악과 노트 시작 동기화]

- RhythmClock은 AudioSource.PlayScheduled를 사용해 음악과 게임 시계를 같은 DSP 시각에 시작합니다.
- 압축 음원이 아직 로딩 중이면 AudioDataLoadState.Loaded가 될 때까지 기다린 뒤 예약합니다.
- 예약 시각 전에는 노트 생성, 이동, 판정을 진행하지 않습니다.
- OGG 디코딩이나 오디오 버퍼 준비 때문에 노트만 먼저 시작되는 현상을 방지합니다.
- 예약 시작 후에는 NoteEditor처럼 AudioSource.timeSamples / AudioClip.frequency를 곡 시간으로 사용합니다.

[처음 확인하는 순서]

1. Assets/Scenes/RhythmScene.unity를 엽니다.
2. Hierarchy에서 Rhythm Game Prototype을 선택합니다.
3. Rhythm Play Field 컴포넌트 아래 Generate / Refresh Stage를 누릅니다.
4. Hierarchy에 Generated Stage가 생긴 것을 확인하고 Ctrl+S로 저장합니다.
5. RhythmSongs에 음악을 넣고 Play를 눌러 곡 선택 로비를 확인합니다.
6. A/S/D/J/K/L 탭·롱노트 입력과 ESC 일시정지를 확인합니다.
