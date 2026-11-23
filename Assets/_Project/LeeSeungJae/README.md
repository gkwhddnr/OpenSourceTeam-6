# 직원 음성 명령 및 대화

개인 작업 씬은 `Scenes/IntegratedConvenienceStore.unity`다. 팀원의 UI, 시간, 직원 이동, 손님과 재고 시스템을 원래 위치에서 참조한다.

- 게임 중 `R`을 누르고 말한 뒤 떼면 녹음이 종료된다. `V`는 녹음 시작·종료 토글이다.
- 로컬 Whisper 모델 `Assets/ggml-small-q5_1.bin`이 한국어 음성을 텍스트로 변환한다. 녹음 파일은 별도로 저장하지 않는다.
- Unity는 인식한 텍스트와 직원 상태를 로컬 중계서버에 보낸다. 중계서버가 이 정보를 외부 OpenAI API에 보내 업무·상품 구역·말투·직원 반응 제안을 받는다. 음성 파일과 Whisper 모델은 개발 PC에 머문다. 직원은 한 명이므로 이름으로 발화를 거절하지 않는다.
- Unity는 말투와 직원 성격에 따른 스트레스 및 팀 확률표로 최종 업무 반응을 선택한다. 일반 대화는 진행 중인 업무를 유지한다.
- 업무 수락, 거부 또는 일반 대화의 확정된 상황을 기반으로 별도 API 요청에서 짧은 직원 대사를 생성한다. 대사 요청에 실패하면 기본 대사를 사용한다.
- 명확한 취소는 즉시 실행하고 원문도 분류에 전달한다. `Tab`의 텍스트 입력에서도 같은 처리가 가능하다.
- 진열 목적지 도착 시 연결된 재고 원장의 해당 분류에서 재고가 가장 적은 품목을 3개 보충한다. 청소와 캐셔는 임시 목적지 이동 단계다.

## 로컬 중계서버 실행

프로젝트 루트에서 아래 PowerShell 명령을 실행한다. 숨김 입력한 키는 현재 터미널과 그 터미널에서 시작하는 서버에만 설정된다. 서버에는 .NET 10 SDK 또는 .NET 10 대상 팩과 ASP.NET Core 10 런타임을 갖춘 상위 SDK가 필요하다.

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
