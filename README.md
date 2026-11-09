# SPACE GAMBLE

Unity 6으로 만든 **1대1 온라인 심리전 게임**입니다.
인디언 포커 구조 — 상대의 숫자는 보이지만 내 숫자는 보이지 않습니다.

| | |
|---|---|
| **장르** | 1대1 심리전 / 갬블 |
| **플랫폼** | PC (Windows) |
| **인원** | 2인 온라인 |
| **1판 길이** | 약 3분 |
| **엔진** | Unity 6000.0.58f1 (URP) |

---

## 게임 소개

매 라운드 두 사람에게 **1~10 중 하나**가 주어집니다.
**상대의 숫자는 보이고 내 숫자는 `??`로 가려집니다.**

숫자가 낮은 쪽이 사출 버튼을 누릅니다. 버튼은 고장 나 있어서 작동할 확률이 매번 다르고, **그 확률은 라운드를 넘어 계속 쌓입니다.**

사고가 한 번이라도 나면 그 사람이 사출되고 게임이 끝납니다.

내 숫자를 알 방법은 하나뿐입니다 — **상대를 관찰하는 것.**
1인칭 고정 시점이라 상대의 시선과 목소리가 거의 유일한 단서입니다.

---

## 규칙

### 라운드 흐름

| 순서 | 단계 | 내용 |
|---|---|---|
| ① | 우선순위 배분 | 각자 1~10 중 하나 |
| ② | 교섭 | 대화하며 선택 결정 |
| ③ | 공개 | 선택과 숫자가 동시에 공개 |
| ④ | 사출 시도 | 낮은 쪽이 버튼을 1회 |
| ⑤ | 판정 | 생존 시 확률 변동 → ①로 / 사고 시 종료 |

### 선택지

교섭 시간 동안 셋 중 하나를 고릅니다. **시간이 끝나는 순간의 선택이 확정**되며, 그 전까지는 몇 번이든 바꿀 수 있습니다.

| 선택 | 효과 | 제한 |
|---|---|---|
| **GO** | 그대로 진행 | 무제한 |
| **FOLD** | 라운드 무효화. 아무도 누르지 않음 | **1판에 2회** |
| **OVERLOAD** | 이 라운드 사출 확률에 배율 (양쪽 모두 적용) | 무제한 |

* **FOLD**의 잔여 횟수는 본인만 볼 수 있습니다. 그리고 FOLD로 끝난 라운드는 양쪽 숫자를 공개하지 않습니다 — 블러핑이었는지 끝까지 알 수 없게 하기 위해서입니다.
* **OVERLOAD**는 블랙잭의 더블 다운에서 가져왔습니다. 내가 높다고 확신할 때 상대를 FOLD로 몰아내는 압박 수단이지만, **배율은 누가 걸었든 양쪽에 적용**되므로 판단이 틀리면 내가 올린 확률로 내가 누릅니다.

### 사출 확률

확률은 양쪽 모두에게 공개되며, 버튼을 누르고 살아남으면 변동합니다.

세부 수치(시작 확률, 증감 분포, 교섭 시간, OVERLOAD 배율)는 `GamePer` ScriptableObject에 분리되어 있어 **빌드 없이 조정**할 수 있습니다.

---

## 조작

| 입력 | 동작 |
|---|---|
| **마우스** | 시선 회전 |
| **좌클릭** | 조준한 대상 누르기 |
| **F4** | 준비 / 준비 취소 |
| **ESC** | 설정 메뉴 |
| **C** | 참가 코드 복사 (대기실) |

마우스 커서는 화면 중앙에 고정됩니다. 버튼은 가리키는 게 아니라 **고개를 돌려 조준해서** 누릅니다.

선택지를 고르려면 콘솔을 내려다봐야 하고, 사출 버튼을 누르려면 그 버튼을 쳐다봐야 합니다. 시선은 동기화되므로 **그 움직임이 상대에게 그대로 보입니다.** 조작 방식 자체가 심리전의 일부가 되도록 의도한 설계입니다.

---

## 실행 방법

### itch.io 다운로드 및 실행 방법 (플레이어용)

> **필수 사항:** 2명의 플레이어 모두 **인터넷 연결**이 필요합니다.

1. **다운로드:** [itch.io](https://itch.io) 페이지에서 게임 압축 파일(`.zip`)을 다운로드합니다.
2. **압축 해제:** 다운로드한 `.zip` 파일의 압축을 원하시는 폴더에 해제합니다.
3. **게임 실행:** 압축이 해제된 폴더에서 **`Space_Gamble.exe`** 파일을 실행합니다.
4. **방 생성 및 입장:**
   * **플레이어 A (호스트):** 메인 화면에서 **[방 만들기]**를 클릭하여 방을 생성한 후, 화면의 참가 코드를 복사(`C` 키)하여 플레이어 B에게 전달합니다.
   * **플레이어 B (참가자):** 메인 화면에서 **[참가하기]**를 누르고 전달받은 코드를 입력하여 입장합니다.
5. **준비 및 시작:** 두 플레이어 모두 로비에서 **`F4`** 키를 눌러 준비(Ready)를 완료하면 게임이 시작됩니다.

---

### 프로젝트 열기 (개발자용)

* **권장 엔진 버전:** `Unity 6000.0.58f1` 이상

온라인 멀티플레이 기능을 정상 작동시키려면 Unity Cloud Dashboard에서 프로젝트를 연결하고 다음 3가지 유니티 서비스를 활성화해야 합니다. (설정이 없어도 프로젝트는 정상적으로 열리고 컴파일됩니다.)

| 서비스 | 용도 |
|---|---|
| **Relay** | 코드 기반 방 생성 / 참가 네트워크 릴레이 |
| **Authentication** | 익명 사용자 로그인 |
| **Vivox** | 1대1 음성 채팅 (Voice Chat) |

---

## 개발 정보

| | |
|---|---|
| **개발** | 1인 (기획 · 프로그래밍 · 연출 · UI) |
| **기간** | 2026.09 ~ |
| **담당** | 전체 |

멀티플레이 네트워크 구조 설계, 게임 로직, 연출, UI 연동, 셰이더 등 AI를 활용하여 자체 제작했습니다.

---
---

# ENGLISH VERSION

# SPACE GAMBLE

A 1v1 online psychological gamble game built with **Unity 6**.
Inspired by Indian Poker mechanics — you can see your opponent's number, but your own number is hidden.

| | |
|---|---|
| **Genre** | 1v1 Psychological / Gamble |
| **Platform** | PC (Windows) |
| **Players** | 2 Players Online |
| **Match Duration** | Approx. 3 minutes |
| **Engine** | Unity 6000.0.58f1 (URP) |

---

## About The Game

Every round, both players receive a number from **1 to 10**.
**You can see your opponent's number, but your own number is masked as `??`.**

The player with the lower number must press their ejection button. The emergency button is malfunctioning, meaning its activation probability fluctuates each round — and **that probability accumulates continuously across rounds.**

If a malfunction leads to an ejection, that player is eliminated and the game ends immediately.

There is only one way to figure out your number — **Observe your opponent.**
With a fixed first-person perspective, your opponent's gaze and voice are virtually your only clues.

---

## Rules

### Round Flow

| Order | Phase | Details |
|---|---|---|
| ① | Priority Allocation | Each player receives a number (1–10) |
| ② | Negotiation | Discuss and select your action |
| ③ | Reveal | Choices and numbers are revealed simultaneously |
| ④ | Ejection Attempt | The player with the lower number presses their button once |
| ⑤ | Resolution | If survived, probability changes → Return to ① / If ejected, Game Over |

### Action Choices

During the Negotiation Phase, players choose one of three actions. **The choice selected when time expires is locked in**, but it can be changed freely until then.

| Action | Effect | Limit |
|---|---|---|
| **GO** | Proceed with the round normally | Unlimited |
| **FOLD** | Nullifies the round; nobody presses the button | **2 times per match** |
| **OVERLOAD** | Multiplies the ejection probability multiplier for the round | Unlimited |

* **FOLD** remaining uses are private to each player. Rounds ended by a FOLD do not reveal the numbers, keeping your opponent guessing whether it was a bluff.
* **OVERLOAD** is inspired by Blackjack's Double Down. It serves as a high-pressure tactic to force a FOLD when you are confident in your number. However, **the multiplier applies to both players**, meaning if your judgment is wrong, you will press the button with elevated risk.

---

## Controls

| Key | Action |
|---|---|
| **Mouse** | Look around (Head rotation) |
| **Left Click** | Interact with targeted object |
| **F4** | Ready / Unready |
| **ESC** | Settings Menu |
| **C** | Copy Room Code (Lobby) |

The mouse cursor is locked to the center of the screen. You interact with buttons by **looking directly at them**, not by clicking with a free cursor.

To choose an action, you must look down at your console; to press the ejection button, you must look up at it. Since your head movements are synchronized in real-time, **your opponent can read where you are looking.** The control scheme itself is an intentional component of the psychological game.

---

## How to Play (Download & Setup)

### Download via itch.io (For Players)

> **Requirement:** Both players must have an active **Internet Connection**.

1. **Download:** Download the game archive (`.zip`) from the [itch.io](https://itch.io) page.
2. **Extract:** Extract the downloaded `.zip` file into any folder of your choice.
3. **Run the Game:** Open the extracted folder and run **`Space_Gamble.exe`**.
4. **Host or Join a Room:**
   * **Player A (Host):** Click **[Create Room]** on the main menu, then press `C` to copy the generated room code and send it to Player B.
   * **Player B (Client):** Click **[Join Room]** on the main menu, enter the received code, and join.
5. **Ready & Play:** Once both players press **`F4`** to set their status to Ready in the lobby, the game begins.

---

### Opening the Project (For Developers)

* **Engine Requirement:** `Unity 6000.0.58f1` or higher

To utilize full online features, connect your project to the Unity Cloud Dashboard and enable the following three services:

| Service | Purpose |
|---|---|
| **Relay** | Code-based room creation and relay networking |
| **Authentication** | Anonymous user sign-in |
| **Vivox** | In-game 1v1 Voice Chat |

---

## Development

| | |
|---|---|
| **Developer** | Solo (Game Design, Programming, Visuals, UI) |
| **Timeline** | Sept 2026 ~ Present |

Developed independently utilizing AI assistance for network architecture, game mechanics, presentation sync, UI integration, and shaders.
