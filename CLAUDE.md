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
- `Assets/Scenes/Main VR Scene.unity` — 메인 VR 씬
- `Assets/Scenes/SampleScene.unity` — 기본 씬

---

## 스크립트 목록 (`Assets/Scripts/`)

| 파일 | 설명 |
|---|---|
| `AnimateHandOnInput.cs` | 컨트롤러 Trigger/Grip 값으로 손 애니메이터 구동 (왼손용) |
| `GloveUDPCommunicator.cs` | **오른손 전용** — 공압장갑 UDP 송수신 + 손 애니메이션 |
| `pistol.cs` | 권총 발사 스크립트 |
| `TeleportationActivator.cs` | 텔레포트 활성화 |
| `InputTest.cs` | 인풋 테스트 |
| `Editor/FpsHorrorKitToURPConverter.cs` | HDRP→URP 마테리얼 변환 툴 (Tools 메뉴) |

---

## 공압 장갑 UDP 통신 (`GloveUDPCommunicator.cs`)

### 구조
- 오른손 컨트롤러를 손목에 매달아 위치 트래킹 유지
- 손가락 애니메이션은 컨트롤러 입력 대신 장갑 UDP 수신값으로 구동
- 왼손은 기존 `AnimateHandOnInput.cs` 그대로 사용

### 패킷 포맷 (양방향 동일)
```
[Thumb(4B)][Index(4B)][Middle(4B)][Ring(4B)][Pinky(4B)]
총 20바이트, float32 little-endian binary, 각 값 0.0~1.0
```

### 포트
- **수신** (장갑 → Unity): 5005
- **송신** (Unity → 장갑): 5006

### Animator 매핑
- Index → `Trigger` 파라미터
- (Middle + Ring + Pinky) / 3 → `Grip` 파라미터

### Unity 설정 방법
1. `Right Hand Controller` 오브젝트에서 `AnimateHandOnInput` 비활성화
2. 같은 오브젝트에 `GloveUDPCommunicator` 추가
3. Inspector: Glove IP / Receive Port(5005) / Send Port(5006) / Hand Animator 연결

### 햅틱 피드백 송신
```csharp
GloveUDPCommunicator glove = GetComponent<GloveUDPCommunicator>();
glove.SendTargets(new float[] { 0f, 0.8f, 0.8f, 0.8f, 0.8f }); // 검지~소지 80% 조임
```

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
