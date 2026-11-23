# 직원 AI 중계서버 개발·검증 안내

## 로컬에서 실행하기

이 문서의 명령은 `Assets/_Project/LeeSeungJae/Server~` 디렉터리를 기준으로 한다. 준비된 환경은 Unity 6000.6.4f1, Windows, .NET SDK 11.0.100, ASP.NET Core 10.0.15다. 서버는 `net10.0`을 대상으로 하며 별도 NuGet 패키지 없이 설치된 대상 팩으로 빌드한다. 설치 상태는 `dotnet --list-sdks`와 `dotnet --list-runtimes`로 확인한다. [Microsoft .NET 지원 정책](https://dotnet.microsoft.com/en-us/platform/support/policy)

1. 개발 PC의 별도 PowerShell에 `OPENAI_API_KEY` 환경 변수를 설정한다. 현재 터미널에만 키를 숨김 입력하는 방법은 [Unity README](../README.md)에 있다. Windows 사용자 환경 변수를 사용하는 경우에는 변경 후 새 터미널에서 서버를 재시작한다. 서버는 프로세스 환경에서 키를 읽는다.
2. `dotnet run --project Gateway/Gateway.csproj`로 시작한다. 다른 터미널에서 `Invoke-RestMethod http://127.0.0.1:5127/health`를 실행하면 `status`가 `ok`로 나온다. 이는 서버 프로세스 확인용이며 키가 없어도 성공한다.
3. 포트가 이미 사용 중이면 `dotnet run --project Gateway/Gateway.csproj -- --Relay:Port=5128`처럼 빈 포트를 지정한다. Unity 통합 씬의 **Voice Integration → Voice Movement Integration → Gateway Base Url**도 `http://127.0.0.1:5128`로 맞춘 뒤 Play 또는 빌드를 시작한다. 기본 바인딩은 IPv4 루프백이고, 클라이언트는 로컬 HTTP와 외부 HTTPS 주소를 지원한다. Windows Editor와 Standalone은 관리형 `HttpClient`를 사용한다.
4. 종료는 서버 터미널에서 `Ctrl+C`다. 키나 모델 권한이 없으면 아래 자동 검증까지 진행할 수 있다. 실제 게임의 분류와 대사는 OpenAI API 사용료가 발생할 수 있다.

## 요청과 행동의 순서

녹음 종료 → 로컬 Whisper → 인식문과 직원 상태 스냅숏 → 중계서버 분류 → 서버·Unity 양쪽의 조합 검사 → Unity 스트레스 반영 및 최종 행동 선택 → 이동·업무 처리 → 확정 결과로 대사 요청 → 직원 UI에 대사 표시.

서버는 Unity가 보낸 텍스트와 상태를 외부 OpenAI Responses API로 보낸다. 시스템 지시와 예시는 서버가 관리하고, 발화와 성격은 입력 JSON에 담는다. 음성과 Whisper 모델은 로컬에서 처리한다. 요청에는 `store: false`를 지정한다. 외부 사업자의 남용 모니터링 로그와 계정별 보관 설정은 별도로 적용될 수 있다. [OpenAI 데이터 제어](https://developers.openai.com/api/docs/guides/your-data)

Unity가 한 발화에 대한 스트레스를 반영하고 기존 성격·스트레스 구간별 확률을 사용한다. 제안된 반응의 가중치 배수는 1.25다. `MISTAKE` 가중치는 0이다. 일반 대화 중에는 기존 이동이 계속된다. 진열을 수락하고 목적지에 도착하면 해당 구역에서 재고가 가장 적은 품목 3개를 보충한다. 청소와 캐셔는 임시 목적지까지 이동한다.

명확한 취소는 Unity가 즉시 적용한 뒤 같은 원문을 분류해 말투를 반영한다. 새 명령·화면 전환·비활성화는 진행 중인 요청을 취소하며 세대 번호와 이동 ID로 오래된 응답을 걸러낸다. 서버는 연결 종료·서버 종료 토큰을 OpenAI 요청에 연결한다. 이미 시작된 외부 처리와 과금은 계속될 수 있다. 자동 재시도는 하지 않는다. 대사 요청 실패 시 현재 행동과 이동은 계속되고 기존 기본 대사를 표시한다.

## HTTP 계약과 제한

`GET /health` 이외의 두 경로는 `Content-Type: application/json`으로 POST한다. 정상 응답에 요청 ID를 돌려준다. 잘못된 JSON 등에서 클라이언트 ID를 읽을 수 없으면 서버가 새 추적 ID를 만든다.

| 경로 | 요청 | 성공 응답 |
|---|---|---|
| `/api/v1/employee/classify` | `requestId`, `utterance`, `employee` | `requestId`, `intent`, `target`, `tone`, `reaction` |
| `/api/v1/employee/dialogue` | 같은 필드와 `decision` | `requestId`, `dialogue` |

분류 요청 예시는 다음과 같다.

```json
{
  "requestId": "f62a3acc-6cb8-4465-bf11-bc79d50e3858",
  "utterance": "라면 채워",
  "employee": {
    "personality": "조심스러운 직원",
    "stress": 35, "fatigue": 20, "loyalty": 70,
    "currentWork": "Idle", "isMoving": false
  }
}
```

대사 요청은 새 ID와 행동 확정 시점의 `employee`를 보내며, 예를 들어 `"decision": {"intent":"RESTOCK","target":"RAMEN","tone":"NEUTRAL","reaction":"REFUSE_EMOTION"}`을 추가한다. 이때 `reaction`은 Unity가 최종 선택한 값이다. `TALK`일 때도 `currentWork`와 `isMoving`을 보내 대화 중인 직원이 무엇을 계속하는지 전달한다.

| 의도 | 허용 대상 | 허용 반응 |
|---|---|---|
| `RESTOCK` | `SNACK`, `RAMEN`, `DAILY_GOODS`, `ICE_CREAM`, `WALK_IN`, `HOT_FOOD` 중 하나 | `ACCEPT_NORMAL`, `ACCEPT_COMPLAIN`, `SLACK_OFF`, `REFUSE_EMOTION` |
| `CLEAN`, `CASHIER` | `DEFAULT` | 진열과 같은 네 반응 |
| `MOVE` | `CLICKED` | `NONE` |
| `CANCEL`, `TALK`, `CLARIFY`, `UNSUPPORTED` | `NONE` | `NONE` |

말투는 `NEUTRAL`, `PRAISE`, `APOLOGY`, `URGENT`, `INSULT` 중 하나다. 상태 수치는 0~100 정수, `currentWork`는 팀의 `Idle`, `Checkout`, `Restock`, `Other`, `isMoving`은 불리언이다. `requestId`는 하이픈이 있는 비어 있지 않은 GUID다. 스키마에 없는 모델·프롬프트 필드는 거부한다.

| 설정 위치 | 기본값 | 근거 |
|---|---|---|
| `Gateway/Contracts.cs`, Unity `EmployeeAiService.cs` | 발화 512자, 성격 256자, 대사 120자 | 짧은 음성 지시와 UI 말풍선을 수용한다. 공백뿐인 값과 제어문자를 거부한다. 대사에는 한글이 필요하다. |
| `Gateway/Contracts.cs`, `OpenAiModel.cs`, Unity `EmployeeAiService.cs` | 클라이언트 요청 16 KiB, 외부 응답 64 KiB, Unity 수신 8 KiB | 이스케이프된 한글 JSON과 API 응답 봉투를 수용하면서 메모리 사용을 제한한다. |
| `Gateway/appsettings.json` | 분류 160토큰·15초, 대사 100토큰·6초 | 이전 직접 호출과 같은 출력 및 대기 예산을 사용한다. |
| Unity `EmployeeAiService.cs` | 분류 20초, 대사 10초 | 서버 타임아웃 응답을 수신할 여유를 둔다. 서버 예산을 늘릴 때 클라이언트 기한도 확인한다. |

모델은 서버의 `OpenAI:ClassificationModel`과 `OpenAI:DialogueModel`로 각각 정한다. 환경 변수로는 `OpenAI__ClassificationModel`과 `OpenAI__DialogueModel`이다. 기본값 `gpt-6-luna`와 `reasoning.effort=none`은 이전 코드와 같다. 공식 문서에서 Responses와 구조화된 출력 지원을 확인했다. 계정별 접근 권한은 실제 요청으로 확인한다. [OpenAI 모델 문서](https://developers.openai.com/api/docs/models/gpt-6-luna)

오류 응답은 `{"requestId":"…","error":{"code":"…","message":"…"}}` 형식이다. `INVALID_INPUT`은 요청 값, `SERVER_CONFIG`와 `OPENAI_AUTH`는 서버 설정과 인증, `MODEL_CONFIG`는 모델·권한을 확인한다. `RATE_LIMITED`는 사용량·한도, `UPSTREAM_TIMEOUT`은 시간 초과, `UPSTREAM_UNAVAILABLE`은 일시적 장애다. `INVALID_RESPONSE`면 업무를 실행하지 않고 재입력을 안내한다. 연결 자체가 실패하면 Unity가 서버 실행 상태와 주소 확인을 안내한다. Unity는 외부 오류 본문을 화면이나 Console에 복사하지 않는다.

## 단계별 시간과 비교 기준

Unity Console에서 `timing`을 찾는다. 한 발화의 `requestId`로 Whisper, 분류 왕복, 행동 확정을 연결한다. 대사에는 별도 `requestId`와 원래 분류의 `classifyId`가 있다. 같은 요청 ID로 서버 로그를 찾으면 어느 구간에서 오래 걸렸는지 구분할 수 있다.

| Unity 측정값 | 측정 구간 |
|---|---|
| `whisper_ms` | 로컬 Whisper에 오디오를 전달한 시점부터 인식 결과까지 |
| `classify_rtt_ms` / `dialogue_rtt_ms` | 각 Unity 요청의 송신부터 수신·검증 또는 오류까지 |
| `decision_total_ms` | 텍스트 처리를 시작해 행동을 확정할 때까지. 즉시 취소는 네트워크 전에 기록한다. |
| `input_to_decision_ms` | 녹음 종료부터 행동 확정까지. Tab 입력이면 텍스트 제출부터 측정한다. |

서버는 `operation`, `status`, `code`, `requestId`와 함께 본문 읽기·입력 검증 `validation_ms`, 외부 HTTP 전송·수신 `openai_ms`, 결과 검증 및 응답 쓰기를 포함한 `total_ms`를 기록한다. 분류·대사의 Unity 왕복에는 해당 서버 시간이 이미 포함된다.

이 작업 전 프로젝트의 `Logs/Editor.log`와 `Logs/Editor-prev.log`에 남은 직접 호출 타이밍만 집계한 참고값이다. 요청별로 남은 표본 수가 달라 각 행은 독립적으로 해석한다.

| 이전 측정값 | 표본 수 | 중앙값 | 최소~최대 |
|---|---:|---:|---:|
| `classification_behavior_ms` | 25 | 1,430 ms | 961~3,548 ms |
| `decision_total_ms` | 10 | 1,407 ms | 984~2,957 ms |
| `dialogue_ms` | 17 | 1,234 ms | 978~1,634 ms |

위 두 로그에는 기존 `whisper_ms` 표본이 없었다. 비교할 때는 같은 PC·모델·텍스트 집합에서 먼저 Tab으로 분류와 대사를 측정하고, 같은 마이크 조건에서 R로 Whisper를 포함한 시간을 측정한다. 첫 요청과 반복 요청을 구분하고 업무 결정끼리 비교한다. 가짜 모델 테스트는 통신·분기 검증용이다. 실제 중계 후 지연과 외부 호스팅의 네트워크·기동 지연은 별도 측정이 필요하다.

## 테스트 순서

1. **오프라인 서버** — `dotnet run --project Tests/GatewayChecks.csproj`. 임시 루프백 포트의 실제 Kestrel에 요청하고 외부 HTTP 전송만 가짜 응답으로 대체한다. 키 누락, 명령·대사, 요청·응답 경계값, 인증·모델·한도 오류, 시간 초과, 연결 취소, 로그 비노출을 검사한다.
2. **오프라인 Unity 코드** — 프로젝트를 한 번 가져온 Windows 개발 PC에서 Python 3으로 `python -B UnityChecks/run.py`를 실행한다. 설치된 Unity의 Roslyn, 프로젝트 Bee 참조, Windows Mono 프로필을 사용한다. Editor 조건과 Windows Player 조건에서 C#을 컴파일한 뒤 실제 클라이언트가 가짜 로컬 서버와 통신하고, 같은 샘플로 확률 선택과 취소 규칙을 검사한다. Unity Hub 경로가 다르면 `UNITY_EDITOR_DATA`를 해당 버전의 `Editor/Data`로 지정한다.
3. **실제 분류** — 키가 설정된 서버를 시작하고 `/health`를 확인한다. `Scenes/IntegratedConvenienceStore.unity`에서 Play 후 영업을 시작한다. Tab으로 아래 예시를 입력하고 Console의 `명령 해석`과 HUD를 확인한다. 응답의 `reaction`은 제안이므로 특정 반응 하나를 합격 조건으로 삼지 않는다.
4. **상태·이동** — 진열을 수락하면 도착 후 대상 품목이 3개 증가하는지 확인한다. 이동 중 “고생했어”를 입력해 이동·업무가 계속되는지 확인한다. `INSULT` 또는 `PRAISE` 발화의 스트레스 반영을 한 번 확인하고 대사 도착 시 같은 증감이 반복되지 않는지 본다. 시간 경과에 따른 별도 감정 변화는 기존 규칙이다. 바닥을 클릭한 뒤 “저기로 가”도 확인한다.
5. **취소·장애** — 이동 중 서버를 끄고 “그만해”를 입력한다. 직원이 즉시 멈추고 뒤이은 분류에는 연결 오류가 표시되어야 한다. 새 명령을 연속 입력하거나 인게임 화면을 떠난 뒤 돌아와 이전 대사가 나타나지 않는지 확인한다. 대사 실패만 재현하려면 테스트 세션에서 `$env:OpenAI__DialogueModel='unavailable-test-model'`로 서버를 재시작한다. 분류에 성공한 업무가 유지되고 Console의 ‘기본’ 대사가 UI에 나오는지 본 뒤 이 환경 변수를 제거하고 재시작한다.
6. **음성·빌드** — R을 누르고 말한 뒤 떼고, V 토글도 반복한다. `whisper_ms`부터 최종 행동까지 확인한다. 같은 PC에서 통합 씬을 포함한 Windows 빌드를 실행해 먼저 Tab, 다음으로 마이크를 검증한다. 현재 빌드의 상대 Whisper 경로는 `Application.persistentDataPath/EmployeeVoiceModels/ggml-small-q5_1.bin`이다. Windows에서는 현재 회사·제품 설정 기준 `%USERPROFILE%/AppData/LocalLow/DefaultCompany/OpenSource-6/EmployeeVoiceModels/`에 로컬 모델이 필요하다. API 키는 서버에서만 설정한다.

| 발화 | 관찰할 결과 |
|---|---|
| 과자 좀 / 라면 채워 | `RESTOCK/SNACK` 또는 `RESTOCK/RAMEN` |
| 병신아 라면 채워 / 병신아 | 각각 `RESTOCK/RAMEN/INSULT`, `TALK/NONE/INSULT` |
| 고생했어 / 아까는 미안해 | `TALK`와 각각 `PRAISE`, `APOLOGY` |
| 빨리 계산해 / 청소해 | `CASHIER/DEFAULT/URGENT` 또는 `CLEAN/DEFAULT`, 수락하면 임시 목적지로 이동 |
| 진열해줘 / 과자랑 라면 채워 | 구역을 다시 확인하고 새 업무 미실행 |
| 과자 버려 / 그만하고 라면 채워 / 취소하지 마 | 폐기·혼합·부정을 새 진열이나 잘못된 취소로 실행하지 않음 |
| 저기로 가 / 그만해 | 선택한 바닥으로 이동 / 즉시 취소 |

실제 직원 이름과 발화·대사는 UI에 표시한다. 씬의 `Enable Voice Diagnostics`는 꺼져 있다. 켜면 Console과 Unity 로그 파일에 원문이 남을 수 있고 F10/F11 비교용 음성이 메모리에 유지된다. 서버는 API 키·인증 헤더·프롬프트·발화·대사를 로그에 남기지 않는다.

## 외부 배포 전

`Server~`는 Unity 6.6이 임포트에서 제외하는 이름이다. 소스·설정·테스트는 이곳에 두고, 서버·테스트용 프로젝트 파일만 상위 Unity Git 제외 규칙에서 예외 처리했다. `bin`, `obj`, 임시 산출물과 개인 비밀 설정은 제외한다. [Unity 6.6 폴더 규칙](https://docs.unity3d.com/6000.6/Documentation/Manual/SpecialFolders.html)

1. `dotnet publish Gateway/Gateway.csproj -c Release -o ./publish`로 .NET 10 게시물을 만든다. 이를 ASP.NET Core 10 런타임 환경에서 `dotnet Gateway.dll`로 실행할 수 있다. Azure Container Apps를 선택하면 게시물을 런타임 이미지에 담고, 우선 비공개 ingress에서 검증한다.
2. 현재 프로세스는 루프백에만 바인딩하고 POST의 Host도 확인한다. 배포 시에는 인증 프록시가 로컬 Host로 전달하도록 구성하거나 별도의 배포 설정에서 내부 수신 주소와 허용 Host를 함께 정의한다. Azure ingress 대상 포트를 실제 리스너와 맞추고 외부 HTTPS를 강제한다. 클라우드의 비밀 저장소에서 `OPENAI_API_KEY`를 주입하고 클라이언트 주소를 배포 HTTPS 주소로 지정한다. [Azure Container Apps ingress](https://learn.microsoft.com/en-us/azure/container-apps/ingress-overview)
3. 공개 전에 사용자·세션 인증, 사용자별·서버 전체 요청 제한, 사용량·예산 모니터링, 비정상 사용 탐지와 차단, 접근 취소·키 교체, 최소 로그 보관 정책을 구현한다. 빌드의 공통 문자열은 사용자 인증 수단으로 사용할 수 없다.
4. 실제 Windows 빌드에서 인증·오류·취소·동시 부하·재시작을 확인한 뒤 공개 여부를 결정한다. 절전이나 scale-to-zero를 쓰는 호스트는 첫 요청이 느릴 수 있다. 호스팅 비용과 OpenAI API 비용은 별도로 산정한다.
