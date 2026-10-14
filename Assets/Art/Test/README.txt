ArtTest3D - 3D 오브젝트에 아트 붙여보기용 실험 씬

구성
- Main Camera: 원근 카메라, URP Renderer 1번(UniversalRenderer3D) 사용
- Directional Light: 그림자 켜짐, 스카이박스 환경광
- Floor / Cube / Sphere: 예시 오브젝트
- ArtBoard: 배경 아트를 붙여볼 세로 판(Quad, 16:9 비율)
- 머티리얼: Materials/ 폴더 (URP Lit)

배경 아트 붙이는 법
1. 이미지를 Assets/Art/ 아래에 넣는다.
2. 이 프로젝트는 2D 모드라 이미지가 Sprite로 들어온다.
   Inspector에서 Texture Type을 "Default"로 바꾸고 Apply.
3. Materials/M_ArtBoard 를 선택하고 Base Map 칸에 이미지를 끌어다 놓는다.
   (다른 오브젝트도 같은 방식, 새 머티리얼은 Create > Material, Shader는 Universal Render Pipeline/Lit)
4. 조명 영향 없이 그림 색 그대로 보고 싶으면 Shader를 Universal Render Pipeline/Unlit 으로 바꾼다.

씬이 어둡게 보이면 Window > Rendering > Lighting 에서 Generate Lighting 을 한 번 누른다.
