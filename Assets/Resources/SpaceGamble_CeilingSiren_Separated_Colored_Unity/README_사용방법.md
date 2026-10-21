# SpaceGamble Ceiling Siren — 메쉬 분리 및 색상 적용

원본의 연결 부품을 찾아 **7개의 독립 메쉬**로 분리하고, **3개의 별도 PBR 재질**을 적용했습니다. 원본의 형태·크기·UV·면 구성을 보존했습니다. 표면을 잘라 분리하거나 모델을 새로 생성하지 않았습니다.

## 파일

- `SpaceGamble_CeilingSiren_Separated.fbx`: Unity용, 독립 메쉬와 재질 슬롯 포함.
- `SpaceGamble_CeilingSiren_Separated.glb`: 색상/PBR 재질이 내장된 모델. 별도 텍스처 불필요.
- `SpaceGamble_CeilingSiren_Separated.blend`: 직접 편집할 수 있는 Blender 원본. 모델은 조립된 상태이며 미리보기 카메라/조명이 포함되어 있습니다.
- `CeilingSiren_Colored_Preview.png`: 색상 적용 후 조립 상태.
- `CeilingSiren_Separated_Preview.png`: 실제 메쉬를 이동시켜 보여주는 분리 확인용 이미지. FBX/GLB/BLEND는 이 분해 상태가 아닌 조립 상태로 저장되어 있습니다.
- `Mesh_Manifest.json`: 부품별 메쉬·삼각형 수와 재질 목록.
- `Unity/Assets/SpaceGambleCeilingSiren/`: Unity에서 재질과 프리팹을 구성하는 보조 파일 및 FBX.

## 부품 구조

```text
Siren_Root
├── Dome_Red                      빨간 돔
├── Base_Black                    검은 원형 베이스
└── GuardFrame_Silver              프레임 그룹 / 메쉬가 아닌 부모 오브젝트
    ├── GuardRing_Upper            은색 상단 가로 링
    ├── GuardRing_Lower            은색 하단 가로 링
    ├── GuardArch_X                은색 보호 아치 1
    ├── GuardArch_Y                은색 보호 아치 2
    └── GuardMountRing_Silver      은색 하단 장착 링
```

돔과 베이스는 각각 따로 선택·이동·재질 변경할 수 있습니다. 보호 프레임은 그룹 전체를 이동하거나 하위 5개 메쉬를 개별 편집할 수 있습니다. 베이스의 원래 검은 보조 면은 `Base_Black` 메쉬에 함께 보존했습니다.

## 색상과 재질

| 재질 | 색상 (sRGB) | Metallic | Roughness |
| --- | --- | ---: | ---: |
| `M_Dome_Red` | 빨강 `#D91524` | 0.15 | 0.22 |
| `M_Guard_Silver` | 은색 `#B8C4CF` | 0.85 | 0.28 |
| `M_Base_Black` | 짙은 검정 `#171C24` | 0.35 | 0.38 |

재질은 불투명입니다. 기본 발광·점멸·회전 애니메이션은 추가하지 않았습니다. 돔의 색을 바꾸거나 발광을 주려면 `M_Dome_Red`만 수정하면 됩니다. 엔진의 조명과 톤매핑에 따라 미리보기와 색감이 달라질 수 있습니다.

## Unity 6에서 사용

1. 압축 내 `Unity/Assets/SpaceGambleCeilingSiren` 폴더를 프로젝트의 `Assets` 안에 복사합니다.
2. 임포트가 완료되면 **Tools > SpaceGamble > Setup Ceiling Siren** 메뉴를 실행합니다.
3. `Materials`에 재질 3개가 생성되고, FBX의 같은 이름 재질 슬롯에 연결됩니다.
4. `Prefabs/SpaceGamble_CeilingSiren.prefab`을 씬에 배치합니다.

보조 스크립트는 프로젝트의 렌더 파이프라인을 확인해 URP/Lit, HDRP/Lit 또는 Standard 셰이더를 선택합니다. 자동으로 실행되지 않으며, 같은 경로에 이미 있는 재질·프리팹은 덮어쓰지 않습니다. Unity Editor에서의 실제 실행 테스트는 하지 않았으므로, 프로젝트별 셰이더 설정에 따라 수동 조정이 필요할 수 있습니다.

보조 스크립트 없이 FBX만 사용한다면 재질 3개를 만들고 위 표의 색상과 속성값을 적용한 후 각 슬롯에 연결하세요. Unity의 Smoothness는 `1 − Roughness`이므로 돔 `0.78`, 프레임 `0.72`, 베이스 `0.62`입니다.

## 방향과 크기

원본과 동일하게 베이스가 아래에 있고 돔이 위로 향하는 조립 방향입니다. 천장에 달 때는 `Siren_Root` 또는 프리팹 전체를 가로축으로 **180° 회전**하세요.

GLB/Blender 기준 단위는 미터이며 전체 크기는 약 **가로 0.195 m × 깊이 0.195 m × 높이 0.200 m**입니다. FBX는 단위 정보를 포함해 내보냈습니다. Unity에서 임포트 후 Transform의 스케일과 실제 크기를 확인하세요.
