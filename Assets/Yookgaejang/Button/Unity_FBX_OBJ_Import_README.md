# SpaceGamble 버튼 — FBX / OBJ+MTL

- **Unity에서는 `SpaceGamble_GuardedLaunchButton.fbx`를 사용하세요.** 계층과 `ButtonPlungerPivot` 피벗을 보존하도록 내보냈습니다. Unity에서 FBX를 `Assets` 안으로 복사해 임포트한 뒤 `ButtonPlungerPivot`의 local Y를 0에서 약 −0.018 m로 애니메이션하세요.
- **MTL은 FBX에 연결되는 파일이 아닙니다.** Wavefront OBJ 전용 재질 파일입니다. OBJ를 쓰려면 `SpaceGamble_GuardedLaunchButton.obj`와 `SpaceGamble_GuardedLaunchButton.mtl`을 같은 폴더에 두세요. OBJ 파일은 MTL을 같은 이름으로 참조합니다.
- FBX와 MTL에는 현재 메시 재질의 색상 및 표면 파라미터가 들어 있고 별도 이미지 텍스처는 없습니다. FBX를 Unity로 가져온 뒤 재질의 URP 셰이더와 블루 Emission을 확인하세요.
- 모델의 기준 크기는 약 16 × 14.8 cm 베이스, 9.3 cm 높이입니다. 모델은 Y-up, +Z 전면을 기준으로 제작했습니다.
