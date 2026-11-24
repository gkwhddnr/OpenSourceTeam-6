# 판매 물품 Layer·Tag 및 정산 연동 정리

담당 하종욱 · 2026-11-23

## 현재 프로젝트 상태

- 판매 상품 Layer는 `SellableItem`(8번)이며 7개 분류 Tag를 등록했다. 기본 `Water` Layer는 음료 상품 분류가 아니다.
- NPC 테스트 상품 `Assets/_Project/HaJongUk/Scenes/HJU_CustomerNpc_Test/ProductBox.prefab`은 임시 모형이다. 실제 판매 상품의 가격이나 정산 데이터는 포함하지 않는다.
- 손님 트리의 `ProductCategory`는 아래 7개 분류를 갖고, 품목 이름·진열 위치·시각 프리팹을 설정한다. 현재 선택 결과를 외부 정산 시스템으로 전달하는 공개 계약은 구현되지 않았다.
- `Assets/SellableItems/01_CurrentlySold`의 54개 프리팹에 분류별 Tag와 판매 상품 Layer를 할당했다. 테스트 씬에는 대표 품목 7개를 연결했다.

## 분류와 대표 품목

Layer는 `SellableItem`이며 아래 Tag는 판매 중인 프리팹의 루트에 할당했다.

| 분류 | 손님 트리 분류 | 추천 Tag | 선택 후보로 등록한 대표 품목 | 단가 |
| --- | --- | --- | --- | --- |
| 음료 | Drink | Item_Drink | P_RSG_WaterBottle | 미정 |
| 스낵·베이커리 | Snack | Item_Snack | Chocolate_Bar_1 | 미정 |
| 라면·컵면 | Ramen | Item_Ramen | SM_Cook_PhoBowl_Full_01 | 미정 |
| 생필품·조미료 | Daily | Item_Daily | SM_Cook_NapkinBox_01 | 미정 |
| 아이스크림 | IceCream | Item_IceCream | Vanilla_Ice_Cream_1 | 미정 |
| 즉석식품 | FreshFood | Item_FreshFood | Hambuger | 미정 |
| 담배 | Cigarette | Item_Cigarette | CigPack | 미정 |

## 정산 연동 시 협의

품목마다 고유 상품 ID와 단가를 가격표에서 관리하고, Tag는 카테고리 식별에 사용한다. 같은 Tag의 상품도 가격이 다를 수 있다. 위 프리팹 이름은 현재 테스트 후보 이름이며 확정된 상품 ID가 아니다. 가격, 품목별 ID, 품절·수량과 원가의 처리 기준은 팀에서 정한다.

손님이 선택한 품목과 수량을 결제 시스템에 전달하고, 결제 성공이 확정된 거래만 한 번 매출에 반영하는 계약을 담당자와 연결한다. 현재 상품 선택은 NPC 내부 테스트 상태이며, 실제 재고 감소·가격 조회·매출 반영은 아직 연동되지 않았다.

## 참고

- 송재명 팀원의 「편의점 판매 물품 에셋 정리표」의 7개 카테고리와 대표 프리팹.
- `Assets/_Project/HaJongUk/Scripts/Customers/NpcPopulationController.cs`
- `ProjectSettings/TagManager.asset`
