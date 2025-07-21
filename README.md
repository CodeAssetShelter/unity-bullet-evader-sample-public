## 📚 Study & Open Source

This repository is intended for **educational use** and is publicly available for reference and learning purposes.

🧩 Developed using **Unity3D 6000.0.36f1** & **Photon Fusion 2**

---

# Sample 001

Photon Fusion 2 기반 멀티플레이 탄막 슈팅

---

## ✨ 주요 특징

1. **멀티플레이 실시간 탄막**
2. **TMP 캐릭터 애니메이터** – UI 텍스트에 초경량 애니메이션 효과 적용
3. **RPC 데이터 전송 인코딩** – RPC 회당 512byte 고려, 전송 데이터 인코딩/디코딩
---

## 🔧 기술 스택 & 역할

| 구분         | 내용                                  |
| ---------- | ----------------------------------- |
| Engine     | Unity 6000.0.36f1 LTS (URP)              |
| Networking | Photon Fusion 2 (Host+Client)       |
| 역할         | **기획 → 개발 100 %**           |

---

## 🗂️ 폴더 구조

```text
Assets/Scripts
├─ Game/               # 공통 유틸, 인터페이스, 로컬 게임로직
├─ Network/            # NetworkManager, Spanwers...
├─ Pooling/            # LocalObjectPool, NetworkObjectPool
├─ Sound/
│  ├─ BgmAsset(+Editor)
│  └─ SoundManager
└─ UI/                 # SafeArea, GameSettings, TMP_CharacterAnimator
```

> **Editor 전용** 스크립트는 각 도메인 하위 `Editor/` 폴더에 위치해 빌드 대상에서 자동 제외됩니다.

---

## 🚀 빠른 시작

```bash
# 1) 레포 클론
git clone https://github.com/CodeAssetShelter/unity-bullet-evader-sample-public.git
cd unity-bullet-evader-sample-public

# 2) Unity Hub → Unity 6000.0.36f1 LTS로 열기

# 3) 실행/빌드
#    - PC:  ▶ Play
```

  * WASD 이동, DirctionKey · 마우스 좌클릭 사격
  * F, Space · 선택

---

## 📚 학습 포인트 / 문제 해결

| 이슈                     | 해결 방법                                   |
| ---------------------- | --------------------------------------- |
| Host 모드 시 틱기반 타이머 | `TickTimer` 재설계, Runner 상태 동기화          |
| 탄막 동기화 지연              | Local 예측, Packet Compression |
| Runtime GC             | 구조체 풀링      |
| Rpc 송수신량 부하             | 구조체 및 클래스 인코딩/디코딩      |
| Photon Fusion 2 마이그레이션  | OnChanged -> Render 등 미지원 기능 수정      |

---

## ⚖️ 라이선스 & 리소스

* **코드** : MIT
* **아트·사운드** : CC‑BY‑SA 3.0, 직접 제작, Photon Fusion(Demo Assets)
* 제3자 라이브러리: Photon Fusion 2 Free

---

## 📫 문의

|            |                                                         |
| ---------- | ------------------------------------------------------- |
| ✉ Email    | [garrettales@gmail.com](mailto:garrettales@gmail.com) |
| 📝 Blog    | [https://lifebalance-archive.tistory.com/](https://lifebalance-archive.tistory.com/)          |
