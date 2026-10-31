# 직원 음성 명령 데모

1. `Scenes/EmployeeVoiceDemo.unity`를 연다.
2. Hierarchy에서 `Employee Voice Demo`를 선택한다.
3. Inspector에 이 폴더의 `Assets/ggml-small-q5_1.bin`과 `Assets/qwen2.5-1.5b-instruct-q4_k_m.gguf` 경로가 설정되어 있다. 다른 모델을 쓰려면 절대 경로 또는 프로젝트 루트 기준 상대 경로로 변경한다.
4. Play 후 Game 화면에서 `Record command`를 눌러 말하고 `Stop recording`을 누른다. 녹음 최대 길이에 도달하면 자동으로 종료된다. 텍스트를 입력하고 `Send text command`로 판단 기능만 확인할 수도 있다.
5. Console에서 `[EmployeeVoice] Recognized`와 `action` 메시지를 확인한다. Inspector에서 직원 이름, 성격, 스트레스, 피로도, 충성도를 조절할 수 있다.

모델 로딩 중이거나 파일, 마이크, LLM이 준비되지 않으면 상태 메시지를 표시한다. 현재 행동은 Console 출력까지만 구현되어 있으며 직원 모델은 플레이 중 생성되는 임시 캡슐이다. 현재 경로는 Unity 에디터 테스트용이며, 모델이 빌드에 자동으로 포함되지는 않는다. 배포용 모델 경로, 모델별 라이선스 및 대상 플랫폼 설정은 별도로 준비해야 한다.
