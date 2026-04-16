# `Assets/Scripts` 개요

이 폴더는 **CPU 텍스처에 원(브러시)으로 칠하기**를 중심으로 한 미니 게임/프로토타입 로직을 담고 있습니다. 그린 결과를 **`ColorAreaCalculator`**가 색별 픽셀 수로 집계하고, **`RatioPanelUI`**로 비율을 표시합니다.

**칠하기 입력**은 예전에 `ColorDrawer`가 마우스/터치를 받던 방식이 **`PlayerUser`(플레이어 조작)** 로 옮겨졌고, **`SimulateUser`(AI 에이전트)** 는 `Simulator`가 스폰해 같은 캔버스에 월드 좌표로 칠합니다. `ColorDrawer` 안의 입력 처리(`HandleInput`)는 제거·주석 처리된 상태이며, 그리기는 **`PaintAtWorldPosition`** 호출로만 이뤄집니다.

## 데이터 흐름 (요약)

```
[PlayerUser / SimulateUser]  (월드 이동)
        ↓ PaintAtWorldPosition (Normal 페이즈에서만 실제 칠함)
   ColorDrawer.PaintCircle → pixelBuffer 갱신 + Texture2D 부분 업로드
        ↓ RegisterPixelColorChange (큐 적재)
   ColorAreaCalculator.FlushPendingPixelChanges (LateUpdate, Normal 페이즈만)
        ↓ 카운트 갱신 + RatioPanelUI.UpdateRatio + 패널 sibling 정렬
```

---

## `Singleton.cs`

**역할:** 제네릭 싱글톤 베이스. 씬에 `T` 컴포넌트가 없으면 빈 오브젝트를 만들어 붙입니다.

**구현 요약**

- `Instance` getter에서 `FindObjectOfType`, 없으면 `new GameObject` + `AddComponent<T>()`.
- `Awake`에서 첫 인스턴스를 `_instance`로 등록하고 `DontDestroyOnLoad`, 중복이면 `Destroy`.
- `protected virtual Initialize()`를 `Awake` 끝에서 호출해 파생 클래스 초기화 훅으로 사용 (`ColorDrawer`가 사용).

---

## `ColorDrawer.cs`

**역할:** 메인 “캔버스”. `Texture2D` + `Color32[] pixelBuffer`에 원을 그리고 `SpriteRenderer`로 보여 줍니다. **게임 시간, 종료 연출, 승리 텍스트, 라운드 종료 후 리셋**까지 담당합니다.  
**직접적인 마우스/터치로 그리기는 현재 사용하지 않으며**, 플레이어·AI가 **`PaintAtWorldPosition`** 을 호출해 칠합니다.

**구현 요약**

- **참조**: `ColorAreaCalculator`, **`Simulator`**(Victory 후 `ResetRoundActors` 연동). 없으면 `FindObjectOfType<Simulator>()` 로 보완.
- **초기화 (`Initialize`)**: `SpriteRenderer` 확보 → `InitializeCanvasTexture`로 텍스처/스프라이트 생성 → `FitCanvasToScreen`으로 Orthographic 카메라에 맞게 스케일 → `ColorAreaCalculator.Initialize` 연동.
- **그리기**
  - `PaintCircle`: 바운딩 박스 안에서 거리 제곱으로 원 내부만 순회, 색이 바뀔 때만 버퍼 수정.
  - `registerWithCalculator == true`이면 `ColorAreaCalculator.RegisterPixelColorChange`로 델타만 큐에 넣음.
  - 변경이 있으면 `UploadTextureRegion`으로 **사각 영역만** `SetPixels32(x,y,w,h)` 후 `Apply(false, false)`.
- **좌표**
  - 월드: `PaintAtWorldPosition` → `TryWorldToTexturePixel` — `InverseTransformPoint` + `sprite.bounds`로 0~1 정규화 후 픽셀 인덱스.
  - `PaintAtScreenPosition` 등 화면 좌표 API는 코드에 남아 있을 수 있으나, **에이전트 경로는 월드 기준**이 중심입니다.
- **브러시 스케일**: `GetScaledBrushRadius(holdTime)` — 홀드 시간에 비례해 반지름 확대. `PlayerUser` / `SimulateUser`의 `strideRate` 구간 안에서 타이머로 호출.
- **게임플레이 페이즈 (`CanvasGameplayPhase`)**
  - `Normal`: 내부용 브러시 색 주기(`colorList` / `changeColorTimer`), `gameplayDuration` 타이머, `LateUpdate`에서 `FlushPendingPixelChanges`. (입력 루프는 주석 처리.)
  - `DominantCoverSweep`: 시간 종료 후 `GetMostPaintedColors`로 우승색 결정 → 텍스처 **왼쪽 하단 (0,0)** 을 중심으로 반지름을 키우며 원으로 덮음 (`registerWithCalculator: false`).
  - `VictoryHold`: `TMP_Text victoryText`에 `"Red|Green|Blue|RGB Victory"` 표시, `victoryFadeInDuration` 동안 알파 0→1, `victoryResetDelaySeconds` 후 `FinishEndgameResetToBase`.
  - **리셋 (`FinishEndgameResetToBase`)**: `pixelBuffer` 전부 `baseColor`, 전체 텍스처 업로드 → `ColorAreaCalculator.ResetPanelsAndInitialize` → **`Simulator.ResetRoundActors()`** (AI 재스폰 + 플레이어 초기 위치/`ResetForRound`) → 페이즈 `Normal` + 플레이 타임 0.
- **차단**: `Normal`이 아닐 때 `PaintAtWorldPosition` 등은 즉시 return → 종료 연출 중에는 칠하기 무시.

---

## `ColorAreaCalculator.cs`

**역할:** 캔버스 전체 픽셀 수 대비 **색별 점유 픽셀 수**를 유지하고, `RatioPanelUI`를 생성·갱신·정렬합니다.

**구현 요약**

- `colorPixelCounts`: `Color32` 키별 픽셀 개수. `Initialize` 시 전부 `baseColor(흰색)`로 시작.
- **성능**: `RegisterPixelColorChange`는 리스트에 `(old, new)`만 추가. 실제 딕셔너리/UI 갱신은 `FlushPendingPixelChanges`에서 한 번에 처리.
- **Flush 흐름**: 등장 색에 대해 `EnsureColorKeyAndPanel`(없으면 카운트 0 + 프리팹 Instantiate) → 모든 델타로 `-1/+1` 적용 → 건드린 색만 `UpdateRatio` → `ReorderPanelsByPixelCounts`.
- **`GetMostPaintedColors`**: `baseColor`·0개 제외 후 최대 점유 색(동률이면 여러 개).
- **`ResetPanelsAndInitialize`**: 기존 `RatioPanelUI` 오브젝트 `Destroy` 후 `Initialize`로 초기화 (`ColorDrawer` 종료 리셋 시 사용).

---

## `UI/RatioPanelUI.cs`

**역할:** 한 가지 색에 대한 **슬라이더 비율(0~100%)** 표시.

**구현 요약**

- `Initialize`: `Slider.maxValue = 100`, `Image`에 해당 색 적용.
- `UpdateRatio`: `Slider.value`에 퍼센트 반영.

---

## `Simulator.cs`

**역할:** `SimulateUser` 프리팹을 **팀별 뷰포트 앵커**에 맞춰 **총 8명** 스폰하고, **`PlayerUser`** 의 라운드 시작 위치를 기억해 Victory 리셋 시 복구합니다.

**구현 요약**

- **스폰 수**: 빨강 2 + 파랑 3 + 초록 3 (`SimulateUserCount == 8`).
- **위치**: `blueSpawnViewport` / `greenSpawnViewport` / `redSpawnViewport`에 기준 두고, `teamSpawnOffsetRadius`로 팀원끼리 살짝 흩어 뜸.
- **역할**: 팀마다 `AgentRole`(Coverage / Denial / Defender)을 셔플·배정. 빨강 2명은 서로 다른 역할 2개를 랜덤 선택.
- **속도**: 스폰 시 `simulateUserStrideRange` 안에서 `stride`(월드/초) 랜덤, `SimulateUser.Initialize(..., strideOverride)`로 전달.
- **라이프사이클**: `Awake`에서 `RespawnSimulateUsers()`; `Start`에서 플레이어 `transform`을 초기 위치로 캐시.
- **리셋**: `ResetRoundActors()` → 기존 자식 `SimulateUser`는 **`DestroyImmediate`**로 즉시 제거(리셋 직후 한 프레임 더 칠하는 문제 완화) → 동일 규칙으로 재스폰 → `PlayerUser.ResetForRound`로 위치·그리기 상태 초기화.
- 경계 클램프·반발은 `Simulator`가 아니라 **`SimulateUser` / `PlayerUser` + `AgentSeparation2D`** 쪽에서 처리합니다.

---

## `PlayerUser.cs`

**역할:** **키보드 입력**으로 월드에서 이동하며 `ColorDrawer.PaintAtWorldPosition`으로 칠합니다. 예전 `ColorDrawer` 마우스/터치 입력을 대체하는 역할입니다.

**구현 요약**

- `FixedUpdate`: `Horizontal` / `Vertical` 입력 → `Rigidbody2D.MovePosition`, `AgentSeparation2D` 분리·경계 반발.
- `strideRate`마다 `startPosition` 기준으로 `DrawColor` 코루틴(홀드 시간에 따른 반지름 스케일).
- **`ResetForRound`**: Victory 후 라운드 리셋 시 그리기 코루틴 중단, stride 타이머 초기화, `Rigidbody2D`와 위치 동기화 — 흰 캔버스에 이전 좌표가 찍히지 않도록 함.

---

## `SimulateUser.cs`

**역할:** AI가 **의도 방향(`currentIntent`)** 과 **분리·경계 보정**으로 움직이며, `strideRate` 주기로 고정 `startPosition`에 원을 그립니다.

**구현 요약**

- **`AgentRole`**: `ScoreDirection`에서 Coverage / Denial / Defender(패트롤·수비 FSM)별 샘플 점수가 다름. Defender 패트롤은 Coverage와 비슷한 가중치를 쓰는 구간이 있음.
- 이동: `Rigidbody2D.MovePosition`, `AgentSeparation2D.Compute`, 뷰포트 기반 경계 반발/탈출 등.
- 라운드 리셋 시 `Simulator`가 인스턴스를 파기·재생성하므로 별도 오브젝트 유지 문제는 없음. `Initialize`에서 그리기 상태 초기화.
- 고급: Coverage용 중심 유도·측면 락, BoundaryRecover 등(코드 주석·시리얼라이즈 필드 참고).

---

## `AgentSeparation2D.cs`

**역할:** 2D에서 **에이전트 간 분리 벡터**와 **카메라 뷰포트 경계 근접/반발/탈출 방향** 등을 정적 메서드로 제공합니다. `SimulateUser`, `PlayerUser`가 사용합니다.

---

## `ArenaBounds.cs`

**역할:** `Camera` 뷰포트에 대응하는 **월드 XY AABB** 계산·클램프·속도 보정 등 **유틸리티 정적 클래스**입니다.

**참고:** 현재 이동체 스크립트는 주로 **`AgentSeparation2D` + 뷰포트 파라미터**로 경계를 다루고, `ArenaBounds`를 직접 호출하지 않을 수 있습니다. 맵 제한 로직을 한곳에 모을 때 활용할 수 있습니다.

---

## 씬에서의 연결 팁

- **`ColorDrawer`**: `ColorAreaCalculator`, (선택) `victoryText`, **`Simulator`** 참조 할당(없으면 런타임 탐색).
- **`ColorAreaCalculator`**: `ratioPanelUIPrefabs`, `ratioPanelUIParent` 할당.
- **`Simulator`**: `simulateUserPrefab`, (선택) `playerUser` 할당. 씬에 `ColorDrawer` 싱글톤이 있어야 에이전트 칠하기·집계가 동작합니다.
- **`PlayerUser`**: 씬에 배치하거나 프리팹으로 두고, 팀 색/이동 파라미터는 인스펙터·`Initialize`로 맞춤.
