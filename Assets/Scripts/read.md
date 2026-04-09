# `Assets/Scripts` 개요

이 폴더는 **CPU 텍스처에 원으로 칠하기**를 중심으로 한 미니 게임/프로토타입 로직을 담고 있습니다. 그린 결과를 **`ColorAreaCalculator`**가 색별 픽셀 수로 집계하고, **`RatioPanelUI`**로 비율을 표시합니다. **`Simulator` / `SimulateUser`**는 플레이어 대신 월드에서 움직이며 같은 캔버스에 칠합니다.

## 데이터 흐름 (요약)

```
[입력 / SimulateUser]
        ↓ PaintAtScreenPosition / PaintAtWorldPosition
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

**역할:** 메인 “캔버스”. `Texture2D` + `Color32[] pixelBuffer`에 원을 그리고 `SpriteRenderer`로 보여 줍니다. 마우스/터치 입력, 게임 시간, 종료 연출, 승리 텍스트까지 담당합니다.

**구현 요약**

- **초기화 (`Initialize`)**: `SpriteRenderer` 확보 → `InitializeCanvasTexture`로 텍스처/스프라이트 생성 → `FitCanvasToScreen`으로 Orthographic 카메라에 맞게 스케일 → `ColorAreaCalculator.Initialize` 연동.
- **그리기**
  - `PaintCircle`: 바운딩 박스 안에서 거리 제곱으로 원 내부만 순회, 색이 바뀔 때만 버퍼 수정.
  - `registerWithCalculator == true`이면 `ColorAreaCalculator.RegisterPixelColorChange`로 델타만 큐에 넣음.
  - 변경이 있으면 `UploadTextureRegion`으로 **사각 영역만** `SetPixels32(x,y,w,h)` 후 `Apply(false, false)` (전체 업로드 방지).
- **좌표**
  - 화면: `PaintAtScreenPosition` — `Screen` 비율로 텍스처 인덱스.
  - 월드: `PaintAtWorldPosition` → `TryWorldToTexturePixel` — `InverseTransformPoint` + `sprite.bounds`로 0~1 정규화 후 픽셀 인덱스.
- **입력**: 에디터 마우스 / 모바일 터치. **누른 위치는 고정**, 홀드 시간에 따라 반지름만 커지도록 `GetScaledBrushRadius` 사용.
- **게임플레이 페이즈 (`CanvasGameplayPhase`)**
  - `Normal`: 브러시 색 주기 변경(`colorList`), `gameplayDuration` 타이머, 입력 처리, `LateUpdate`에서 `FlushPendingPixelChanges`.
  - `DominantCoverSweep`: 시간 종료 후 `GetMostPaintedColors`로 우승색 결정 → 텍스처 **왼쪽 하단 (0,0)** 을 중심으로 반지름을 키우며 원으로 덮음 (`registerWithCalculator: false`로 집계 큐 비움).
  - `VictoryHold`: `TMP_Text victoryText`에 `"Red|Green|Blue|RGB Victory"` 표시, `victoryFadeInDuration` 동안 알파 0→1, `victoryResetDelaySeconds` 후 `FinishEndgameResetToBase`.
  - 리셋: `pixelBuffer` 전부 `baseColor`, 전체 텍스처 업로드, `ColorAreaCalculator.ResetPanelsAndInitialize`, 페이즈 `Normal` + 플레이 타임 0.
- **차단**: `Normal`이 아닐 때 `PaintAtWorldPosition` / `PaintAtScreenPosition`은 즉시 return → 시뮬/입력 그리기 무시.

---

## `ColorAreaCalculator.cs`

**역할:** 캔버스 전체 픽셀 수 대비 **색별 점유 픽셀 수**를 유지하고, `RatioPanelUI`를 생성·갱신·정렬합니다.

**구현 요약**

- `colorPixelCounts`: `Color32` 키별 픽셀 개수. `Initialize` 시 전부 `baseColor(흰색)`로 시작.
- **성능**: `RegisterPixelColorChange`는 리스트에 `(old, new)`만 추가. 실제 딕셔너리/UI 갱신은 `FlushPendingPixelChanges`에서 한 번에 처리.
- **Flush 흐름**: 등장 색에 대해 `EnsureColorKeyAndPanel`(없으면 카운트 0 + 프리팹 Instantiate) → 모든 델타로 `-1/+1` 적용 → 건드린 색만 `UpdateRatio` → `ReorderPanelsByPixelCounts`(점유 많은 색이 sibling 앞쪽).
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

**역할:** `SimulateUser` 프리팹을 **시작 시 `simulateUserCount`만큼** 생성하고, 유저가 화면 밖으로 나가 사라질 때 **한 명씩 재스폰**합니다.

**구현 요약**

- `SpawnSimulateUser`: `Instantiate` 후 `GetEdgeSpawnWorldPositionAndRotation`으로 위치/회전 설정, `SetOwner(this)`, `Initialize(stride, color)` — `stride`는 랜덤, 색은 `ColorDrawer.Instance.GetRandomColor()`.
- 스폰 위치: 뷰포트 **좌/우 변**(x=0 또는 1, y 랜덤) 또는 **상/하 변**(y=0 또는 1, x 랜덤), `spawnOutsideViewport`만큼 경계 밖으로 밀어 스폰.
- 회전: `SimulateUser`가 로컬 `Vector2.right`로 이동하므로, 어느 변에서 들어오는지에 따라 Z 각도 범위를 다르게 줌(왼쪽 -45~45°, 아래 45~135° 등).

---

## `SimulateUser.cs`

**역할:** 월드에서 이동하며 **고정된 시작 위치**에 대해 일정 시간 동안 반지름이 커지는 원을 `ColorDrawer`에 그립니다. 화면 밖으로 나가면 제거되고 `Simulator`에 재스폰을 요청합니다.

**구현 요약**

- `Start`: `Straight` / `Circle` 이동 랜덤.
- `strideRate`마다 `startPosition`을 갱신하고 `DrawColor` 코루틴 실행: 매 프레임 `PaintAtWorldPosition(startPosition, GetScaledBrushRadius(timer), color)`.
- 이동: `Straight`는 로컬 `right`, `Circle`은 각도 `theta`로 `Translate`.
- **뷰포트 밖 Destroy**: 스폰이 밖에서 될 수 있어 `hasEnteredViewport`로 **한 번이라도 화면 안에 들어온 뒤**에만 `IsOutsideViewport`일 때 `NotifySimulateUserDestroyed` + `Destroy`.

---

## 씬에서의 연결 팁

- `ColorDrawer` 오브젝트: `ColorAreaCalculator`, (선택) `victoryText` 참조 할당.
- `ColorAreaCalculator`: `ratioPanelUIPrefabs`, `ratioPanelUIParent` 할당.
- `Simulator`: `simulateUserPrefab` 할당; 씬에 `ColorDrawer` 싱글톤이 있어야 `GetRandomColor` 등이 동작합니다.
