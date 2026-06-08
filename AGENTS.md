# VRproject_HapticGlove — 프로젝트 컨텍스트

## 프로젝트 개요
- **엔진**: Unity 6 (URP 17.4.0)
- **타겟 기기**: Meta Quest 3
- **목표**: 오른손을 공압 햅틱 장갑으로 대체하는 VR 프로젝트
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
- `Assets/Scenes/SubwayScene.unity` — 현재 VR 메인 씬
- `Assets/Scenes/TestScene.unity` — 테스트 씬

---

## 스크립트 목록 (`Assets/Scripts/`)

| 파일 | 설명 |
|---|---|
| `AnimateHandOnInput.cs` | 컨트롤러 Trigger/Grip 값으로 손 애니메이터 구동 (왼손용) |
| `GloveUDPCommunicator.cs` | **오른손 전용** — 공압장갑 JSON UDP 송수신 + 총/검 모드 연결 |
| `PlayerWeaponLoadout.cs` | 플레이어 시작 무기 선택 + 손에 고정 장착 |
| `pistol.cs` | 권총 발사 스크립트 |
| `TeleportationActivator.cs` | 텔레포트 활성화 |
| `InputTest.cs` | 인풋 테스트 |
| `Editor/FpsHorrorKitToURPConverter.cs` | HDRP→URP 마테리얼 변환 툴 (Tools 메뉴) |

---

## 공압 장갑 UDP 통신 (`GloveUDPCommunicator.cs`)

### 구조
- 오른손 컨트롤러를 손목에 매달아 위치 트래킹 유지
- 오른손 손가락 애니메이션은 컨트롤러 입력 대신 장갑 모드 값으로 구동
- 왼손은 기존 `AnimateHandOnInput.cs` 그대로 사용
- 총 발사는 컨트롤러 Trigger가 아니라 장갑에서 받은 `shot_id` 증가 여부로 처리
- 검 모드의 `damage_multiplier`는 1~3 범위에서 `PlayerStatus.currentTension` 0~1로 매핑
- 플레이어 무기는 `VR Player`의 `PlayerWeaponLoadout.startingWeapon`에서 `Pistol`/`Katana` 중 선택
- 선택된 무기만 오른손에 고정 장착되고, 책상 위 진열용 무기/선택되지 않은 무기는 비활성화
- 무기 잡기/버리기는 XR Grab/컨트롤러 입력과 무관하게 비활성화, 인스펙터 선택으로만 변경

### Network
- Glove/cRIO IP: `192.168.0.10`
- Unity PC IP: `192.168.0.9`
- Glove -> Unity UDP: Unity가 `0.0.0.0:9001` 또는 `192.168.0.9:9001`에서 listen
- Unity -> Glove mode command UDP: `192.168.0.10:9002`
- Unity 수신 bind는 `127.0.0.1:9001` 금지, `0.0.0.0:9001` 권장
- 브라우저 시각화와 Unity가 동시에 `9001`을 잡으면 충돌 가능, 시연 때는 Unity만 `9001` 수신 권장

### Glove -> Unity 패킷
UTF-8 JSON, UDP.

검 모드:
```
{
  "schema": "kocca-game-glove-demo-v1",
  "mode": "sword",
  "damage_multiplier": 1.72
}
```

총 모드:
```
{
  "schema": "kocca-game-glove-demo-v1",
  "mode": "gun",
  "shot_id": 17
}
```

총 처리:
- 처음 받은 `shot_id`는 기준값으로 저장
- 이후 `shot_id > lastShotId`이면 격발 처리
- 처리 후 `lastShotId = shot_id`

### Unity -> Glove 패킷
UTF-8 JSON, UDP. 모드 변경용.

```
{
  "schema": "kocca-game-command-v1",
  "mode": "sword"
}
```

```
{
  "schema": "kocca-game-command-v1",
  "mode": "gun"
}
```

Unity는 모드 버튼 입력 시 같은 패킷을 3~5번 정도, 30ms 간격으로 반복 송신한다. 장갑 쪽은 같은 `mode` 반복 수신을 무시해도 된다.

### Animator 매핑
- Index → `Trigger` 파라미터
- (Middle + Ring + Pinky) / 3 → `Grip` 파라미터
- sword 모드 시 시각 포즈: `0,0.6,0.6,0.6,0.6`
- gun 모드 시 시각 포즈: `0,0,0.95,0.95,0.95`

### Unity 설정 방법
1. 오른손 손 오브젝트에서 `AnimateHandOnInput` 비활성화
2. 같은 오브젝트에 `GloveUDPCommunicator` 추가
3. Inspector 설정:
   - Local Listen Address: `0.0.0.0`
   - Receive Port: `9001`
   - Glove IP: `192.168.0.10`
   - Command Port: `9002`
   - Pistol: 현재 사용할 `Pistol` 컴포넌트 연결
   - Player Status: 씬의 `PlayerStatus` 컴포넌트 연결
   - Hand Animator: 오른손 Animator 연결

### 시작 무기 설정
1. `VR Player` 오브젝트의 `PlayerWeaponLoadout`에서 `Starting Weapon` 선택
2. `Weapon Hold Parent`는 오른손 Transform 연결
3. Pistol/Katana의 local position/euler/scale 값으로 손 안에서의 위치 보정
4. `Send Glove Mode On Start`가 켜져 있으면 시작 무기에 맞춰 장갑에 `gun` 또는 `sword` 명령 송신

### 모드 변경 호출
```csharp
GloveUDPCommunicator glove = GetComponent<GloveUDPCommunicator>();
glove.SetSwordMode();
glove.SetGunMode();
```

### 장갑 쪽 mode별 동작
- sword 수신: target `0,0.6,0.6,0.6,0.6`, Unity로 `damage_multiplier` 송신
- gun 수신: target `0,0,0.95,0.95,0.95`, trigger `fire=0.6`, `rearm=0.5`, `release=0`, `rearm check delay=0.3`, Unity로 `shot_id` 송신

---

## 총 (`Assets/Guns/`)

### pistol.cs 주요 설정
- `bulletPrefab`: `Assets/Guns/Prefabs/BulletLite_01.prefab`
- `firePoint`: 총구 위치 Transform
- `bulletSpeed`: 20f
- `bulletLifetime`: 5f

### BulletLite_01.prefab 수정 이력
- Rigidbody 컴포넌트 수동 추가 (원본에 없었음), `useGravity: false`
- 인스턴스화 시 `Quaternion.Euler(0f, 90f, 0f)` 오프셋 적용 (메쉬가 X축 방향으로 모델링되어 있음)

---

## 임포트된 에셋
| 에셋 | 상태 | 비고 |
|---|---|---|
| Meta XR SDK Core | ✅ 정상 | |
| XR Interaction Toolkit | ✅ 정상 | |
| Animated Hands | ✅ 정상 | |
| Zombies_Pack (ithappy) | ✅ 정상 | |
| LowpolyWeapon_lite | ⚠️ 핑크 마테리얼 | Built-in 셰이더 → Edit>Rendering>Materials>Convert All Built-in Materials to URP 로 변환 가능 |
| jts_wakizashi | ⚠️ 핑크 마테리얼 | Built-in 커스텀 셰이더 → 수동으로 URP/Lit 교체 필요 |
| FpsHorrorKit | ❌ 제거됨 | HDRP 전용, URP 호환 불가 |

---

## 주의사항
- GraphicsSettings.asset 변조 위험: 에셋 임포트 후 마테리얼이 갑자기 전부 핑크가 되면 `git diff ProjectSettings/GraphicsSettings.asset` 먼저 확인
- 비선형 MeshCollider + Rigidbody 조합 주의 (총알 등)
- URP 17.x = Unity 6 기준, `rb.linearVelocity` API 사용 (구버전은 `rb.velocity`)
