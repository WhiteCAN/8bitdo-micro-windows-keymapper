# MicroKey Studio 설계 문서

작성일: 2026-08-16

## 요약

MicroKey Studio는 8BitDo Micro 컨트롤러의 기기 내부 키매핑을 설정하기 위한 C#/.NET 8 WPF Windows 프로그램이다. 이 프로젝트는 오픈소스 공개와 포트폴리오 활용을 함께 목표로 한다. 일반 Windows 사용자도 사용할 수 있는 앱을 만들고, 저장소에는 AI를 활용한 BLE 프로토콜 분석 과정을 문서화한다.

이 앱은 PC에서만 입력을 바꾸는 리매퍼가 아니라, 8BitDo Micro 기기 자체에 설정을 저장하는 도구를 목표로 한다. MicroKey Studio에서 쓴 매핑은 앱을 종료한 뒤에도 기기에 남아 있어야 하며, 공식 모바일 앱의 저장 방식과 같은 결과를 내는 것이 목표다.

## 목표

- 8BitDo Micro 키매핑을 위한 완성도 있는 Windows UI를 제공한다.
- Windows BLE API를 사용해 8BitDo Micro에 연결한다.
- 프로토콜 지원이 가능한 범위에서 현재 기기 설정을 읽는다.
- 편집한 매핑을 기기에 다시 쓴다.
- 장기적으로 공식 앱 수준의 범위를 지원한다: 키보드 키, 단축키, 마우스 동작, 미디어 키, 프로필, 매크로 편집, 초기화, 가져오기/내보내기.
- 프로토콜 분석, 패킷 캡처, 구현 노트를 잘 문서화한다.
- 개인 로그, APK, 로컬 기기 식별자, 폰 관련 개인 정보는 커밋하지 않는다.

## 비목표

- 첫 공개 버전에서 모든 8BitDo 컨트롤러를 지원하지 않는다.
- 펌웨어 업데이트 도구를 대체하지 않는다.
- 추출한 공식 APK, proprietary 바이너리, 원본 개인 로그를 공개 저장소에 포함하지 않는다.
- PC에서만 동작하는 입력 리매핑을 주요 기능으로 삼지 않는다.

## 기술 기준점

로컬 분석을 통해 공식 앱 패키지가 `com.abitdo.advance`임을 확인했다. Android 앱은 `libadvance-lib.so`라는 네이티브 라이브러리에서 Micro 설정 패킷을 만든 뒤, `SHBLEUtils.writeBLE(byte[])`를 통해 해당 바이트를 전송한다.

공개 문서에 남길 수 있는 관찰된 프로토콜 정보:

- 관찰된 기기 계열/이름: 8BitDo Micro BLE 앱 모드, `80EL` 같은 advertising name 패턴.
- Primary GATT service: `0000FF10-0000-1000-8000-00805F9B34FB`.
- Write/notify characteristic: `0000FF13-0000-1000-8000-00805F9B34FB`.
- 공식 앱 로그에는 실제 write payload를 보여주는 `writeBleHidData:` 항목이 나온다.
- `MicroUI`는 20개 버튼 매핑 배열을 만들며, 각 논리 매핑 항목은 4개의 정수형 슬롯을 사용한다.
- `writeMicroCustomConfig`, `readMicroCustomConfig`, `setMicroReportEnable`, `readVersion_Micro` 같은 네이티브 함수가 설정 흐름을 조정한다.

위 정보는 개인 계정 비밀값이 아니라 관찰된 프로토콜 동작이므로 공개 문서에 포함할 수 있다. 원본 로그와 전체 APK 추출물은 로컬에만 둔다.

## 민감/비공개 데이터 처리

커밋하지 않을 항목:

- 원본 APK 파일 또는 추출한 APK 트리.
- 원본 `adb logcat` 캡처.
- 원본 Bluetooth HCI 캡처.
- 폰 시리얼 번호.
- Bluetooth MAC 주소.
- 사용자 PC의 개인 파일 경로.
- `docs/private/` 아래의 모든 파일.

공개 문서에 포함할 수 있는 항목:

- 정리된 BLE UUID.
- 기기 주소를 제거한 sanitized 패킷 예시.
- 높은 수준의 분석 노트.
- 공개 도구를 사용한 재현 가능한 분석 절차.

개발 중 필요한 정확한 캡처 값은 비공개 로컬 문서에 저장할 수 있지만, git에는 포함하지 않는다.

## 아키텍처

솔루션은 C#/.NET 8과 WPF를 사용한다.

```text
MicroKeyStudio/
  src/
    MicroKeyStudio.App/
    MicroKeyStudio.Ble/
    MicroKeyStudio.Protocol/
    MicroKeyStudio.Storage/
  tests/
    MicroKeyStudio.Protocol.Tests/
    MicroKeyStudio.Storage.Tests/
  docs/
    protocol-notes.md
    capture-analysis.md
    superpowers/specs/
  tools/
    LogcatParser/
```

### MicroKeyStudio.App

MVVM 구조의 WPF 애플리케이션이다. 화면, 다이얼로그, 명령, 검증 메시지, 사용자 상호작용 상태를 담당한다. 프로토콜 바이트 생성이나 Windows BLE API 호출을 직접 수행하지 않는다.

주요 화면:

- 기기 연결 화면: 검색, 연결, 연결 해제, 신호/상태, 확인 가능한 경우 펌웨어/버전 표시.
- 매핑 편집기: Micro 버튼 목록, 선택한 동작 편집기, 충돌 표시.
- 프로필 관리자: 로컬 프로필 저장/불러오기, 복제, 이름 변경, 가져오기/내보내기.
- 매크로 편집기: 단계 목록, 지연, 키 down/up 이벤트, 검증.
- 프로토콜/디버그 패널: 패킷 추적과 실험적 read/write 흐름을 위한 선택적 개발자 화면.
- 설정/about: 안전 고지, 프로젝트 링크, 감사 표시, 진단 내보내기.

### MicroKeyStudio.Ble

Windows BLE 계층이다. `Windows.Devices.Bluetooth`와 `Windows.Devices.Bluetooth.GenericAttributeProfile`을 감싼다.

책임:

- 후보 Micro 기기를 검색한다.
- device id 또는 BLE 주소로 연결한다.
- 설정된 service/characteristic UUID를 찾는다.
- notification을 구독한다.
- 바이트 payload를 순서대로 쓰고, pacing과 retry 정책을 적용한다.
- 구조화된 연결 이벤트를 앱 계층으로 전달한다.

BLE 계층은 테스트에서 fake transport로 대체할 수 있어야 한다.

### MicroKeyStudio.Protocol

프로토콜과 매핑 모델을 담당한다. 가장 중요한 경계다.

책임:

- Micro 버튼과 대상 동작을 표현한다.
- UI 매핑을 프로토콜 record로 변환한다.
- 알려진 응답을 가능한 범위에서 파싱한다.
- read, write, save, report-enable, reset 패킷을 생성한다.
- 테스트용 캡처 패킷 fixture는 sanitized 형태로 보관한다.

초기 구현은 실험 플래그 뒤에서 이미 캡처한 known-good 시퀀스를 replay할 수 있다. 패킷 포맷이 더 해석되면 replay 방식은 생성 방식으로 대체한다.

### MicroKeyStudio.Storage

로컬 프로필 저장을 담당한다.

책임:

- 프로필을 사람이 읽을 수 있는 JSON으로 저장한다.
- 프로필 스키마 버전을 관리한다.
- 가져온 프로필 파일을 검증한다.
- 사용자가 명시적으로 진단 내보내기를 하지 않는 한, 프로토콜 raw bytes를 사용자 프로필 파일에 저장하지 않는다.

## 데이터 모델

핵심 개념:

- `MicroButton`: 기기의 물리 버튼.
- `MappedAction`: 대상 동작. 키보드 키, 키 조합, 마우스 동작, 미디어 키, 매크로, 비활성화, pass-through를 포함한다.
- `MacroStep`: key down, key up, 마우스 동작, 지연, 향후 지원 가능한 텍스트 입력 확장.
- `MappingProfile`: 이름이 있는 버튼 매핑 컬렉션과 메타데이터.
- `DeviceSession`: 현재 BLE 연결, 발견된 기능, 펌웨어/버전, 동기화 상태.

프로필은 GitHub 사용자들이 preset을 공유할 수 있도록 사람이 읽기 쉬운 JSON이어야 한다.

## 프로토콜 흐름

기기 write는 상태 머신으로 다룬다.

1. BLE 기기에 연결한다.
2. Windows에서 지원하는 범위에서 MTU를 설정하거나, 협상된 크기에 맞춰 write chunking을 조정한다.
3. service와 characteristic UUID를 찾는다.
4. notification을 구독한다.
5. Micro report를 활성화한다.
6. 가능하면 현재 설정을 읽는다.
7. 매핑 데이터 frame을 쓴다.
8. save/commit 명령을 보낸다.
9. acknowledgement 또는 timeout을 기다린다.
10. 성공, 부분 성공, 실패를 보고한다.

공식 앱은 retry를 수행하며, 일부 write failure 후에도 나중에 성공 응답을 받는 동작이 관찰되었다. Windows 구현은 이를 감안해 write를 직렬화하고, 이해 가능한 acknowledgement를 기다리며, 자세한 진단 정보를 제공해야 한다.

## 오류 처리

사용자에게 보여주는 오류는 구체적이고 바로 행동 가능한 형태여야 한다.

- 기기를 찾을 수 없음.
- 기기는 찾았지만 service/characteristic이 없음.
- notification을 사용할 수 없음.
- write timeout.
- 기기가 설정을 거부함.
- 선택한 동작 타입에 대한 프로토콜 지원이 아직 불완전함.

진단 로그에는 sanitized packet direction, command id, 길이, 상태를 포함하되, 기본적으로 로컬 private device identifier는 포함하지 않는다.

## 테스트

프로토콜 테스트:

- 알려진 packet fixture가 예상 바이트를 생성하는지 확인한다.
- 매핑 entry가 일관되게 encode되는지 확인한다.
- 잘못된 매핑은 검증에서 실패해야 한다.
- 프로필 스키마 migration이 동작해야 한다.

BLE 테스트:

- fake transport가 write 순서를 기록한다.
- retry와 timeout 동작이 결정적이어야 한다.
- notification 파서는 알 수 없는 패킷을 안전하게 처리해야 한다.

앱 테스트:

- ViewModel이 불완전한 매핑을 검증한다.
- 프로필 가져오기/내보내기가 round-trip된다.
- 기기가 연결되지 않았을 때 save 명령이 비활성화된다.

수동 하드웨어 검증:

- 실제 8BitDo Micro에 연결한다.
- 지원 가능한 경우 version/config를 읽는다.
- known-good 매핑 하나를 쓴다.
- 연결 해제/재연결 후 매핑이 유지되는지 확인한다.
- reset이 기본 동작을 복원하는지 확인한다.

## 공개 로드맵

Phase 1: 프로젝트 scaffold와 BLE 연결.

Phase 2: 개발자 전용 화면에서 캡처된 known-good write 시퀀스 replay.

Phase 3: 키보드/마우스/미디어 매핑 record를 해석하고 패킷을 생성.

Phase 4: 프로필 편집기와 로컬 JSON 프로필 저장.

Phase 5: 매크로 편집기.

Phase 6: reset/readback 지원과 release packaging.

Phase 7: 문서 polish, 스크린샷, 가능하면 signed release artifact.

## GitHub 공개 방향

README에서 강조할 내용:

- 8BitDo Micro용 Windows 키매핑 도구.
- 비공식 커뮤니티 연구 프로젝트.
- C#/.NET 8 WPF 앱.
- sanitized 로그와 APK 분석을 바탕으로 한 BLE 프로토콜 연구.
- AI-assisted 개발 workflow.
- 안전 고지: 실험적 write는 기기 설정을 바꿀 수 있음.

저장소에는 sanitized 예시만 포함한다. 정확한 로컬 캡처는 ignored private notes에만 둔다.

## 열린 질문

- 생성된 매핑 frame의 정확한 checksum/hash 필드.
- 키보드, 마우스, 미디어, 매크로 전체 action code table.
- 이 기기에서 Windows BLE write 동작에 명시적 chunking이 필요한지.
- 모든 8BitDo Micro 펌웨어 버전에서 같은 UUID가 적용되는지.
- readback이 write-only configuration 대비 얼마나 안정적인지.

## 승인

이 설계는 앱 이름을 `MicroKey Studio`, 저장소 이름을 `microkey-studio`, 구현 언어를 C# only로 가정한다.
