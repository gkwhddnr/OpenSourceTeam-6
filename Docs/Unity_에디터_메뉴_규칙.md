# Unity 에디터 메뉴 규칙

팀에서 새로 만드는 Unity 에디터 기능은 상단 `OpenSourceTeam6` 메뉴 아래에 배치한다.
최상위 메뉴를 새로 만들지 않고 기능 영역별 하위 메뉴를 사용한다.

```csharp
[UnityEditor.MenuItem("OpenSourceTeam6/기능영역/기능 이름")]
```

예: `OpenSourceTeam6/Movement/Create Initial Scenes`. Unity 및 외부 패키지가 제공하는
기존 메뉴는 해당 패키지의 메뉴이며 팀 자체 메뉴와 구분한다. 에디터 전용 코드는
Editor 전용 어셈블리에 둔다. 에셋 생성 메뉴 역시 `OpenSourceTeam6` 아래로 묶는다.
