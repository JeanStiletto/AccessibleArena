# Accessible Arena

시각 장애인 플레이어가 스크린 리더를 사용하여 플레이할 수 있게 해주는 Magic: The Gathering Arena 접근성 모드. 완전한 키보드 내비게이션, 모든 게임 상태에 대한 스크린 리더 안내, 12개 언어 지원.

**상태:** 공개 베타. 핵심 게임플레이는 작동합니다. 일부 특수한 경우와 사소한 버그가 남아있습니다. 아래 알려진 문제를 참조하세요.

**참고:** 현재 키보드만 지원합니다. 마우스나 터치 지원은 없습니다. Windows 11과 NVDA에서만 테스트되었습니다. 다른 Windows 버전 및 스크린 리더(JAWS, Narrator 등)도 작동할 수 있지만 테스트되지 않았습니다.

## 기능

- 모든 화면에서 완전한 키보드 내비게이션 (홈, 상점, 마스터리, 덱 빌더, 대전)
- Tolk 라이브러리를 통한 스크린 리더 통합
- 방향키로 카드 정보 읽기 (이름, 마나 비용, 유형, 공격력/방어력, 규칙 텍스트, 플레이버 텍스트, 희귀도, 아티스트)
- 완전한 대전 지원: 구역 내비게이션, 전투, 타겟 선택, 스택, 브라우저 (점술, 감시, 멀리건)
- 부착 및 전투 관계 안내 (마법부여 대상, 방어, 타겟 대상)
- 구매 옵션 및 결제 대화상자를 지원하는 접근 가능한 상점
- 연습을 위한 봇 매치 지원
- 어디서나 사용 가능한 설정 메뉴(F2)와 도움말 메뉴(F1)
- 12개 언어: 영어, 독일어, 프랑스어, 스페인어, 이탈리아어, 포르투갈어(BR), 일본어, 한국어, 러시아어, 폴란드어, 중국어 간체, 중국어 번체

## 요구사항

- Windows 10 이상
- Magic: The Gathering Arena (공식 설치 프로그램 또는 Epic Games Store를 통해 설치)
- 스크린 리더 (NVDA 권장: https://www.nvaccess.org/download/)
- MelonLoader (설치 프로그램이 자동으로 처리합니다)

## 설치

### 설치 프로그램 사용 (권장)

1. GitHub의 최신 릴리스에서 `AccessibleArenaInstaller.exe`를 다운로드하세요: https://github.com/JeanStiletto/AccessibleArena/releases/latest/download/AccessibleArenaInstaller.exe
2. MTG Arena가 실행 중이면 종료하세요
3. 설치 프로그램을 실행하세요. MTGA 설치를 감지하고, 필요한 경우 MelonLoader를 설치하고, 모드를 배포합니다
4. MTG Arena를 시작하세요. 스크린 리더를 통해 "Accessible Arena v... 시작됨"이 들려야 합니다

### 수동 설치

1. MTGA 폴더에 MelonLoader를 설치하세요 (https://github.com/LavaGang/MelonLoader)
2. 최신 릴리스에서 `AccessibleArena.dll`을 다운로드하세요
3. DLL을 다음 위치에 복사하세요: `C:\Program Files\Wizards of the Coast\MTGA\Mods\`
4. `Tolk.dll`과 `nvdaControllerClient64.dll`이 MTGA 루트 폴더에 있는지 확인하세요
5. MTG Arena를 시작하세요

## 빠른 시작

아직 Wizards 계정이 없다면, 게임 내 등록 화면을 사용하는 대신 https://myaccounts.wizards.com/ 에서 계정을 만들 수 있습니다.

설치 후 MTG Arena를 시작하세요. 모드가 스크린 리더를 통해 현재 화면을 안내합니다.

- 언제든지 **F1**을 눌러 모든 키보드 단축키가 나열된 탐색 가능한 도움말 메뉴를 열 수 있습니다
- **F2**를 눌러 설정 메뉴 (언어, 상세도, 튜토리얼 메시지)를 열 수 있습니다
- **F3**을 눌러 현재 화면 이름을 들을 수 있습니다
- **위/아래 방향키** 또는 **Tab/Shift+Tab**으로 메뉴를 탐색합니다
- **Enter** 또는 **스페이스**로 요소를 활성화합니다
- **Backspace**로 뒤로 갑니다

## 키보드 단축키

### 메뉴

- 위/아래 방향키 (또는 W/S): 항목 탐색
- Tab/Shift+Tab: 항목 탐색 (위/아래 방향키와 동일)
- 좌/우 방향키 (또는 A/D): 캐러셀 및 스테퍼 컨트롤
- Home/End: 첫 번째/마지막 항목으로 이동
- Page Up/Page Down: 컬렉션의 이전/다음 페이지
- Enter/스페이스: 활성화
- Backspace: 뒤로

### 대전 - 구역

- C: 내 손패
- G / Shift+G: 내 무덤 / 상대 무덤
- X / Shift+X: 내 추방 / 상대 추방
- S: 스택
- B / Shift+B: 내 생물 / 상대 생물
- A / Shift+A: 내 대지 / 상대 대지
- R / Shift+R: 내 비생물 / 상대 비생물

### 대전 - 구역 내

- 좌/우: 카드 탐색
- Home/End: 첫 번째/마지막 카드로 이동
- 위/아래 방향키: 카드에 포커스 시 카드 세부정보 읽기
- I: 확장 카드 정보 (키워드 설명, 다른 면)
- Shift+위/아래: 전장 열 전환

### 대전 - 정보

- T: 현재 턴 및 단계
- L: 생명 총합
- V: 플레이어 정보 구역 (좌/우로 플레이어 전환, 위/아래로 속성)
- D / Shift+D: 내 서고 카드 수 / 상대 서고 카드 수
- Shift+C: 상대 손패 카드 수

### 대전 - 행동

- 스페이스: 확인 (우선권 패스, 공격자/방어자 확인, 다음 단계)
- Backspace: 취소 / 거절
- Tab: 타겟 또는 강조된 요소 순환
- Ctrl+Tab: 상대 타겟만 순환
- Enter: 타겟 선택

### 대전 - 브라우저 (점술, 감시, 멀리건)

- Tab: 모든 카드 탐색
- C/D: 위/아래 구역으로 이동
- 좌/우: 구역 내 탐색
- Enter: 카드 배치 전환
- 스페이스: 선택 확인
- Backspace: 취소

### 전역

- F1: 도움말 메뉴
- F2: 설정 메뉴
- F3: 현재 화면 안내
- Ctrl+R: 마지막 안내 반복
- Backspace: 범용 뒤로/닫기/취소

## 버그 보고

버그를 발견하면 GitHub에서 이슈를 열어주세요: https://github.com/JeanStiletto/AccessibleArena/issues

다음 정보를 포함해주세요:

- 버그가 발생했을 때 무엇을 하고 있었는지
- 무엇이 일어날 것으로 예상했는지
- 실제로 무엇이 일어났는지
- 사용 중인 스크린 리더와 버전
- MelonLoader 로그 파일 첨부: `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log`

## 알려진 문제

- 우선권 패스 스페이스 키가 항상 안정적이지 않습니다 (모드가 대체 방법으로 버튼을 직접 클릭합니다)
- 덱 빌더 덱 목록 카드가 이름과 수량만 표시하고 전체 카드 세부정보를 표시하지 않습니다
- PlayBlade 큐 유형 선택 (랭크, 오픈 플레이, Brawl)이 항상 올바른 게임 모드를 설정하지 않을 수 있습니다

전체 목록은 docs/KNOWN_ISSUES.md를 참조하세요.

## 문제 해결

**게임 시작 후 음성 출력 없음**
- MTG Arena를 시작하기 전에 스크린 리더가 실행 중인지 확인하세요
- `Tolk.dll`과 `nvdaControllerClient64.dll`이 MTGA 루트 폴더에 있는지 확인하세요 (설치 프로그램이 자동으로 배치합니다)
- `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log`의 MelonLoader 로그에서 오류를 확인하세요

**시작 시 게임이 충돌하거나 모드가 로드되지 않음**
- MelonLoader가 설치되어 있는지 확인하세요.
- 게임이 최근에 업데이트된 경우 MelonLoader 또는 모드를 다시 설치해야 할 수 있습니다. 설치 프로그램을 다시 실행하세요.
- `AccessibleArena.dll`이 `C:\Program Files\Wizards of the Coast\MTGA\Mods\`에 있는지 확인하세요

**모드가 작동했지만 게임 업데이트 후 중단됨**
- MTG Arena 업데이트가 MelonLoader 파일을 덮어쓸 수 있습니다. 설치 프로그램을 다시 실행하여 MelonLoader와 모드를 모두 다시 설치하세요.
- 게임이 내부 구조를 크게 변경한 경우 모드 업데이트가 필요할 수 있습니다. GitHub에서 새 릴리스를 확인하세요.

**키보드 단축키가 작동하지 않음**
- 게임 창이 포커스되어 있는지 확인하세요 (클릭하거나 Alt+Tab으로 전환)
- F1을 눌러 모드가 활성화되어 있는지 확인하세요. 도움말 메뉴가 들리면 모드가 실행 중입니다.
- 일부 단축키는 특정 상황에서만 작동합니다 (대전 단축키는 대전 중에만)

**잘못된 언어**
- F2를 눌러 설정 메뉴를 열고 Enter로 언어를 전환하세요

## 소스에서 빌드

요구사항: .NET SDK (net472 대상을 지원하는 모든 버전)

```
git clone https://github.com/JeanStiletto/AccessibleArena.git
cd AccessibleArena
dotnet build src/AccessibleArena.csproj
```

빌드된 DLL은 `src/bin/Debug/net472/AccessibleArena.dll`에 있습니다.

게임 어셈블리 참조는 `libs/` 폴더에 있어야 합니다. MTGA 설치에서 다음 DLL을 복사하세요 (`MTGA_Data/Managed/`):
- Assembly-CSharp.dll
- Core.dll
- UnityEngine.dll, UnityEngine.CoreModule.dll, UnityEngine.UI.dll, UnityEngine.UIModule.dll, UnityEngine.InputLegacyModule.dll
- Unity.TextMeshPro.dll, Unity.InputSystem.dll
- Wizards.Arena.Models.dll, Wizards.Arena.Enums.dll, Wizards.Mtga.Metadata.dll, Wizards.Mtga.Interfaces.dll
- ZFBrowser.dll

MelonLoader DLL (`MelonLoader.dll`, `0Harmony.dll`)은 MelonLoader 설치에서 가져옵니다.

## 라이선스

이 프로젝트는 GNU General Public License v3.0에 따라 라이선스됩니다. 자세한 내용은 LICENSE 파일을 참조하세요.

## 링크

- GitHub: https://github.com/JeanStiletto/AccessibleArena
- NVDA 스크린 리더 (권장): https://www.nvaccess.org/download/
- MelonLoader: https://github.com/LavaGang/MelonLoader
- MTG Arena: https://magic.wizards.com/mtgarena
