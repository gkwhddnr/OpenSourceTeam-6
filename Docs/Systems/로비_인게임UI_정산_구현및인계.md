# 로비 · 인게임 UI 및 일차 정산 구현 및 인계 문서

- **프로젝트**: OpenSourceTeam-6
- **담당**: 송재명
- **작성일**: 2026-11-02 (2026-11-06 1차 통합 데모 규격 및 기능정의서 반영 갱신)
- **기준 기획서**: [로비·인게임 UI 및 정산 시스템 기획서](로비_인게임UI_정산_시스템기획서.md), [1차 통합 데모 규격서](1차_데모_통합규격서.md)
- **기준 개발서**: [로비·인게임 UI 및 정산 시스템 개발서](로비_인게임UI_정산_시스템개발서.md)

---

## 1. 실행 방법

Unity `6000.6.4f1`에서 상단 메뉴 **`OpenSourceTeam6/UI/Create Initial UI Scene And Prefab`**을 1회 실행해 최신 규격 프리팹·씬을 갱신한 뒤 다음 개인 검증 씬을 열고 **Play**한다.

- **개인 검증 씬**: `Assets/_Project/SongJaeMyeong/Scenes/SJM_UI_Work.unity`
- **공용 연결용 프리팹**:
  - 화면 UI 루트: `Assets/_Project/SongJaeMyeong/Prefabs/PF_StoreUIRoot.prefab`
  - NPC 머리 위 월드 UI: `Assets/_Project/SongJaeMyeong/Prefabs/PF_EmployeeWorldStatusUI.prefab`
- **초기 재생성 메뉴**: 상단 `OpenSourceTeam6/UI/Create Initial UI Scene And Prefab`

### 단독 실행 확인 방법 (`SJM_UI_Work.unity`)
1. Play 시작 시 **로비 화면**(`[게임 시작]`, `[환경 설정]`, `[게임 종료]`)이 표시된다 (`게임 시작` 클릭 시 1일차 `120초` 데모 영업 HUD 진입).
2. 인게임 화면에서 **`V` 키**로 음성 녹음 시작/종료를 토글할 수 있고, **`Tab` 키**(또는 상단 버튼)를 누르면 **보조 텍스트 업무 지시창**(`계산`, `진열`, `휴식·대기`)이 열리며, **`Esc` 키**로 **일시정지 메뉴 및 환경 설정(볼륨·마우스 감도·마이크 장치 확인)**을 열 수 있다.
3. 지시 전달 시 스트레스 구간(`0~39` 안정 / `40~79` 불만 / `80~100` 분노) 확률 규칙(`EmotionReactionRules`)에 따라 `정상 수락 / 불만 수락 / 농땡이(태만) / 정면 거부`가 판정되며, 하단 배너와 **3D 알바 머리 위 말풍선(`EmployeeWorldStatusPresenter`)**에 반응 대사가 출력되고 알바가 해당 구역 마커(`카운터`/`진열대`/`휴게석`)로 이동한다.
4. `UI_PrototypeDemo` 오브젝트가 제공하는 시연·검증 단축키:
   - `V`: 음성 녹음(`듣는 중`) 시작/종료 토글 → 확률 기반 AI 반응 시뮬레이션
   - `F1`: 계산 지시 음성 시뮬레이션 즉시 실행
   - `F2`: **① 단순 인식 실패 / 미지원 명령** 배너 확인
   - `F3`: **② 감정 악화(분노) 지시 거부** 배너·대사·말풍선 및 거부 횟수 누적 확인
   - `F4`: **③ 시스템 · 이동 경로 오류** 배너 및 대기 복구 표시 확인
   - `F5`: 스트레스 `+25` 가산 (`안정` 초록 → `피로·불만` 노랑 → `분노·임계` 빨강 게이지 전환 확인)
   - `F6`: 즉시 일차 영업 종료 및 **일차 정산 결과 화면** 진입
5. 공용 통합 씬(`Team_Integration.unity`)에 반영할 때는 `PF_StoreUIRoot.prefab`(및 알바 머리 위 `PF_EmployeeWorldStatusUI.prefab`)만 배치하며 `UI_PrototypeDemo` 오브젝트는 포함하지 않는다.

---

## 2. 팀원용 공개 API와 연결 순서

다른 담당자 시스템은 `OpenSourceTeam6.UI.Core`와(필요 시) `OpenSourceTeam6.UI.Unity` 어셈블리를 참조하고, `StoreUiSessionController`를 `IUISystemPresenter`로 주입받아 사용한다. 모든 호출은 **Unity 메인 스레드**에서 수행한다.

| API (`IUISystemPresenter` / `UI.Core`) | 용도와 주의사항 |
| :--- | :--- |
| `StartDay(day, startWon, durationSec)` | 지정 일차와 초기 자금·영업 시간으로 세션을 초기화하고 인게임 HUD(목표 금액 포함)로 전환 |
| `UpdateEmployeeHud(EmployeeHudSnapshot)` | 직원 이름·성격·현재 업무·이동 배지(`StaffMoveBadge`) 및 `스트레스·피로도·충성도`(`0~100`) 갱신 (3D 머리 위 상태 UI 동시 갱신) |
| `PushVoiceStage(stage, transcript)` | `Idle` / `Listening` / `Processing`(점 애니메이션 표시) / `Completed` 단계와 인식 문장 표시 |
| `PushCommandFeedback(VoiceCommandFeedback)` | 수락 또는 3종 실패 배너와 **알바 반응 대사(`EmployeeDialogue`)** 및 3D 머리 위 말풍선 출력 |
| `RecordWorkSample(role, stress, dt)` | 매 프레임(또는 작업 틱) 수행 시간과 현재 스트레스를 전달해 감정 효율 반영 매출 누적 |
| `RecordAngerRefusal()` | 분노 상태로 인한 의도적 명령 거부 발생 횟수 `+1` 기록 |
| `CompleteDayAndShowSettlement(endingStress)` | 당일 정산을 **1회만 확정**하고 정산 결과 화면으로 전환 |
| `VoiceRecordToggleRequested` | 인게임 `V` 키 입력 시 마이크 녹음 시작/종료 토글 요청 발행 |
| `TextCommandSubmitted` / `CancelWorkRequested` | 보조 텍스트 명령창에서 명령 전달 또는 휴식·취소 클릭 시 발행 (`OnDisable`에서 구독 해제 필수) |
| `EmotionReactionRules.ComputeWeights(...)` | 스트레스 구간·성격·피로도·LLM 제안(`1.25x`)을 반영한 `AcceptNormal / AcceptComplain / SlackOff / RefuseEmotion` 확률 가중치 산출 |

### 하종욱 담당 (`Camera & Movement`) 연동 시 주의사항
- 공용 씬 연결 시 `PlayerRigController.InputGate`(`IPlayerInputGate`)를 `ICursorLockGate` 어댑터 컴포넌트로 감싸 `StoreUiSessionController`의 `externalCursorLockGateComponent`(또는 `SetExternalCursorLockGate`)에 연결한다.
- 외부 게이트가 주입되면 내부 `StandaloneCursorLockGate`는 `Cursor.lockState` / `Cursor.visible`을 직접 변경하지 않으며, 커서 제어 소유권은 전적으로 `PlayerInputGate`가 유지한다.
- 알바 프리팹 머리 위에 `PF_EmployeeWorldStatusUI.prefab`을 연결하고 `StoreUiSessionController.SetWorldStatusPresenter(...)`로 참조를 주입한다.

### 이승재 담당 (`Whisper & LLMUnity FSM`) 연동 시 주의사항
- `VoiceRecordToggleRequested` 이벤트를 구독해 `V` 키로 마이크 녹음을 시작/종료하고, 백그라운드 추론 중에는 `PushVoiceStage(VoiceProcessStage.Processing, ...)`를 호출해 지연 상태를 알린다.
- 메인 스레드 복귀 후 최종 판정 결과와 알바 반응 대사를 `VoiceCommandFeedback(..., employeeDialogue)`로 전달한다.
- 보조 텍스트 명령 경로는 `TextCommandSubmitted` 이벤트를 구독해 음성 명령과 동일한 행동 판별 파이프라인으로 전달한다.

---

## 3. 확인 결과

- **컴파일 검증**: `OpenSourceTeam6.UI.Core`(`noEngineReferences=true`), `UI.Unity`, `UI.Editor`, `UI.EditorTests` 총 4개 어셈블리 컴파일 오류 `0`건 확인.
- **EditMode 단위 테스트 (`10/10 통과`)**:
  - 스트레스 구간(`0~39`, `40~79`, `80~100`) 및 효율 계수(`1.0` / `0.8` / `0.5`) 검증
  - 기본(`180초`) 및 **1차 데모 프리셋(`120초`, 최소 진열 `15초`)** 목표 달성·패널티·분노 누적 등급 검증
  - **`EmotionReactionRules` 스트레스 구간별 반응 확률 및 `REST` 휴식 지시 100% 수락 보장 검증**
  - **편의점 한국어 명령어 의도 분류(`Checkout`, `Restock`, `Rest`, `Clarify`, `Unsupported`) 검증**
  - `SettleOnce` 중복 지급 방지, 비유한 값 거절, 중첩 잠금(`NestedLockCounter`) 안전성 검증

---

## 4. 직접 수정하지 않는 영역 및 후속 범위

- `Assets/_Project/SongJaeMyeong/Scripts/UI/` 내부 구현 및 프리팹 수정이 필요하면 담당자 송재명에게 Issue와 Label로 요청한다.
- 다수 직원(n명) 카드 확장, 다중 일차(`2~7일`) 발주·운영 소비, 인트로/일차별 스토리 대화창·최종 엔딩 연출 및 사운드 에셋 적용은 1차 통합 데모 확인 이후 후속 작업으로 확장한다.
