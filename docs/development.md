# 개발 및 배포

[사용자 README로 돌아가기](../README.md)

## 프로젝트 열기

저장소를 clone한 뒤 Unity Hub에서 저장소 루트를 프로젝트로 엽니다. 프로젝트 버전은 `ProjectSettings/ProjectVersion.txt`를 기준으로 합니다. 변경 사항과 검증 결과를 설명하는 Pull Request로 기여할 수 있습니다.

## README 번역 관리

README 상단의 언어 링크로 문서를 전환합니다. 기본 문서는 한국어이며, 중국어는 간체를 사용합니다.

| 언어 | 문서 |
| --- | --- |
| 한국어 | [README.md](../README.md) |
| English | [README.en.md](../README.en.md) |
| 日本語 | [README.ja.md](../README.ja.md) |
| 简体中文 | [README.zh-CN.md](../README.zh-CN.md) |

사용자 기능·설치 방법·주의 사항을 바꿀 때는 네 문서를 함께 갱신합니다. 실제 메뉴·버튼 이름, 파일 경로, 명령은 유지하고 설명과 이미지 대체 텍스트를 번역합니다. 이미지와 GIF는 `docs/media`의 같은 파일을 참조합니다. 제목을 번역하면 목차 앵커도 확인하고, 다른 언어로 된 참고 문서에는 링크 옆에 해당 언어를 표시합니다. README의 언어 선택과 앱 UI의 지원 언어는 별개입니다.

## 셰이더와 커스텀 편집기 추가

- `Shader`를 만들고, 필요하면 `TextureCocktailContent`를 상속한 편집기를 제공합니다.
- `[TextureCocktailShader("Hidden/MyShader")]`로 편집기를 등록합니다. 레지스트리는 실제 클래스 타입을 연결하므로 클래스명·네임스페이스·어셈블리는 셰이더명과 달라도 됩니다.
- 셰이더 속성이 없는 편집기는 플러그인 레지스트리의 호환 경로를 사용합니다. 셰이더의 basename과 클래스명이 일치하면 다른 네임스페이스나 로드된 어셈블리에서도 찾습니다. 명시적인 셰이더 속성 연결이 우선합니다.
- `TextureCocktailShaderPackage` 에셋은 선택기의 메타데이터를 덮어쓸 수 있으며, 속성으로 등록한 편집기 연결은 유지됩니다.
- `OnGUI`와 `OnShaderValueChanged`를 재정의합니다. 호스트가 `Initialize`를 호출하고 편집기 인스턴스의 수명을 관리합니다. `UseDefaultLayout`과 `OnValuepdate`는 호환용 멤버이며 호스트에서 호출하지 않습니다.
- 새 렌더 요청에는 `baseWindow.RenderPreview()`를 사용합니다. 호스트의 값 변경 콜백 안에서 요청하면 콜백 이후 한 번의 렌더로 합쳐집니다. 기존 `CompileShader()`는 콜백 안에서도 동기적으로 실행됩니다.
- 속성 범위는 셰이더가 정의합니다. 내장 숫자 컨트롤은 `ShaderParameterGUI`를 사용하며, 모드는 셰이더 키워드나 이름 있는 패스에 명시적으로 연결합니다.

상세 예제는 [플러그인 가이드](../Packages/luticalab.texturecocktail/PLUGIN_GUIDE.md)를 참고하세요.

## 코드 구조

| 구성 요소 | 담당 |
| --- | --- |
| `TextureCocktail` | 콘텐츠 UI, 셰이더 선택, 미리보기 갱신 조정 |
| `TexturePreviewRenderer` | 미리보기 렌더링 |
| `TextureExportUtility` | PNG 인코딩과 저장 |
| `PolygonMaskEditor` | 폴리곤 상호작용과 마스크 텍스처 수명 관리. 명령이 소유 창에 변경을 알림 |
| `MeshVisibilitySnapshot` | 현재 포즈의 형상을 캡처 |
| `MeshVisibilityDirections` | ±XYZ와 대각선을 포함하는 26·98·386개 중첩 검사 방향 |
| `MeshVisibilityAnalyzer` | 단일 시점 분류 및 여러 방향의 가시성 합산. 한 방향에서라도 보인 면은 후보에서 제외 |
| `MeshVisibilityOverlay` / `MeshVisibilityWindow` | Scene 뷰 후보 표시와 분석 UI |

## 회귀 검사

픽스처는 `Packages/luticalab.texturecocktail/Tests~`에 있어 일반 셰이더 선택기에 나타나지 않습니다. **대상 프로젝트를 Unity에서 닫은 뒤**, 프로젝트 버전과 맞는 Editor 경로로 실행합니다.

```powershell
.\Packages\luticalab.texturecocktail\Tests~\Run-RegressionChecks.ps1 -UnityPath 'C:\Program Files\Unity\Hub\Editor\2022.3.22f1\Editor\Unity.exe'
```

실행기는 픽스처를 고유한 임시 Assets 폴더로 복사하고 Unity 배치 모드로 검사한 뒤 그 폴더만 제거합니다. 픽셀·리드백 검사가 있으므로 그래픽 기능을 사용할 수 있는 Editor가 필요합니다. 로그는 `Logs/TextureCocktailRegressionChecks.log`에 남습니다.

릴리스 자동화 검사는 저장소 루트에서 실행합니다.

```bash
python3 -m unittest discover -s .github/scripts -p 'test_*.py' -v
```

## Release a new version

GitHub의 **Actions → Build Release → Run workflow**에서 브랜치 **main**을 선택해 실행합니다. 기본 **patch**는 마지막 릴리스의 patch 번호를 올립니다(예: `0.0.9 → 0.0.10`). 필요하면 **minor** 또는 **major**를 선택합니다. 버전이나 태그를 수동으로 만들 필요는 없습니다. `package.json`에 선택한 증가 폭을 만족하는 더 높은 버전을 미리 지정했다면 그 버전을 유지합니다.

워크플로는 패키지 버전 커밋과 대응 태그를 함께 생성합니다. Core 내용이 바뀌지 않았다면 Core 버전을 유지하고, 바뀌었다면 미리 준비한 상위 버전을 사용하거나 patch를 올립니다. TextureCocktail의 두 의존성 선언과 embedded package lock도 동기화합니다.

ZIP과 UnityPackage는 태그가 가리키는 커밋에서 생성합니다. 파일을 draft 릴리스에 올리고 모두 검증한 뒤 공개하며, 성공한 릴리스는 VCC 목록을 자동 갱신합니다. GitHub의 초안·업로드·공개 메타데이터 반영 지연에는 제한된 조회 재시도를 적용합니다.

### 실패한 배포 재시도

같은 배포를 다시 실행하려면 **Re-run failed jobs** 또는 **Re-run all jobs**를 사용합니다. 실행 ID로 원래 버전과 태그를 복구하며, 이미 공개된 릴리스는 검증하고 파일을 유지합니다.

**새 Run workflow는 새 버전 배포 요청**입니다. 새 실행이 버전을 준비하기 전에 main이 바뀌면 main을 덮어쓰지 않고 중단합니다. 새 커밋을 확인한 뒤 새 실행을 시작하세요.

관련 Pull Request에서 릴리스 자동화 검사가 실행됩니다. 주요 워크플로는 [release.yml](../.github/workflows/release.yml), [release-checks.yml](../.github/workflows/release-checks.yml), [build-listing.yml](../.github/workflows/build-listing.yml)입니다.

## 향후 검토할 아이디어

기존 로드맵에는 MCP/A2A 이미지 편집기와 기능별 recipe가 포함되어 있습니다. 이는 계획 항목이며 현재 제공되는 기능을 뜻하지 않습니다.
