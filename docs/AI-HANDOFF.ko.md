# AI 에이전트 인수인계

최종 갱신: 2026-08-19

## 목표

MicroKey Studio는 8BitDo Micro 기기에 저장된 현재 키보드 매핑과 슬립 비활성화 값을 읽고 편집하고 저장하고 복원하기 위한 C#/.NET 8 WPF 프로그램입니다. 전체 재읽기 검증을 포함한 기기 저장은 실기기에서 확인했습니다. 다음 목표는 슬립 비활성화 저장을 공식 모바일 앱에서 독립 확인하고 실기기 복원을 검증하는 것입니다.

이 프로젝트는 비공식 커뮤니티 프로젝트입니다. 공식 8BitDo 프로그램인 것처럼 표현하면 안 됩니다.

## 저장소 현재 상태

- 작업 브랜치: `codex/full-editor-device-save`
- 인수인계 시 작업 트리는 깨끗해야 합니다.
- 상세 구현 계획: `docs/superpowers/plans/2026-08-16-full-screen-editor-device-save.md`
- 설계 문서:
  - `docs/superpowers/specs/2026-08-16-key-mapping-editor-design.md`
  - `docs/superpowers/specs/2026-08-16-key-mapping-editor-design.ko.md`
- 프로토콜 근거 문서:
  - `docs/protocol-notes.ko.md`
  - `docs/capture-analysis.ko.md`

다른 브랜치나 생성된 배포 파일에서 작업하지 말고, 먼저 다음을 확인합니다.

```powershell
git branch --show-current
git status --short
dotnet test MicroKeyStudio.sln -c Debug
```

## 완료된 작업

구현 계획의 1-8단계는 완료됐고, 공식 모바일 앱으로 기기 저장 결과도 확인했습니다.

1. CRC 및 HID 코덱
   - CRC-16/ANSI: 다항식 `0xA001`, 초기값 `0xFFFF`.
   - 페이지 CRC는 하위 바이트, 상위 바이트 순서입니다.
   - 문자, 숫자, 기호, 이동 키, 숫자 키패드, F1-F24, 보조키와 동시 입력을 처리합니다.
   - 동시 입력 표준 순서는 `Ctrl`, `Alt`, `Shift`, `Win`, 주 키입니다.

2. 읽기-수정-쓰기 패킷 빌더
   - 기기에서 읽은 180바이트 전체 스냅샷을 기준으로 시작합니다.
   - 알려진 4바이트 버튼 슬롯만 수정합니다.
   - 각 페이지 CRC를 다시 계산하고 `04 01` 페이지 4개와 커밋 패킷을 만듭니다.
   - 복원용 전체 페이로드 패킷 생성과 슬롯별 재읽기 비교를 지원합니다.

3. 비공개 설정 백업 저장소
   - 정확히 180바이트만 엄격한 버전 JSON으로 저장합니다.
   - JSON 필드는 `schema`, `timestamp`, `payload` 세 개뿐입니다.
   - 기기 이름, 주소, 식별자가 포함된 스키마는 거부합니다.

4. 전체 화면 편집 상태 및 실제 키보드 캡처
   - `적용`이 성공하기 전까지 변경값은 초안입니다.
   - `취소`는 기존 행과 프로필 매핑을 보존합니다.
   - 저장 실패 시 메모리 상태를 원래대로 돌리고 편집기와 초안은 유지합니다.
   - 실제 키보드에서 `Ctrl+C`, `Ctrl+Shift+S` 같은 동시 입력을 받을 수 있습니다.
   - `Esc`, `Ctrl+Esc`도 매핑 값이며 편집기를 닫지 않습니다.

5. 전체 화면 WPF UI
   - 좁은 키 선택 패널을 전체 너비 편집 화면으로 교체했습니다.
   - 자유 입력, 취소, 적용은 화면 하단에 계속 보입니다.
   - 키 버튼은 64x48 고정 크기입니다.
   - 공식 모바일 앱 화면을 참고한 실제 키보드 고정 행으로 정렬했으며 F1-F24, 숫자 키패드, 기호, 이동 키, `Win`을 포함합니다.
   - preview.26에서 Shift 기호 21개를 모두 추가해 `!`는 `Shift+1`, `?`는 `Shift+/`처럼 바로 선택할 수 있습니다.
   - 한글과 영문 편집기 문구가 있습니다.
   - 실행 화면에서 실제 `Ctrl+C`와 `Esc` 입력을 확인했습니다.

6. 검증형 기기 저장 및 복원 구현
   - 저장은 엄격한 비공개 백업을 만들고 변경 확인 후 생성한 페이지를 쓰며 새 재읽기 일치를 요구합니다.
   - 복원은 프로그램을 다시 실행해도 가장 최근의 유효한 백업을 찾아 180바이트 전체 재읽기 일치를 요구합니다.
   - 기기 저장은 공식 모바일 앱으로 확인했고, 기기 복원은 연결 상태에서 추가 검증이 필요합니다.

7. 슬립 비활성화 읽기·저장
   - 공식 모바일 앱의 OFF → ON → OFF 반복 캡처에서 전체 오프셋 `0x03`만 바뀌는 것을 확인했습니다.
   - ON은 `0x01`, OFF는 `0x00`이며 OFF 반복 캡처의 180바이트 설정은 완전히 같았습니다.
   - 전체 설정을 읽은 뒤 체크박스를 활성화하고, 키 매핑과 같은 백업·읽기-수정-쓰기·전체 재읽기 검증 흐름으로 저장합니다.

주요 커밋 순서:

```text
a528691 feat: encode Micro keyboard HID mappings
15ffaa7 fix: validate config pages and HID chords
7a9202e feat: build verified Micro save packets
b435347 feat: back up device config before writes
5f7d944 fix: strictly validate config backup documents
b438e3d test: validate missing backup fields as valid JSON
6d7ccb2 feat: add full screen mapping editor state
a58dd4f fix: preserve mapping edits on save failure
4a17578 feat: redesign the key editor workspace
ab284d4 fix: label the unimplemented device sync accurately
```

## 다음 작업

1. PC에서 슬립 비활성화를 ON으로 저장하고 전체 재읽기 성공 메시지를 확인합니다.
2. PC 연결을 끊은 뒤 공식 모바일 앱에서 슬립 비활성화가 ON인지 독립적으로 확인합니다.
3. Micro를 키보드 모드로 연결하고 `최근 백업 복원` 명령을 실행한 뒤 공식 모바일 앱에서 복원된 설정을 확인합니다.
4. 프로필 이름과 프로필 목록 해석은 별도 작업으로 유지합니다. 현재 근거로 버튼 매핑은 기기 로컬이지만 프로필 이름은 아직 해석되지 않았습니다.

## 현재 검증 결과

가장 최근 Release 실행 결과는 다음과 같습니다.

```text
Protocol 테스트: 78개 통과
Storage 테스트: 12개 통과
App 테스트: 93개 통과
전체: 183개 통과
Release 빌드: 경고 0, 오류 0
```

실행 중인 `MicroKeyStudio.App.exe`가 있으면 Debug DLL을 잠글 수 있으므로 먼저 닫고 현재 환경에서 테스트를 다시 실행해야 합니다.

## 알려진 제한과 위험

- 현재는 기기의 활성 매핑만 이해합니다. 기기 내부 프로필 이름과 프로필 추가/삭제/선택은 구현되지 않았습니다.
- 자동화 복원 흐름은 검증됐지만 실기기 복원 확인은 아직 남아 있습니다.
- `MicroConfigSnapshot.TryCreate`에는 중복 페이지 오프셋이 false 대신 예외를 낼 수 있는 사소한 미해결 항목이 있습니다.
- 브랜치 중간 커밋 하나가 내부 작업 보고서를 잠시 추적했고 다음 커밋에서 삭제했습니다. GitHub 공개 전 사용자 작업이 손실되지 않는지 확인한 후 기능 브랜치 이력을 squash 등으로 정리해야 합니다.
- 화면 문구만 보고 프로토콜 바이트를 추측하면 안 됩니다. 코덱, 원본 스냅샷, 재읽기 결과만 사용합니다.

## 개인정보 및 공개 규칙

다음 항목은 절대 커밋하거나 공개하지 않습니다.

- 휴대폰 일련번호 또는 Android 버그 리포트;
- Bluetooth 주소 또는 기기 식별자;
- 원본 HCI/BLE 로그와 패킷 캡처;
- APK, 디컴파일 결과 또는 공식 앱의 독점 콘텐츠;
- 기기 백업 JSON 페이로드;
- 개인 PC의 절대 경로.

저장소는 이미 `docs/private/`, 리버스 엔지니어링 산출물, 백업/빌드 결과, `.superpowers/`, `.worktrees/`를 무시합니다. 커밋 전 항상 확인합니다.

```powershell
git status --short
git diff --cached --name-only
git diff --check
```

## 완료 판정 기준

다음 조건을 모두 충족하기 전에는 저장이나 복원이 작동한다고 말하면 안 됩니다.

1. 성공/실패 경로 자동 테스트가 모두 통과합니다.
2. 첫 쓰기 전에 백업이 생성됩니다.
3. 프로그램이 새로 읽은 값으로 저장 결과를 검증합니다.
4. 공식 모바일 앱에서 통제 변경을 독립 확인합니다.
5. 복원도 재읽기와 공식 앱에서 독립 확인합니다.
6. Release 테스트/빌드와 무설치 패키지 실행 검증이 통과합니다.
