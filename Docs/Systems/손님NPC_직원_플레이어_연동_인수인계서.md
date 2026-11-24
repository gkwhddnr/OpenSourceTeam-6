# 손님 NPC와 직원·플레이어 업무 연동 인수인계서

2026-11-23 · 전달 하종욱 · 인수 송재명 · WBS EXT-10

최근 회의에서 정한 역할 분담에 따라 하종욱은 손님 행동, 상품 선택과 선반 집기, 계산대 대기, 퇴장을 담당한다. 송재명은 직원과 플레이어의 업무, 상품 위치 안내, 포스기 결제, 재고와 매출 정산을 연결한다. 손님이 상품을 집을 때 재고를 차감하고, 포스기나 직원이 결제를 확정할 때 판매 내역과 매출을 한 번 반영한다.

## 손님 유형

| 유형 | 입장 후 행동 | 직원·플레이어가 관여하는 시점 |
| --- | --- | --- |
| 직행형 `Direct` | 집을 수 있는 상품을 정해 바로 진열대로 이동한다. | 상품을 집은 뒤 계산한다. |
| 고민형 `Deliberating` | 입구에 가까운 통로로 이동하고 2~4초 멈춘 뒤 상품을 고른다. | 상품을 집은 뒤 계산한다. |
| 선택 고민형 `Indecisive` | 매장 안쪽 통로에서 4~8초 머문 뒤 고른 상품으로 이동한다. | 상품을 집은 뒤 계산한다. |
| 미발견 시 질문형 `AskWhenMissing` | 정해진 상품의 진열 위치를 탐색한다. 찾지 못하면 직원 또는 플레이어에게 다가가 위치를 묻는다. | 위치를 안내할 수 있다. 안내 뒤 재고를 확보하면 계산한다. |
| 미발견 시 퇴장형 `LeaveWhenMissing` | 정해진 상품을 탐색하고 찾지 못하면 출구로 간다. | 상품을 찾고 집은 경우에만 계산한다. |
| 입장 후 망설임 퇴장형 `HesitateAndLeave` | 입장 지점에서 2~4초 제자리에 멈춘 뒤 빈손으로 출구로 간다. | 상품 질문과 계산 없이 퇴장한다. |

여섯 유형을 한 묶음으로 섞어 생성한다. 이동을 마친 손님은 바깥 목표 지점에서 제거되며, 다음 손님은 1~5초 사이의 무작위 간격을 거쳐 생성된다. 행인은 매장에 들어오지 않고 보도를 걷는다.

## 연결할 API

* `OpenSourceTeam6.Customers.Unity.NpcPopulationController`를 씬의 접점으로 사용한다. 별도 asmdef에서 호출한다면 `OpenSourceTeam6.Customers.Unity`를 참조한다. Unity 메인 스레드에서 조회·응답·승인을 호출하고, 씬이나 연동 컴포넌트를 내릴 때 이벤트 구독을 해제한다.

| API | 호출 또는 처리 방식 |
| --- | --- |
| `CanTakeFromShelf` · `Func<string, bool>` | 상품을 선택하거나 집기 직전에 카테고리 태그로 판매 가능 여부를 조회한다. 여러 번 호출될 수 있으므로 수량 차감, 판매 등록 등 부작용을 두지 않는다. |
| `ProductPickedUp(int customerId, string productTag)` | 집기가 성공하면 알린다. 정산 담당 시스템에서 고객 ID를 기준으로 재고 한 개 차감과 구매 대기 기록을 처리한다. 매출은 결제 확정 단계에서 기록한다. |
| `WaitForExternalCheckout` | 실제 연동 시 `true`로 켠다. 테스트 씬 기본값인 `false`에서는 `checkoutDuration`이 지나면 자동으로 계산한다. |
| `TryGetCurrentCheckout(out int customerId, out string productTag)` | 반환값이 `true`이면 손님이 계산대에 실제로 도착해 승인 대기 중이다. 반환값이 `false`이면 출력값을 결제에 사용하지 않는다. |
| `TryCompleteCurrentCheckout(out int customerId)` | 포스기나 직원이 현재 계산대 손님의 결제를 승인할 때 호출한다. 수락되면 다음 NPC 업데이트에서 퇴장과 대기열 전진을 처리한다. 같은 대기 건의 두 번째 호출이나 일시정지 중 호출은 거절한다. |
| `GetQueueSnapshot(List<CheckoutWaitSnapshot> output)` | UI에 고객 ID, 대기 시간, 위치를 표시할 때 사용한다. 위치 `0`은 계산대 자리이고 `1`부터 줄 위치다. 외부 결제 가능 여부는 `TryGetCurrentCheckout`으로 확인한다. |
| `CustomerAskedForProduct(int customerId, string productName, Transform target)` | 손님이 지정된 직원·플레이어의 질문 위치에 도착하면 안내 요청을 알린다. 질문 가능 위치가 둘이면 손님에게 가까운 위치를 사용한다. |
| `TryAnswerProductQuestion(int customerId, Transform productLocation)` | 질문한 고객 ID와 손님이 접근할 수 있는 상품 위치를 전달한다. 수락된 위치 안내 뒤에도 집기에는 재고 확보가 필요하다. 응답 기한이 지나거나 재고를 확보하지 못하면 손님이 퇴장한다. |
| `SetDay`, `SetPaused`, `SetCustomerTarget`, `SetPedestrianTarget` | 일차, 영업 일시정지, 동시 인원을 업무·시간 시스템과 연결할 때 사용한다. 테스트 패널의 슬라이더와 같은 속도 변경은 `SetCustomerSpeed`와 `SetPedestrianSpeed`로 적용한다. |

플레이어의 포스기 조작과 직원 결제는 한 승인 접점을 공유한다. 시간이 걸리는 업무라면 완료 시점에 계산대 손님 ID를 다시 조회하고 시작 시 예약한 ID와 대조한다. 조회·승인·판매 기록은 한 메인 스레드 흐름에서 처리하고, 승인 API가 반환한 ID의 판매 기록을 한 번만 확정한다. 현재 API는 카테고리 태그를 전달한다. 같은 카테고리 안에서 상품별 가격을 구분하려면 별도 SKU 계약이 필요하다. 집기 이후 씬 전환이나 인원 감소로 손님이 결제 전에 제거될 때 구매 대기 기록 취소와 실재고 복원도 정산 시스템에서 처리한다.

상품 분류 태그는 `Item_Drink`, `Item_Snack`, `Item_Ramen`, `Item_Daily`, `Item_IceCream`, `Item_FreshFood`, `Item_Cigarette`다. 상품 프리팹 루트에는 `SellableItem` 레이어를 사용한다. [판매 물품 Layer·Tag와 정산 전달서](판매물품_Layer_Tag_정산_전달서.md)

## Inspector와 테스트 패널

| 설정 | 조정 방법 |
| --- | --- |
| 일차별 인원 | `dayPopulations`에서 일차별 손님·행인 목표 수를 설정한다. 직접 설정한 일차가 없으면 직전 등록 일차의 값을 사용한다. 테스트 씬의 1·2·3일차 기본값은 손님/행인 각각 2/2, 3/4, 4/6이다. 손님 상한은 `maxPopulation`과 `queueSlots` 개수 중 작은 값이다. |
| 통로·보도 | `entrance`, `browsingWaypoints`, `exit`, `leftSidewalkZone`, `rightSidewalkZone`, `sidewalkZoneSize`로 접근 위치를 지정한다. `browsingWaypoints` 배열의 앞쪽/뒤쪽을 짧은 고민/긴 고민 유형이 사용한다. 목적지는 NavMesh에 올린다. |
| 상품 | `products`에 카테고리, 표시명, 집기 위치, 선택적 시각 프리팹, 실제 진열 오브젝트를 지정한다. `unstocked`는 후보를 품절로 처리한다. `demoMissingProductName`은 테스트에서 품절 손님 행동을 재현하는 값이다. |
| 질문 | `employeeQuestionTarget`과 `playerQuestionTarget`은 대화 대상에게 접근할 수 있는 위치다. `questionTimeout`으로 답변 대기를 정한다. 테스트 씬에는 두 질문 위치와 품절 상품 `Spicy cup ramen`이 있으며 응답은 6초를 기다린다. |
| 계산대 | `checkout`은 결제 위치, `queueSlots`는 앞자리부터 차례로 배치한다. `checkoutDuration`은 자동 계산 시간으로 테스트 씬은 4.5초다. |
| 속도·고민 시간 | 테스트 패널의 속도는 0.5~3.0m/s로 조절한다. 기본은 손님 1.5m/s, 행인 1.7m/s다. `hesitationMinSeconds`, `hesitationMaxSeconds`는 짧은 고민과 입장 후 퇴장에 적용한다. 긴 고민은 해당 범위의 두 배다. |

## 인수 검증 순서

1. `Assets/_Project/HaJongUk/Scenes/HJU_CustomerNpc_Test.unity`를 열고 Play 한다. NPC sandbox에서 일차·인원·속도를 조절한다. Hierarchy의 손님 이름 끝 유형으로 여섯 행동을 구분하고, 질문이 발생하면 패널의 Latest product question을 확인한다.
2. 자동 계산 상태에서 구매 유형이 상품을 집고 계산대로 이동하는지, 질문형과 두 퇴장 유형이 구매 없이 나갈 수 있는지 확인한다. 테스트 씬은 실제 진열 오브젝트를 잠시 손님에게 붙이고 계산 후 진열대로 돌려놓는다. 물건을 든 모습은 상품 앞에서 집은 뒤 계산을 마치기 전까지 확인할 수 있다.
3. 연동 씬에서 외부 계산 모드를 켠다. 첫 손님이 계산대에 도착할 때까지 결제할 수 없는지 확인하고, 포스기와 직원 업무에서 같은 손님을 동시에 처리해도 판매가 한 번만 기록되는지 확인한다. 결제 후 다음 손님이 이동해야 한다.
4. 질문 대상에 실제 직원 또는 플레이어 접근 위치를 연결한다. 품절 상태에서 질문과 무응답 퇴장을 확인하고, 재고가 있는 상품의 위치를 유효 시간 내 안내했을 때 집기와 계산으로 이어지는지 검증한다.

Unity 시험에서 여섯 유형별 동선과 퇴장, 질문 위치 이동, 외부 승인과 차례 전환을 확인했다. NPC가 제공하는 재고 조회·집기 이벤트·승인 API를 실제 직원 업무, 포스기, 결제와 정산에 연결한 뒤 위 순서로 통합 검증한다.
