# SimpleTwoBoneIK 사용법

2관절 체인(팔·다리·로봇 팔 등)을 목표 지점으로 보내는 IK.

| 항목 | 내용 |
|---|---|
| 필요 패키지 | **없음** |
| 필요 리그 | Generic / Humanoid 무관. **Blender 리그 그대로 가능** |
| 에디터 미리보기 | **지원** (Play 없이 타겟을 끌면 따라옴) |
| 계산 시점 | `LateUpdate` — 애니메이터 포즈 위에 덮어씀 |

---

## 1. 파일 배치

| 파일 | 위치 |
|---|---|
| `SimpleTwoBoneIK.cs` | 아무 폴더 |
| `IKDriver.cs` | 아무 폴더 |
| `SimpleTwoBoneIKEditor.cs` | **`Assets/Editor/`** |
| `IKSetupMenu.cs` | **`Assets/Editor/`** |

`Editor` 폴더 이름이 정확해야 합니다. 다른 이름이면 빌드에서 에러가 납니다.

---

## 2. 빠른 설정 (메뉴)

1. 씬에서 **캐릭터 선택**
2. **Tools → IK → 양팔 IK 만들기**

이 구조가 자동으로 생깁니다.

```
Character  (Animator, IKDriver)
 ├ IK_RightArm   (SimpleTwoBoneIK)
 │   ├ RightArm_Target
 │   └ RightArm_Hint
 └ IK_LeftArm
     ├ LeftArm_Target
     └ LeftArm_Hint
```

`IKDriver`의 채널도 자동으로 채워집니다.

| 메뉴 | 만드는 것 |
|---|---|
| 양팔 IK 만들기 | `rightArm`, `leftArm` |
| 양다리 IK 만들기 | `rightLeg`, `leftLeg` |
| 팔다리 전부 만들기 | 4개 전부 |
| 이 캐릭터의 IK 전부 끄기 | 미리보기 일괄 해제 |

---

## 3. 수동 설정 (체인 하나만)

1. 빈 오브젝트 생성 → **`SimpleTwoBoneIK`** 추가
2. **Preset** 선택 (RightArm / LeftArm / RightLeg / LeftLeg / Custom)
3. **`뼈 자동 찾기`** 클릭
4. **`Target / Hint 자동 생성`** 클릭

---

## 4. 뼈 자동 찾기 규칙

**끝 뼈만 찾으면 나머지는 부모를 타고 올라가서 결정됩니다.**

```
Tip  = 이름으로 찾은 뼈 (hand / wrist / foot / ankle)
Mid  = Tip의 부모
Root = Mid의 부모
```

| 체인 | 찾는 단어 |
|---|---|
| 팔 | `hand`, `wrist` |
| 다리 | `foot`, `ankle` |

| 처리 | 대상 |
|---|---|
| **제외** | `thumb` `index` `middle` `ring` `pinky` `finger` `toe` `ball` `_ik` `attach` `socket` `target` `hint` |
| **건너뜀** | 이름에 `twist` / `roll`이 든 보조 뼈 |

**좌우 판별**

1. 이름에 `right` / `left`가 있으면 그걸 씀
2. 없으면 끝에 붙은 `r` / `l`을 봄 — `hand.R`, `hand_l`, `hand.r.001` 전부 인식

### 못 찾을 때

| 상황 | 해결 |
|---|---|
| 뼈 이름이 특이함 (`palm`, `손` 등) | Preset을 **Custom**으로 두고 3칸 직접 지정 |
| 엉뚱한 뼈를 찾음 | **Search Root**를 캐릭터 루트로 좁히기 |
| 좌우가 바뀜 | 캐릭터 기준 좌우입니다. 마주 보면 화면상 반대 |

---

## 5. 미리보기

1. **`미리보기 켜기`**
2. 씬에서 **노란 Target 핸들**을 드래그 → 체인이 실시간으로 따라옴
3. 위치가 정해지면 **`미리보기 끄기`**

> **중요** — 미리보기 중에는 실제 뼈 포즈가 씬에 저장됩니다.
> `미리보기 켜기`를 누른 순간의 포즈를 기억해두고, 끄면 복원합니다.
> 깜빡하고 켠 채로 저장해도 게임을 실행하면 `IKDriver.Awake()`가 꺼줍니다.

씬 뷰 색 구분:

| 색 | 의미 |
|---|---|
| 하늘색 선 | 뼈 체인 |
| **노랑** | Target |
| **분홍** | Hint |

---

## 6. Hint (중간 관절 방향)

어깨와 손 위치가 정해져도 **팔꿈치는 답이 하나가 아닙니다.** 팔을 뻗은 채 팔꿈치만 빙 돌려보면 원을 그립니다.

Hint는 "팔꿈치는 이쪽을 향해라"를 알려줍니다. 없으면 팔이 뒤집히거나 몸을 뚫습니다.

- `Target / Hint 자동 생성`이 **지금 꺾여 있는 방향**으로 계산해서 놓습니다
- 대부분 그대로 쓰면 됩니다
- 팔꿈치가 이상하면 Hint를 반대편으로 옮기세요
- 위치가 정확할 필요는 없습니다. **방향만 맞으면 됩니다**

---

## 7. 켜고 끄기 (IKDriver)

Weight를 1로 두면 **가만히 있을 때도 손이 목표에 붙어 있습니다.** 필요한 구간에만 켜야 합니다.

### 애니메이션 이벤트

클립에 이벤트를 찍고:

| 칸 | 값 |
|---|---|
| Function | `IKOn` 또는 `IKOff` |
| **String** | 채널 id (`rightArm` 등) |

> `IKDriver`는 **Animator가 붙은 바로 그 오브젝트**에 있어야 합니다.
> 애니메이션 이벤트는 같은 오브젝트의 함수만 부를 수 있습니다.

예시:

| 클립 | 프레임 | Function | String |
|---|---|---|---|
| `butten_push` | 손 뻗기 시작 | `IKOn` | `rightArm` |
| `butten_push` | 손 떼기 시작 | `IKOff` | `rightArm` |
| `lever_pull` | 레버 잡는 순간 | `IKOn` | `leftArm` |
| `lever_pull` | 놓는 순간 | `IKOff` | `leftArm` |

### 코드

```csharp
driver.IKOn("rightArm");        // 부드럽게 1로
driver.IKOff("rightArm");       // 부드럽게 0으로
driver.AllOff();                // 전부 끄기 (리셋용)

driver.SetWeightImmediate("rightArm", 1f);   // 블렌드 없이 즉시
float w = driver.GetWeight("rightArm");
```

`blendTime`(기본 0.15초)이 0→1에 걸리는 시간입니다. 짧을수록 딱 붙는 느낌입니다.

---

## 8. 값 설명

| 항목 | 의미 |
|---|---|
| **Weight** | 0 = 애니메이션 그대로 / 1 = 목표에 완전히 붙음 |
| **Rotation Weight** | 끝(손·발)의 **회전**을 목표에 맞출지. 손바닥 방향이 중요하면 1 |
| **Hint Weight** | 중간 관절을 Hint 쪽으로 얼마나 비틀지 |
| **Search Root** | 뼈를 찾기 시작할 지점. 비우면 최상위 부모부터 |

---

## 9. 자주 겪는 문제

| 증상 | 원인 / 해결 |
|---|---|
| 아무 변화가 없음 | **Weight가 0**입니다. 1로 올리세요 |
| 에디터에서 안 움직임 | **미리보기 켜기**를 누르세요. Constraint와 달리 Play 없이도 됩니다 |
| 팔이 뒤집힘 / 몸을 뚫음 | **Hint 방향**입니다. 반대편으로 옮기세요 |
| 팔이 쭉 펴진 채 떨림 | 목표가 **닿는 거리 밖**입니다. 더 가까이 |
| 손은 가는데 손목이 꺾임 | `Rotation Weight`를 낮추거나 Target의 **회전**을 맞추세요 |
| 애니메이션이 아예 안 보임 | Weight가 계속 1입니다. `IKOff`가 안 불리는지 확인 |
| 재시작하면 포즈가 이상함 | `AllOff()`를 리셋 지점에서 부르세요 |
| 이벤트가 안 불림 | `IKDriver`가 **Animator와 같은 오브젝트**에 있는지 확인 |

---

## 10. 다른 곳에 쓰기

2관절이면 뭐든 됩니다. Preset을 **Custom**으로 두고 뼈 3개만 지정하세요.

| 쓸 수 있는 예 | Root → Mid → Tip |
|---|---|
| 로봇 팔 | 기둥 → 관절 → 집게 |
| 문 여는 손 | 윗팔 → 아래팔 → 손 |
| 사다리 오르기 | 양팔 + 양다리 4개 동시 |
| 지면에 발 붙이기 | 허벅지 → 정강이 → 발 (Target을 바닥 레이캐스트 지점으로) |

**3관절 이상(척추, 꼬리 등)은 못 합니다.** 이 알고리즘은 삼각형 하나를 푸는 방식이라 2관절 전용입니다.

---

## 11. 동작 원리

```
1. 코사인 법칙으로 중간 관절 각도를 구해 굽힌다
     두 뼈 길이는 고정, 목표까지 거리만 바뀜
2. 뿌리를 돌려 끝을 목표로 보낸다
3. 뿌리를 목표 축으로 비틀어 중간 관절을 Hint 쪽으로 돌린다
4. 끝의 회전을 목표에 맞춘다
```

반복 계산이 아니라 **한 번에 푸는 해석적 방식**이라 가볍습니다. 체인 수십 개를 돌려도 부담이 없습니다.
