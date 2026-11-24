# 직원 음성 명령 및 대화

개인 작업 씬은 `Scenes/IntegratedConvenienceStore.unity`다. 팀원의 UI, 시간, 직원 이동, 손님과 재고 시스템을 원래 위치에서 참조한다.

- 게임 중 `R`을 누르고 말한 뒤 떼면 녹음이 종료된다. `V`는 녹음 시작·종료 토글이다.
- 로컬 Whisper 모델 `Assets/ggml-small-q5_1.bin`이 한국어 음성을 텍스트로 변환한다. 녹음 파일은 별도로 저장하지 않는다.
- Unity는 인식한 텍스트와 직원 상태를 로컬 중계서버에 보낸다. 중계서버가 이 정보를 외부 OpenAI API에 보내 업무·상품 구역·말투·직원 반응 제안을 받는다. 음성 파일과 Whisper 모델은 개발 PC에 머문다. 직원은 한 명이므로 이름으로 발화를 거절하지 않는다.
- Unity는 말투와 직원 성격에 따른 스트레스 및 팀 확률표로 최종 업무 반응을 선택한다. 일반 대화는 진행 중인 업무를 유지한다.
- 업무 수락, 거부 또는 일반 대화의 확정된 상황을 기반으로 별도 API 요청에서 짧은 직원 대사를 생성한다. 대사 요청에 실패하면 기본 대사를 사용한다.
- 명확한 취소는 즉시 실행하고 원문도 분류에 전달한다. `Tab`의 텍스트 입력에서도 같은 처리가 가능하다.
- 진열 목적지 도착 시 연결된 재고 원장의 해당 분류에서 재고가 가장 적은 품목을 3개 보충한다. 청소와 캐셔는 임시 목적지 이동 단계다.
- `쉬어`, `휴식해`, `그만하고 쉬어`, `저기 가서 쉬어` 등 명확한 휴식 요청은 현재 작업을 중지하고 현재 위치에서 대기한다. 별도 휴식 장소 이동이나 즉시 회복은 적용하지 않는다. 부정·후속 업무가 섞인 말은 즉시 휴식으로 실행하지 않는다.

## 이력서 심사 연결 상태

Gateway의 `evaluate-persona` 호출과 응답 검증, 심사 프로필 준비 및 업무 중 피로 배율 적용 코드는 개인 작업 폴더에 준비되어 있다. **현재 팀 이력서 UI의 채용 버튼에는 심사 전 비동기 연결점이 없어 아직 이 기능을 호출하지 않는다.** 기존 채용은 계속 기본 프로필로 진행한다.

팀 UI 연결에 필요한 공개 계약:

1. `PersonaSetupPanelPresenter`가 이름과 성격의 원본 입력 및 현재 `EmployeePersonaSetupData`를 전달하는 비동기 심사 연결점을 제공한다. 빈 입력을 기본값으로 치환하기 전에 검사하고, 심사 중 중복 클릭을 막는다. 입력 변경·패널 종료 시 취소한다.
2. 개인 코드에서 통합 씬의 `VoiceMovementIntegration.EvaluatePersonaAsync(name, traits, cancellation)`을 호출한다. 응답의 `Matches`로 현재 입력과 일치하는지 확인한다. 편집·닫기 시 `CancelPersonaEvaluation()`을 호출한다.
3. 성공 시 `TryPrepareEvaluatedPersona(baseline, result, out prepared, out reason)`으로 심사 시급·능력치·업무 스트레스 배율·시급 민감도를 포함한 `prepared`를 만든다. 팀 UI는 **그 데이터로** 기존 `ConfirmStartClicked`를 발생시켜야 한다. 실패하거나 입력이 바뀌면 기존 확인 이벤트를 발생시키지 않고 재시도를 안내한다. 팀 UI는 별도 asmdef이므로 개인 `Assembly-CSharp` 타입을 직접 참조하는 대신 팀 UI가 공개 이벤트/인터페이스를 제공하고 개인 코드가 구독하는 방향으로 연결한다.
4. `PersonaConfirmed`에서 준비된 결과가 이름·성격·시급과 일치하면 업무 피로 배율까지 적용한다. 팀이 제공하지 않은 필드와 기존 성격 유형은 유지한다. 성실도·게으름의 최종 행동 가중치 반영 및 시급 민감도의 실제 급여 조정 동작은 팀 규격 연결이 필요하다.
5. 기존 일일 고정 운영비 20,000원에 인건비가 포함되는지와 근무 시간은 아직 정해지지 않았다. 심사 시급을 정산 비용에 임의로 더하지 않는다. 정산 담당자는 인건비 정의·중복 차감 방지와 일일 표시·실제 차감 연결점을 확정해야 한다.

## 로컬 중계서버 실행

Windows에서 `OPENAI_API_KEY` 환경 변수를 설정했다면 파일 탐색기에서 [StartServer.bat](Server~/StartServer.bat)를 더블클릭한다. 게임을 테스트하는 동안 실행 창을 열어 두고, 마치면 그 창에서 `Ctrl+C`를 누른다.

PowerShell에서 직접 시작하려면 프로젝트 루트에서 아래 명령을 실행한다. 숨김 입력한 키는 현재 터미널과 그 터미널에서 시작하는 서버에만 설정된다. 서버에는 .NET 10 SDK 또는 .NET 10 대상 팩과 ASP.NET Core 10 런타임을 갖춘 상위 SDK가 필요하다.

```powershell
Set-Location 'Assets/_Project/LeeSeungJae/Server~'
$keyInput = Read-Host '서버용 OpenAI API 키' -AsSecureString
$env:OPENAI_API_KEY = [System.Net.NetworkCredential]::new('', $keyInput).Password
$keyInput.Dispose()
dotnet run --project Gateway/Gateway.csproj
```

이미 서버 터미널에 환경 변수가 있다면 키 입력 단계는 생략한다. 다른 터미널에서 `Invoke-RestMethod http://127.0.0.1:5127/health`의 `status`가 `ok`인지 확인한다. 종료는 서버 터미널에서 `Ctrl+C`다. 키를 변경하면 새 환경 값을 읽는 터미널에서 서버를 다시 시작한다.

Unity Editor와 Windows 게임은 같은 주소를 사용한다. 주소 변경은 Play 또는 빌드 전에 통합 씬의 **Voice Integration → Voice Movement Integration → Gateway Base Url**에서 한다. 기본값은 `http://127.0.0.1:5127`이다. 다른 PC에서 이 주소는 해당 PC 자신을 가리킨다.

게임 UI에는 인식문과 직원 대사가 표시된다. Console은 기본적으로 단계별 시간, 요청 ID, 선택된 행동과 대사 출처를 기록한다. `Enable Voice Diagnostics`를 켜면 원문과 대사가 Unity 로그에 남을 수 있으며 F10·F11로 같은 녹음의 음량 보정과 단어 문맥을 비교할 수 있다. 중계서버는 발화와 대사를 기본 로그에 남기지 않는다. OpenAI 요청에는 `store: false`를 사용하며 외부 보관에는 계정의 데이터 제어 정책도 적용된다. [OpenAI 데이터 제어](https://developers.openai.com/api/docs/guides/your-data)

오프라인 테스트, 실제 마이크·Windows 빌드 확인 순서, 이전 직접 호출의 시간 기준 및 외부 배포 요건은 [중계서버 개발·검증 안내](Server~/README.md)에 있다.
