# 로비 · 인게임 UI 및 일차 정산 시스템 개발서

- **프로젝트**: OpenSourceTeam-6 / 음성 지시 기반 AI 직원 편의점 운영 게임
- **담당**: 송재명 (총괄 기획 · UI · 정산 · 스토리)
- **작성일**: 2026-11-02
- **기준 기획서**: [로비·인게임 UI 및 정산 시스템 기획서](로비_인게임UI_정산_시스템기획서.md)
- **확인 환경**: Unity 6000.6.4f1, uGUI (`com.unity.ugui` 2.6.0), Input System 1.20.0, URP 17.6.0
- **대상**: PC 싱글플레이 시제품

---

## 1. 개발 결과물과 경계

첫 결과물은 독립 실행 가능한 개인 검증 씬(`SJM_UI_Work.unity`), 공용 씬에 그대로 배치 가능한 UI 루트 프리팹(`PF_StoreUIRoot.prefab`), 엔진 비의존 정산·UI 상태 계약(`OpenSourceTeam6.UI.Core`), 그리고 uGUI 연결 계층(`OpenSourceTeam6.UI.Unity`)이다.

| 자체 코드가 담당 (`Core` / `Unity`) | Unity 및 타 담당자 기능이 담당 |
| :--- | :--- |
| 일차 정산 공식, 유효 작업 시간 누적, 중복 정산 차단 (`UI.Core`) | `whisper.unity` 마이크 녹음 및 로컬 LLM 행동 판정 (`이승재`) |
| 화면 상태 계약, 3종 명령 피드백 모델, 스탯 정규화·검증 (`UI.Core`) | 1인칭 시점·보행·`PlayerInputGate` 커서 제어·`IAgentMovement` (`하종욱`) |
| 로비·HUD·보조 명령창·정산창 uGUI 렌더링 및 잠금 토큰 관리 (`UI.Unity`) | `Canvas`, `CanvasScaler`, `InputSystemUIInputModule` 이벤트 라우팅 (`Unity`) |
| 초기 UI 씬·프리팹 자동 생성 메뉴 `OpenSourceTeam6/UI/...` (`UI.Editor`) | 실제 매장 3D 배경 및 캐릭터 애니메이션 (후속 에셋 적용) |

### 초기 구현 선택과 이유
- **렌더링 방식 (`uGUI` + 내장 동적 한글 폰트)**: 1인칭 카메라 위에 겹치는 HUD 특성상 `Screen Space - Overlay` 기반 `Canvas`가 카메라 렌더링 설정과 무관하게 일정한 가독성을 보장한다. 외부 상용/OS 폰트 파일을 저장소에 무단 복제하지 않도록 기본값으로 Unity 내장 `LegacyRuntime.ttf`(OS 한글 동적 폴백 지원)를 사용하되, 추후 무료 라이선스 폰트 에셋 교체용 직렬화 필드를 열어 둔다.
- **타 시스템 비결합 경계**: 하종욱(`Movement`) 및 이승재(`Voice/AI`) 브랜치가 `develop`에 순차 병합되더라도 컴파일 깨짐 없이 독립 개발·검증할 수 있도록, `OpenSourceTeam6.UI.Core`에 최소 연동 인터페이스(`ICursorLockGate`, `IUISystemPresenter`, `ITextCommandSink`)를 둔다. 공용 통합 시 얇은 어댑터 컴포넌트로 `PlayerRigController.InputGate` 및 음성·FSM 컨트롤러에 연결한다.
- **전역 상태 배제**: 전역 `Singleton`이나 정적 `Event Bus`를 두지 않고 Inspector 직렬화 참조와 명시적 인터페이스로 조립한다.

---

## 2. 전체 구조와 참조 방향

```
외부 음성·FSM 시스템 (이승재) / 검증 드라이버
   │  (PushVoiceStage / PushCommandFeedback / UpdateEmployeeHud / RecordWorkDelta)
   ▼
StoreUiSessionController (UI.Unity 조립 책임자, IUISystemPresenter 구현)
   ├→ DaySettlementSession & DaySettlementCalculator (UI.Core 순수 규칙)
   ├→ LobbyPanelPresenter          (로비 시작·종료)
   ├→ InGameHudPresenter           (시간·자금·직원 3스탯·이동 배지·음성 지연·3종 오류 배너)
   ├→ CommandOverlayPresenter      (보조 텍스트 명령 입력 ──▶ TextCommandSubmitted / CancelRequested)
   ├→ SettlementPanelPresenter     (일차 정산 내역·결과 등급 표시)
   └→ ICursorLockGate              (공용 통합 시 PlayerRigController.InputGate 어댑터로 위임)
```

- `OpenSourceTeam6.UI.Core`는 `UnityEngine`을 참조하지 않는다(`noEngineReferences: true`).
- 각 패널 Presenter는 다른 패널을 직접 참조하지 않으며, 조립 책임자인 `StoreUiSessionController`가 화면 전환과 잠금 토큰 수명을 일괄 조율한다.

---

## 3. 폴더 · 파일 배치 및 어셈블리 구성

```
Assets/_Project/SongJaeMyeong/
├─ Scripts/
│  └─ UI/
│     ├─ Core/
│     │  ├─ OpenSourceTeam6.UI.Core.asmdef
│     │  ├─ UiContracts.cs                # 화면·음성·피드백·직원 HUD DTO 및 공개 인터페이스
│     │  └─ EconomyRules.cs               # 유효 수행 시간 누적기·일차 정산 계산기·중복 방지 세션
│     ├─ Unity/
│     │  ├─ OpenSourceTeam6.UI.Unity.asmdef
│     │  ├─ StoreUiSessionController.cs   # UI 전체 수명·타이머·공개 API 조립 컨트롤러
│     │  ├─ LobbyPanelPresenter.cs        # 로비 화면 뷰
│     │  ├─ InGameHudPresenter.cs         # 상시 인게임 HUD 뷰
│     │  ├─ CommandOverlayPresenter.cs    # 보조 텍스트 명령 입력창 뷰
│     │  ├─ SettlementPanelPresenter.cs   # 일차 정산 및 결과 화면 뷰
│     │  ├─ StandaloneCursorLockGate.cs   # 개인 씬 단독 실행용 폴백 잠금 게이트 (외부 주입 시 미사용)
│     │  └─ UiPrototypeDemoDriver.cs      # 개인 검증 씬 시뮬레이션 입력 드라이버 (통합 시 비활성화)
│     ├─ Editor/
│     │  ├─ OpenSourceTeam6.UI.Editor.asmdef
│     │  └─ UiSceneAndPrefabBuilder.cs    # OpenSourceTeam6/UI 메뉴 기반 프리팹·개인 씬 생성기
│     └─ Test/
│        └─ EditMode/
│           ├─ OpenSourceTeam6.UI.EditorTests.asmdef
│           └─ EconomyAndUiRulesTests.cs  # 정산 공식·경계값·중복 방지·잠금 규칙 단위 테스트
├─ Prefabs/
│  └─ PF_StoreUIRoot.prefab
└─ Scenes/
   └─ SJM_UI_Work.unity
```

### 어셈블리와 namespace 규칙
| 어셈블리 이름 (`rootNamespace` 동일) | 참조 어셈블리 | 플랫폼 / 조건 |
| :--- | :--- | :--- |
| `OpenSourceTeam6.UI.Core` | 없음 (`.NET` 기본 라이브러리만 사용) | 전체, `noEngineReferences: true` |
| `OpenSourceTeam6.UI.Unity` | `OpenSourceTeam6.UI.Core`, `UnityEngine.UI`, `Unity.InputSystem` | 전체 런타임 |
| `OpenSourceTeam6.UI.Editor` | `OpenSourceTeam6.UI.Core`, `OpenSourceTeam6.UI.Unity`, `UnityEngine.UI`, `Unity.InputSystem` | `includePlatforms: ["Editor"]` |
| `OpenSourceTeam6.UI.EditorTests` | `OpenSourceTeam6.UI.Core`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner` | `Editor` 전용 테스트 (`TEST_INCLUDE_TESTS`) |

---

## 4. 코드 작성 형태와 데이터 규칙

- `public`은 다른 담당자 시스템과 연결할 계약(`IUISystemPresenter`, DTO, Presenter 진입점)에만 사용하고 내부 계산 보조는 `private` / `internal`로 제한한다.
- 모든 `float` 입력(시간 `deltaSeconds`, 스트레스·피로도·충성도 수치, 설정 단가)은 `float.IsNaN` / `float.IsInfinity` 비유한 값과 음수 시간을 경계에서 검사·거절하며 조용히 잘못된 정산값을 누적하지 않는다.
- 통화 단위는 정수 `원`(`int`), 시간 단위는 `초`(`float`), 게이지 상태값은 `0f ~ 100f` 범위로 정규화한다.
- UI 조작 잠금은 `IDisposable` 토큰 단위로 획득·해제한다. 화면 전환이나 `OnDisable` 시 보유 중인 모든 잠금 토큰을 즉시 `Dispose()`해 플레이어 시점이 영구 잠기는 현상을 방지한다.
- 모든 UI 갱신 API는 **Unity 메인 스레드**에서 호출한다. 백그라운드 STT/LLM 작업 결과는 호출자(음성·AI 시스템)가 메인 스레드로 마샬링한 뒤 전달한다.

---

## 5. 외부 공개 API 계약 (`OpenSourceTeam6.UI.Core`)

```csharp
namespace OpenSourceTeam6.UI.Core
{
    public enum UiScreenMode { Lobby, InGame, Settlement }
    public enum VoiceProcessStage { Idle, Listening, Processing, Completed }
    public enum CommandFeedbackKind { Info, Accepted, RecognitionFailed, RefusedByEmotion, SystemOrMoveError }
    public enum StaffWorkRole { Idle, Checkout, Restock, Other }
    public enum StaffMoveBadge { Idle, Moving, Arrived, Failed, Cancelled }
    public enum DayOutcomeGrade { StableSuccess, UnstableAngerSuccess, FailedTargetShortfall }

    public interface ICursorLockGate
    {
        System.IDisposable AcquireLock(string reason);
        bool IsLocked { get; }
    }

    public interface IUISystemPresenter
    {
        UiScreenMode CurrentMode { get; }
        bool IsCommandOverlayOpen { get; }

        event System.Action<string> TextCommandSubmitted;
        event System.Action CancelWorkRequested;
        event System.Action DayStarted;

        void StartDay(int dayNumber, int startingBalanceWon, float durationSeconds);
        void UpdateEmployeeHud(EmployeeHudSnapshot snapshot);
        void PushVoiceStage(VoiceProcessStage stage, string recognizedTranscript);
        void PushCommandFeedback(VoiceCommandFeedback feedback);
        void RecordWorkSample(StaffWorkRole role, float stress, float deltaSeconds);
        void RecordAngerRefusal();
        DaySettlementResult CompleteDayAndShowSettlement(float endingStress);
    }
}
```

### 타 담당자 연결 가이드

1. **하종욱 담당 (`Camera & Movement`) 연결 시**:
   - `Team_Integration.unity`에서 `PlayerRigController`가 준비된 뒤, 얇은 브리지 컴포넌트가 `ICursorLockGate.AcquireLock(reason)`을 `playerRigController.InputGate.AcquireLock(reason)`으로 그대로 위임한다.
   - `StoreUiSessionController`는 외부 `ICursorLockGate`(`MonoBehaviour` 직렬화 주입)가 연결되어 있으면 내부 폴백 커서 제어를 수행하지 않고 100% `PlayerInputGate`에 커서·시점 잠금을 맡긴다.
   - 직원의 `IAgentMovement.Snapshot.State` 및 `Finished` 결과를 `StaffMoveBadge`(`Idle / Moving / Arrived / Failed / Cancelled`)로 매핑해 `UpdateEmployeeHud`에 전달한다. 이동 실패(`MoveState.Failed`) 시 `PushCommandFeedback`으로 `CommandFeedbackKind.SystemOrMoveError`를 표시한다.
2. **이승재 담당 (`Whisper & LLMUnity FSM`) 연결 시**:
   - 마이크 녹음 시작 시 `PushVoiceStage(VoiceProcessStage.Listening, "")`, 녹음 종료 및 STT·LLM 추론 시작 시 `PushVoiceStage(VoiceProcessStage.Processing, partialText)`를 호출해 지연 인디케이터를 켠다.
   - 행동 판정 완료 시:
     - 정상 명령: `PushCommandFeedback` (`Accepted`)
     - 미지원/불명확 명령: `PushCommandFeedback` (`RecognitionFailed`)
     - 감정/충성도 불량 거부: `RecordAngerRefusal()` + `PushCommandFeedback` (`RefusedByEmotion`)
     - 마이크/모델 오류: `PushCommandFeedback` (`SystemOrMoveError`)
   - 보조 텍스트 입력창과 연결할 때는 `uiPresenter.TextCommandSubmitted += OnTextCommand;`를 구독하고 자신의 `OnDisable`에서 구독을 해제한다.

---

## 6. 에디터 메뉴 규칙 준수

팀 공통 메뉴 규칙(`Unity에디터메뉴_규칙`)에 따라 최상위 메뉴를 신설하지 않고 상단 **`OpenSourceTeam6`** 아래에 하위 메뉴를 배치한다.
- `OpenSourceTeam6/UI/Create Initial UI Scene And Prefab`:
  - `Assets/_Project/SongJaeMyeong/Prefabs/PF_StoreUIRoot.prefab` 생성/갱신
  - `Assets/_Project/SongJaeMyeong/Scenes/SJM_UI_Work.unity` 개인 검증 씬 생성 (`Main Camera`, `Directional Light`, `EventSystem` + `InputSystemUIInputModule`, `PF_StoreUIRoot` 인스턴스, `UiPrototypeDemoDriver` 배치)

---

## 7. 검증 계획과 완료 기준

### 7.1 EditMode 단위 테스트 (`EconomyAndUiRulesTests`)
1. 안정 구간(`스트레스 20`)에서 충분한 계산·진열 수행 시 진열 패널티 없이 목표 잔액(`80,000원`) 이상 및 `StableSuccess` 판정 확인.
2. 진열 유효 시간이 기준(`25초`) 미만일 때 총 기본 매출 `20%` 감산 패널티 정확성 확인.
3. 감정 구간별 효율 계수(`1.0` / `0.8` / `0.5`)가 유효 수행 시간에 정확히 반영되는지 확인.
4. 목표 잔액을 달성했더라도 종료 스트레스 `≥ 80` 또는 분노 거부 `≥ 3회`일 때 `UnstableAngerSuccess`로 분기되는지 확인.
5. `DaySettlementSession`에서 정산 확정 후 재호출·추가 작업 기록 시 결과가 변하지 않는 **1회 정산 보장(중복 지급 방지)** 확인.
6. `NaN`, `Infinity`, 음수 `deltaSeconds` 입력 거절 확인.
7. 스트레스 구간(`0~39`, `40~79`, `80~100`) 등급 분류 경계값(`39f`, `40f`, `79.9f`, `80f`) 확인.

### 7.2 에디터 및 씬 실행 검증
1. `SJM_UI_Work.unity` 단독 실행 시 컴파일 오류·런타임 예외 `0`건 확인.
2. 로비 → 인게임 HUD → 보조 명령창(`Tab`) → 일차 정산 화면 전환 및 한글 UI 가독성 확인.
3. 시뮬레이션 단축키(또는 화면 테스트 버튼)로 음성 지연 표시 및 3종 오류 배너, 스탯 게이지 변화, 정산 내역 출력 확인.
