# ShowTime 기술 문서 — 핵심 구현 4 + 시행착오

> 이 프로젝트는 게임이 아니라 **전투 연출 파이프라인의 시연**이다.
> 버튼 없이 타임라인 한 루프(7.5초)가 재생되고, 그 뒤의 셰이더·트랙·렌더 패스·측정이 본체다.
> 모든 씬/타임라인/머티리얼은 에디터 툴이 코드로 생성하며, 외부 아트 에셋은 0개다.

---

## 핵심 구현 1 — 전투 셰이더 (HLSL/ShaderLab 직접 작성)

**문제**: 몹 20기가 각자 다른 타이밍에 플래시·디졸브·아웃라인을 재생해야 하는데, 머티리얼 인스턴스를 만들면 배칭이 깨지고 관리가 불가능해진다.

**해결**:
- 전투 연출을 **몹 통합 셰이더 1개**(`Mob_Combat.shader`)로 합치고, 렌더러별 차이는 전부 `MaterialPropertyBlock`으로 주입 — 공유 머티리얼 유지.
- **디졸브**: 노이즈 텍스처를 색이 아니라 "픽셀별 소멸 순서표"로 사용, `clip(noise - amount)` + 경계 픽셀 HDR 발광. 노이즈는 외부 에셋 대신 에디터 툴로 절차 생성(밸류 노이즈 4옥타브, seamless). 인스턴스별 UV 오프셋/스케일로 **텍스처 1장을 공유하면서 20기가 전부 다른 모양으로 소멸**.
- **아웃라인**: "실루엣 알파(UV 0~1 밖 = 알파 0)" 규칙의 8방향 이웃 샘플링. 그레이박스 쿼드에선 테두리가, 스프라이트가 들어오면 그림 실루엣이 같은 코드로 경계가 된다. `_OutlineAmount=0`이면 `[branch]`로 샘플링 자체를 생략(평상시 비용 0).
- **화면 왜곡(오브젝트 방식)**: `_CameraOpaqueTexture`를 밀린 UV로 재샘플링하는 그랩 쿼드. 링 밖 픽셀은 `clip`으로 폐기해 재샘플링 대역폭을 링 영역으로 한정.
- 모바일 논점 기록: 알파 테스트(`clip`)는 TBDR에서 Early-Z를 깨므로 필요한 곳에만. 아웃라인 8탭은 저사양에서 4탭 대안.

## 핵심 구현 2 — Timeline 커스텀 트랙·마커·조립 툴

**문제**: 연출 시퀀스를 코드 타이머로 짜면 스크럽/중간 재생이 불가능하고, 아티스트가 타이밍을 만질 수 없다.

**해결**:
- 커스텀 트랙 3종: `MobFxTrack`(플래시 스윕/디졸브/아웃라인 클립, MobGroup 바인딩), `ShaderFloatTrack`(임의 셰이더 float from→to, 클립 밖 렌더러 off 옵션), `CameraShakeTrack`(Perlin 셰이크 — 결정적이라 스크럽 안전, 기준 위치 복원 보장).
- **클립은 전부 무상태(stateless)**: 누적 변수 없이 클립 로컬 시간만으로 값을 계산 → 어느 방향으로 스크럽해도 같은 그림.
- **아웃라인 세기 = 클립 ease-in/out 가중치**: "차오르는" 연출을 코드가 아니라 클립 블렌드 곡선이 담당 — 믹서 설계의 이점.
- 마커: `HitStopMarker`(INotification) → 수신자가 timeScale을 낮췄다 **Realtime 대기 후** 복원. 탄막 발사도 같은 패턴(`BulletBurstMarker`).
- 렌더러가 없는 대상(RenderFeature 구동 값)은 **내장 AnimationTrack**으로 구동 — 커스텀/내장 트랙 혼용.
- 조립 툴 `SkillComposerWindow`: 트랙/클립/마커/바인딩을 한국어 라벨로 편집. **같은 트랙 클립 겹침 = 크로스페이드 함정을 경고**로 잡아주고, 새 클립 기본 배치도 마지막 클립 뒤.
- 그룹 관리: `MobGroup`이 렌더러당 구동부 3개를 대체 — 값 캐시 + dirty 플래그로 렌더러당 프레임 최대 1회 `SetPropertyBlock`.

## 핵심 구현 3 — RenderGraph 화면 연출 패스 (Unity 6 신규 API)

**문제**: 오브젝트 방식 왜곡은 Opaque Texture 복사가 필요하고, 쿼드가 놓인 영역만 왜곡되며, 투명 큐 정렬에 종속된다.

**해결**: RenderFeature 2종을 RenderGraph API(`RecordRenderGraph`)로 작성.
- `SkillImpactFeature`: 스킬 발동 비네트+색조(탈색·한색 틴트) — `AddBlitPass`로 activeColor→새 텍스처, `resourceData.cameraColor` 교체.
- `SkillRippleFeature`: 전체 화면 충격파 물결 — 월드 좌표를 카메라로 뷰포트 변환, 종횡비 보정(링이 타원이 되지 않게).
- **평상시 비용 0 설계**: `AddRenderPasses`에서 구동 값이 0이면 패스를 큐에 넣지 않는다.
- 데모에서 오브젝트 방식(M1 쿼드)과 패스 방식이 **같은 프레임에 동시 재생**되어 차이를 보여준다:

| | 오브젝트 방식 (그랩 쿼드) | 패스 방식 (RenderGraph) |
| --- | --- | --- |
| 사전 비용 | Opaque Texture 복사 1회 | 없음 (activeColor 직접) |
| 적용 범위 | 쿼드가 덮는 영역 | 화면 전체 |
| 파이프라인 위치 | 투명 큐 정렬에 종속 | 삽입 지점 자유 (이벤트 선택) |
| 카메라 정보 | 셰이더에서 간접 | 패스 코드에서 직접 접근 |

## 핵심 구현 4 — GPU Instancing 탄막 + 실측

**문제**: 탄막 수백 발을 GameObject로 그리면 드로우콜이 탄환 수만큼 폭증한다.

**해결**: `BulletSystem` — 시뮬레이션(구조체 배열, swap-remove, 프레임당 힙 할당 0)과 렌더링을 분리. `Graphics.RenderMeshInstanced`로 살아있는 탄환 전부를 드로우콜 1회에. 대조군(탄환당 GameObject) 모드를 같은 시스템에 내장해 전환 실측.

**실측** (에디터 1920×1080, 탄환 400+ 활성 구간, 셰이더 워밍업 후 측정, 원자료 `Docs/perf/*.csv`):

| | Instanced | Naive (GameObject) |
| --- | --- | --- |
| 드로우콜 | avg 30.7 / max 31 | avg 463.8 / max 534 |
| 셋패스 | 28.7 | 31.9 |
| 프레임 타임 (중앙값) | 5.46ms | 5.73ms |

- 탄막 600발의 드로우콜 비용: **+1 vs +434**.
- 셋패스가 비슷한 이유: SRP Batcher가 나이브 쪽 상태 변경을 흡수한다 — 드로우콜 폭증은 인스턴싱만이 막는다.
- 프레임 타임 차이가 작은 이유: 고성능 PC의 CPU 여유. 드로우콜당 CPU 비용이 지배하는 모바일/키오스크급에서 벌어지는 항목이다 (정직한 해석).

---

## 시행착오 기록 (문제 → 원인 → 교훈)

1. **타임라인 클립이 에디터 재시작 후 missing script** — ScriptableObject 파생(클립/마커)은 **파일명 = 클래스명**이 아니면 도메인 리로드 후 직렬화 복원이 깨진다("No script asset"). 당장은 돌아가서 더 위험. 한 파일에 여러 클립 클래스 금지.
2. **RenderGraph 블릿이 검은 화면** — 블릿 셰이더에 core `Common.hlsl`만 include → `TEXTURE2D_X` 미정의로 컴파일 실패 → 깨진 머티리얼로 블릿하면 미기록 텍스처가 화면에 올라간다. **URP `Core.hlsl` + core `Blit.hlsl` 페어가 정석.** 원인 격리는 프래그를 ①단색(파이프라인) → ②소스 패스스루(샘플링) → ③원래 수학 순으로 바꾸는 삼단 격리로.
3. **`RenderMeshInstanced`가 아무것도 안 그림** — `material.enableInstancing` 필수. 없으면 매 프레임 예외만 쌓이고 화면엔 증상이 없다.
4. **녹화가 연출 전에 끝남** — 플레이 시작 히치에서 `Time.maximumDeltaTime`(기본 0.333s) 클램프로 게임 시간이 벽시계보다 수 초 뒤처진다. **검증/녹화 도구는 대상과 같은 시계(게임 시간)를 쓸 것.**
5. **`TimelineAsset.markerTrack`은 자동 생성되지 않는다** — `CreateMarkerTrack()` 선행 필수 (NRE).
6. **에디터 백그라운드 절전** — 포커스 없으면 게임 루프가 멎는다. 검증 자동화는 OS 화면 캡처(포커스 의존) 대신 **씬 내 `ScreenCapture` 레코더 + Interaction Mode 무스로틀**로.

## 측정·검증 방법론

- `FrameRecorder`: 게임 뷰를 원본 해상도로 게임 시간 기준 캡처 — 타임라인 한 루프 = 한 녹화. GIF/영상의 원판.
- `PerfProbe`: `ProfilerRecorder`(Draw Calls/SetPass/Batches) + 프레임 타임을 매 프레임 CSV로. 수정 1건 = 측정 1회, 항상 워밍업 1회 후 측정.
