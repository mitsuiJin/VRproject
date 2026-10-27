# VRproject_HapticGlove — 프로젝트 컨텍스트

## 프로젝트 개요
- **엔진**: Unity 6 (URP 17.4.0)
- **타겟 기기**: Meta Quest 3
- **목표**: 오른손을 공압 햅틱 장갑으로 대체하는 VR 슈팅/액션(좀비) 게임
- **브랜치**: `siyun` (작업 브랜치), `main` (기본)
- **Git 원격**: https://github.com/mitsuiJin/VRproject

---

## 렌더 파이프라인
- **URP (Universal Render Pipeline)** 사용
- 에셋 임포트 시 HDRP/Built-in 전용 에셋은 핑크 마테리얼 발생 → **URP 호환 에셋만 사용할 것**
- 에셋 고를 때 설명란에 "URP" 또는 "Universal Render Pipeline" 명시 확인 필수

### 과거 사고 기록
- FpsHorrorKit 임포트 시 `ProjectSettings/GraphicsSettings.asset`의 렌더 파이프라인이 HDRP로 교체됨 → 전체 마테리얼 핑크
- 복구: `git checkout HEAD -- ProjectSettings/GraphicsSettings.asset`
- FpsHorrorKit은 HDRP 전용이라 제거함

---

## 씬 구성
- `Assets/Scenes/TestScene.unity` — 기능 테스트용 씬
- `Assets/Scenes/SubwayScene.unity` — 메인 플레이 씬 (NavMesh 베이크됨, ZombiePortal 배치)

---

## 스크립트 목록

### `Assets/Scripts/`
| 파일 | 설명 |
|---|---|
| `AnimateHandOnInput.cs` | 컨트롤러 Trigger/Grip 값으로 손 애니메이터 구동 (왼손용) |
| `GloveUDPCommunicator.cs` | **오른손 전용** — 공압장갑과 UDP/JSON 모드 통신 + 손 애니메이션 |
| `PlayerStates.cs` (클래스명 `PlayerStatus`) | 싱글톤. 플레이어 텐션(검기 긴장도) 상태 저장 |
| `PlayerWeaponLoadout.cs` | Pistol/Katana 중 활성 무기를 손에 부착·고정, 장갑에 모드 전달 |
| `pistol.cs` (클래스명 `Pistol`) | 권총 발사 + 총구 햅틱 (`RightControllerHaptics`) |
| `Bullet.cs` | 총알 트리거 충돌 시 `EnemyHealth.TakeDamage` 후 자기 파괴 |
| `Katana.cs` | 근접 베기 트리거 충돌 → `EnemyHealth.TakeDamage` + 베기 사운드 |
| `TwoHandKatana.cs` | 카타나 양손 그립: 한 손이면 위치 고정, 두 손이면 이동 가능 + 칼날이 두 번째 손 방향으로 정렬 |
| `TeleportationActivator.cs` | 입력 액션으로 텔레포트 레이 인터랙터 on/off |
| `InputTest.cs` | 인풋 테스트 |
| `Editor/FpsHorrorKitToURPConverter.cs` | HDRP→URP 마테리얼 변환 툴 (Tools 메뉴) |

### `Assets/Enemy/scripts/`
| 파일 | 설명 |
|---|---|
| `ZombieSpawner.cs` | Phase 기반 + 목표 개체수(`targetActiveZombies`) 유지 스폰, `NavMesh.SamplePosition`으로 스폰 위치 계산 |
| `EnemyHealth.cs` | 데미지 처리. **플레이어 텐션이 임계값(`tensionInstantKillThreshold`) 이상이면 즉사 + 넉백**(텐션에 비례한 거리), 넉백 시 햅틱 발생 |
| `EnemyMoovement.cs` | NavMeshAgent로 플레이어 추적, 도달 시 공격 애니메이션 트리거 |
| `EnemySound.cs` | 주기적 그로울, 피격/사망/공격 사운드 재생 |

> 주의: `PlayerStates.cs`, `Katana.cs`, `Bullet.cs`, `EnemyMoovement.cs`, `EnemySound.cs`는 **레거시 인코딩(비-UTF8)**으로 저장되어 있어, 이 소스들은 일반 텍스트 에디터로 열지 말고 Unity/Visual Studio 등에서 원래 인코딩을 유지하며 수정할 것.

---

## 공압 장갑 통신 (`GloveUDPCommunicator.cs`)

### 프로토콜
- **UDP 기반 JSON** (바이너리 고정 패킷 아님)
- **수신** (장갑 → Unity): 포트 **9001**, 스키마 `kocca-game-glove-demo-v1`
  ```json
  {"schema":"kocca-game-glove-demo-v1","mode":"sword","damage_multiplier":1.0,"shot_id":0}
  ```
  - `mode`: `"sword"` 또는 `"gun"`
  - `sword` 모드: `damage_multiplier` → `minDamageMultiplier`~`maxDamageMultiplier` 범위로 정규화되어 `PlayerStatus.currentTension`에 반영
  - `gun` 모드: `shot_id`가 이전 값보다 커지면 1회 발사로 처리 (최초 수신값은 baseline으로만 사용)
- **송신** (Unity → 장갑, 모드 커맨드): 포트 **9002**, 스키마 `kocca-game-command-v1`, 패킷 손실 대비 `commandRepeatCount`회 반복 전송
  ```json
  {"schema":"kocca-game-command-v1","mode":"sword"}
  ```

### Animator 매핑 (모드 기반, 손가락 각도 직접 수신 아님)
- `sword` 모드 → `Trigger=0, Grip≈0.6`
- `gun` 모드 → `Trigger=0, Grip≈0.95`

### 연동
- `PlayerWeaponLoadout`이 현재 장착 무기(Pistol/Katana)에 맞춰 `glove.initialMode`를 설정하고 `SendModeCommand()` 호출
- `gun` 모드에서 shot 수신 시 `pistol.FireFromGlove()` 호출 → `Bullet` 발사 + `RightControllerHaptics.Pulse()`

### Unity 설정 방법
1. `Right Hand Controller` 오브젝트에서 `AnimateHandOnInput` 비활성화
2. 같은 오브젝트에 `GloveUDPCommunicator` 추가
3. Inspector: Glove IP / Receive Port(9001) / Command Port(9002) / Hand Animator / Pistol 연결

### 레거시 API
- `SendTargets(float[5])`는 하위 호환용으로만 남아 있으며, 내부적으로 손가락 값 패턴을 보고 `sword`/`gun` 모드 커맨드로 변환해서 보냄 (실제 손가락 각도를 그대로 보내는 방식이 아님)

---

## 무기 시스템

### `PlayerWeaponLoadout.cs`
- `startingWeapon`(Pistol/Katana) 기준으로 무기를 `weaponHoldParent`에 부착, 비선택 무기는 비활성화 + `XRGrabInteractable` 잠금(Rigidbody kinematic)
- 씬 내 다른 Pistol/Katana/TwoHandKatana 및 이름에 "Rifle/Pistol/Katana"가 들어간 오브젝트도 자동으로 숨김 처리
- 실행 시 `ApplyGloveMode()`로 장갑에 현재 무기에 맞는 모드 커맨드 전송

### `pistol.cs` (`Pistol`)
- `bulletPrefab`: `Assets/Guns/Prefabs/BulletLite_01.prefab`
- `firePoint`: 총구 위치 Transform, `bulletSpeed`: 20f, `bulletLifetime`: 5f
- `allowControllerFire`는 테스트용 플래그 (기본 false) — 실제 발사는 장갑 `shot_id` 수신(`FireFromGlove`)으로 트리거
- 발사 시 `RightControllerHaptics.Pulse()`로 오른 컨트롤러 햅틱

### `BulletLite_01.prefab` 수정 이력
- Rigidbody 컴포넌트 수동 추가 (원본에 없었음), `useGravity: false`
- 인스턴스화 시 `Quaternion.Euler(0f, 90f, 0f)` 오프셋 적용 (메쉬가 X축 방향으로 모델링되어 있음)

### `Katana.cs` / `TwoHandKatana.cs`
- `Katana`: 트리거 콜라이더가 `Enemy` 태그와 충돌하면 `EnemyHealth.TakeDamage(damage, hitPoint)` + 베기 사운드
- `TwoHandKatana`: 한 손만 잡으면 위치/회전 고정(`isKinematic`), 두 손 모두 잡으면 물리 활성화 + 칼날이 두 손 사이 방향으로 자동 정렬

---

## 적(좀비) 시스템 (`Assets/Enemy/`)
- `ZombieSpawner`가 Phase별 프리팹 풀에서 랜덤 스폰, `targetActiveZombies` 개체수를 주기적으로 유지(죽은/누락된 좀비는 목록에서 제거 후 재스폰)
- `EnemyHealth`: 일반 피격은 `health` 차감 → `Hit`/`Die` 애니메이터 트리거. **플레이어 텐션(`PlayerStatus.currentTension`)이 임계값 이상이면 즉시 사망 + 텐션에 비례한 넉백 슬라이드**, 넉백 시 오른 컨트롤러 햅틱 발생
- `EnemyMoovement`: `NavMeshAgent`로 `Player` 태그 오브젝트를 추적, 공격 사거리 도달 시 `Attack` 트리거 (쿨다운 적용)
- `ZombiePortal` 프리팹: `SubwayScene`에 배치된 스폰 지점

---

## 임포트된 에셋
| 에셋 | 상태 | 비고 |
|---|---|---|
| Meta XR SDK Core | ✅ 정상 | |
| XR Interaction Toolkit | ✅ 정상 | |
| Animated Hands | ✅ 정상 | |
| Zombies_Pack (ithappy) | ✅ 정상 | `Assets/Enemy/Zombie/` 좀비 프리팹 출처 |
| LowpolyWeapon_lite | ⚠️ 핑크 마테리얼 | Built-in 셰이더 → Edit>Rendering>Materials>Convert All Built-in Materials to URP 로 변환 가능 |
| jts_wakizashi | ⚠️ 핑크 마테리얼 | Built-in 커스텀 셰이더 → 수동으로 URP/Lit 교체 필요 |
| FpsHorrorKit | ❌ 제거됨 | HDRP 전용, URP 호환 불가 |

---

## 주의사항
- GraphicsSettings.asset 변조 위험: 에셋 임포트 후 마테리얼이 갑자기 전부 핑크가 되면 `git diff ProjectSettings/GraphicsSettings.asset` 먼저 확인
- 비선형 MeshCollider + Rigidbody 조합 주의 (총알 등)
- URP 17.x = Unity 6 기준, `rb.linearVelocity` API 사용 (구버전은 `rb.velocity`)
- `Assets/Scripts/` 및 `Assets/Enemy/scripts/`의 일부 .cs 파일이 비-UTF8 인코딩이라 일반 텍스트 도구(Edit/Write 등)로 직접 수정이 거부될 수 있음 — Unity/Visual Studio로 수정
