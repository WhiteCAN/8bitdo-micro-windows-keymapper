# MicroKey Studio

**한국어** | [English](README.en.md)

> [!CAUTION]
> **주의: 이 프로그램은 충분히 검증된 공식 소프트웨어가 아닙니다.**
>
> 이 프로젝트는 OpenAI Codex를 활용한 바이브 코딩 방식으로만 개발되었습니다. 개발자가 전체 소스 코드를 줄 단위로 수동 검토하지 않았으며, 독립적인 보안 감사와 배포 파일의 백신·악성코드 검사를 완료하지 않았습니다. 자동 테스트 통과와 SHA-256 제공은 프로그램의 안전성을 보증하지 않습니다.
>
> 이 프로그램은 기기 설정을 직접 읽고 씁니다. 예상치 못한 오작동이나 설정 손실이 발생할 수 있으므로 테스트용 매핑부터 사용하고, 공식 모바일 앱으로 복구할 수 있도록 준비한 뒤 사용자 책임으로 실행하세요.

MicroKey Studio는 8BitDo Micro를 위한 비공식 Windows 키매핑 도구입니다.

이 프로젝트는 C#/.NET 8과 WPF로 개발합니다. 이 preview는 기기 설정 읽기, PC 로컬 프로필 편집, 백업 및 다시 읽기 보호 장치가 있는 현재 연결 기기 저장을 지원합니다. 기기 저장은 실기기에서 확인했고 복원의 실기기 최종 검증은 남아 있습니다.

## 다운로드

[MicroKey Studio 0.1.0-preview.31 Windows x64 포터블 ZIP 다운로드](https://github.com/WhiteCAN/8bitdo-micro-windows-keymapper/releases/download/v0.1.0-preview.31/MicroKeyStudio-v0.1.0-preview.31-2026-08-19-win-x64-portable.zip)

설치할 필요 없이 ZIP 압축을 풀고 `MicroKeyStudio.App.exe`를 실행하면 됩니다. Windows SmartScreen 경고가 표시될 수 있습니다.

- [릴리스 정보](https://github.com/WhiteCAN/8bitdo-micro-windows-keymapper/releases/tag/v0.1.0-preview.31)
- SHA-256: `3766AA0ADB47E9C1B33375F69687A8BF6A906AC88E0ED2F235FD41BDB37762DF`

## 현재 상태

현재 버전: `0.1.0-preview.31`, 릴리스 날짜: `2026-08-19`.

초기 개발 단계입니다. 동작, 조건부 동작, PC 로컬 전용, 미구현 기능은 [feature-status.ko.md](docs/feature-status.ko.md), 버전별 변경 사항은 [CHANGELOG.ko.md](CHANGELOG.ko.md)를 확인하세요.

## 안전 안내

이 프로젝트는 실험적인 커뮤니티 프로젝트입니다. 지원되지 않는 설정 데이터를 기기에 쓰면 동작이 바뀔 수 있습니다. 먼저 복구 가능한 테스트 매핑으로 확인하고, 필요할 때 공식 모바일 앱으로 복구하거나 초기화할 수 있게 준비해 주세요.

- 기기 저장은 새 설정을 먼저 읽고, 변경하는 매핑만 수정하는 read-modify-write 방식으로 나머지 설정 바이트를 보존합니다.
- 저장 직전 설정은 확인 대화상자보다 먼저 `Documents/MicroKeyStudio/backups`에 백업합니다. 복원은 가장 최근의 유효한 백업을 사용하며 공장 초기화가 아닙니다.
- 저장과 복원 모두 새 전체 설정 다시 읽기를 요구합니다. 저장은 읽기가 불완전하거나 요청한 매핑과 다르면 성공으로 처리하지 않으며, 복원은 전체 다시 읽기 결과가 백업과 바이트 단위로 같아야 합니다.
- 기기 쓰기는 현재 연결된 기기의 활성 설정에 적용합니다. 기기 쪽 프로필 이름과 프로필 선택은 아직 해석되지 않아 기기 프로필을 선택할 수 없습니다.

## UI 기능

- 한국어와 English 언어 선택.
- 기기 찾기 후 프로필 목록을 채우고, 선택한 프로필을 불러오는 흐름.
- 기기 찾기, 프로필 선택기, 프로필 개수, 프로필 불러오기 컨트롤을 한 영역에 배치.
- 로컬 프로필 추가, 이름 변경, 삭제, 불러오기 컨트롤.
- 기기 읽기는 임시 이름의 기기 저장 프로필을 업데이트합니다. 프로필명 디코딩은 아직 확인되지 않았습니다.
- 로컬 프로필은 사용자 문서 폴더의 `MicroKeyStudio/profiles.json`에 저장됩니다.
- 매핑 칩 클릭 후 선택한 버튼 액션을 수정하는 편집 패널.
- 공식 앱과 비슷한 키 선택 카테고리와 `Ctrl+C` 같은 동시 키 자유 입력칸.
- 이미지 위 연결선과 버튼 중심 종점으로 각 매핑 행에 해당하는 물리 버튼을 정확하게 표시합니다.
- 기기에서 읽은 `슬립 비활성화` 상태 표시와 안전한 저장·재읽기 검증.
- 저장 전 자동 백업과 저장 후 다시 읽기 검증.

## 공개하면 안 되는 로컬 파일

다음 파일은 문제 분석에 유용하지만 기기 설정이나 사용자 정의 이름을 포함할 수 있으므로 GitHub 저장소, Issue, Discussion에 원본 그대로 올리지 마세요.

- `Documents/MicroKeyStudio/last-load-diagnostics.txt`: 기기 이름, 프로필 이름, 원시 알림 패킷이 포함될 수 있습니다.
- `Documents/MicroKeyStudio/backups/micro-config-backup-*.json`: 기기의 전체 180바이트 설정 백업입니다.
- `Documents/MicroKeyStudio/profiles.json`: 사용자가 만든 로컬 프로필 이름과 키 매핑입니다.
- Android 버그 리포트, HCI/BLE 로그, APK, 디컴파일 결과, 휴대폰 또는 Bluetooth 식별자.

오류를 공유할 때는 상태 문구만 복사하고 기기 이름, 개인 경로, 원시 HEX 값은 제거하세요.

## Asset

앱에서 사용하는 제품 이미지 출처는 [docs/asset-sources.ko.md](docs/asset-sources.ko.md)에 기록합니다.
