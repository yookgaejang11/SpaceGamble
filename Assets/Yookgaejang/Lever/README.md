# SpaceGamble Pull Lever — Animator Ready

손잡이와 레버 축 전체가 힌지 회전축을 중심으로 함께 움직이도록 구성한 콘솔 레버입니다. 정적 베이스는 고정하고, Unity Animator에서는 **`LeverPivot`만** 회전시키세요.

## Animator 계층

```text
BaseRoot
├── BaseLower / BaseHousing / TopInset / InsetStripe
├── HingeCheek_L / HingeCheek_R
├── PivotAxle / PivotCap_L / PivotCap_R
└── LeverPivot                       ← 힌지 중심, local X 회전 애니메이션
    └── LeverAssembly                ← 움직이는 메시를 묶는 빈 그룹
        ├── LeverStem
        ├── StemLowerCollar / StemUpperCollar
        ├── RedGripCore / RedGripEnd_L / RedGripEnd_R
        └── GripEndMark_L / GripEndMark_R
```

`LeverAssembly`는 손잡이 부품들을 한곳에 모으는 identity 변환 그룹이며, 개별 메시를 따로 애니메이션할 필요가 없습니다. `LeverPivot`은 힌지 축 중심에 놓여 있습니다. 기준 포즈는 local X 약 **−4°**, 당긴 포즈는 약 **+52°**입니다. 당길 때 레버 끝이 **+Z 방향**으로 이동합니다.

## Unity 6 사용

- Unity에서 애니메이션을 만들 때는 `SpaceGamble_Lever.fbx`를 `Assets`에 가져와 사용하세요. GLB 임포터를 이미 쓰는 프로젝트라면 `SpaceGamble_Lever.glb`도 사용할 수 있습니다.
- FBX 프리팹에서 `LeverPivot`을 선택해 Animation 창에서 Local Rotation X를 −4°에서 +52°로 키프레임합니다. `LeverAssembly`는 애니메이션하지 않습니다.
- `SpaceGambleLeverAnimator.cs`는 선택형 코드 방식입니다. Animator 클립을 사용할 경우에는 이 스크립트와 동시에 같은 피벗을 제어하지 마세요.
- OBJ는 메시 검사/호환용입니다. OBJ는 계층 피벗을 보존하지 않으므로 애니메이션에는 FBX 또는 GLB를 사용하세요.

## 파일과 크기

- `SpaceGamble_Lever.fbx` — Unity Animator용, 피벗과 부품 계층 포함
- `SpaceGamble_Lever.glb` — 메시와 계층을 담은 원본 glTF 바이너리
- `SpaceGamble_Lever.obj` + `SpaceGamble_Lever.mtl` — OBJ 재질 짝; 같은 폴더에 둬야 합니다
- `SpaceGamble_Lever_preview.png` — 기존 외관 프리뷰
- `SpaceGambleLeverAnimator.cs` — 선택형 `Pull()` / `ReturnToRest()` 보조 스크립트
- `build_lever.py`, `export_lever_fbx.py` — 메시와 포맷 생성 소스

크기는 약 **30.6 × 22.0 cm 베이스**, 총 높이 약 **46.9 cm**이며 단위는 미터, Y-up, 손잡이 축은 X입니다. 콜라이더, 입력, 손 애니메이션, NGO 상태 동기화는 Unity 프로젝트에서 별도로 추가하세요.
