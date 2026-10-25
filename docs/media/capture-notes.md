# README 미디어 촬영 기록

[사용자 README](../../README.md)

## 환경과 출처

- 촬영일: 2026-10-25
- Unity: **2022.3.22f1**, Windows, Direct3D 11, 에디터 UI 배율 100%
- 패키지: TextureCocktail **0.0.10**, LuticaLab Core **0.0.3**
- 화면 언어: English. README에서는 필요한 항목에 한국어 이름도 병기합니다.
- 이미지 예제: 저장소의 [`Assets/rety.png`](../../Assets/rety.png). 내용을 보기 쉽게 원본 좌표 `(0, 500)`에서 `1024 × 1024` 영역을 잘라 `768 × 768`로 축소한 임시 PNG를 사용했습니다. 원본 파일은 변경하지 않았습니다.
- 메시 예제: [생성기](../examples/MeshVisibilityDemo.cs)로 만든 두꺼운 외벽, 열린 출입구, 내부 상자가 있는 작은 작업실 건물입니다. 외부 모델이나 텍스처가 필요하지 않습니다. 예제 머티리얼은 **Built-in Render Pipeline의 Standard 셰이더**를 사용합니다.

모든 화면은 실행 중인 Unity의 실제 TextureCocktail 창을 캡처했습니다. 임시 에디터 스크립트로 실제 프리셋 처리 메서드, A/B 슬라이더 값, Scene 뷰 회전과 X-ray 설정을 적용한 뒤 `InternalEditorUtility.ReadScreenPixel`로 기록했습니다. GIF는 **자동 진행한 실제 기능 시연**이며 마우스 조작을 녹화한 영상은 아닙니다. UI나 결과 이미지를 다시 그리거나 합성하지 않았습니다.

텍스처 GIF는 6 fps, 메시 GIF는 10 fps이며 256색 팔레트로 변환했습니다. GIF의 색상은 팔레트 변환 때문에 PNG나 Unity 원본과 조금 다를 수 있습니다. 임시 캡처 코드는 제품 패키지에 포함되지 않으며, 건물 생성기는 재현용 소스로 별도 제공합니다.

## 화면별 내용

| 파일 | 실제로 보여주는 동작 |
| --- | --- |
| [`texture-editor.png`](texture-editor.png) | Fast Image Converter에서 **Brighten**을 적용한 편집 창. 밝기 0.2, 대비 1.1, 채도 1.05, 감마 1. |
| [`texture-workflow.gif`](texture-workflow.gif) | **Reset → Brighten → Vibrant → Desaturate → Reset → Brighten** 프리셋에 따라 슬라이더와 미리보기가 바뀌는 모습. |
| [`ab-comparison.gif`](ab-comparison.gif) | Brighten 결과를 스냅샷으로 열고 **Split Slider**의 위치를 움직여 Before와 After 비교. |
| [`mesh-visibility.png`](mesh-visibility.png) | 작업실 건물을 **Current Scene View**, **Balanced (4 samples)**로 분석한 결과. 가려짐 후보만 표시하고 X-ray를 켠 화면. |
| [`mesh-visibility.gif`](mesh-visibility.gif) | 건물 주위를 360도 회전하며, 각 시점에서 다시 분석한 가려짐 후보를 확인하는 시연. |

메시 GIF는 135프레임·13.5초입니다. 처음에는 후보 표시를 끈 건물 외형과 출입구 내부를 보여주고, 이어서 후보 표시와 X-ray를 켜고 360도 회전합니다. 후면에서 1.5초 동안 후보 표시를 꺼 막힌 뒷벽의 외형과 비교한 뒤 회전을 마무리합니다. 촬영 스크립트가 Scene 뷰를 360도 회전시키며 시점마다 **Analyze Current Pose**와 같은 분석을 호출했습니다. 제품에 자동 360도 분석 기능이 추가된 것은 아니며, 사용자가 시점을 바꿀 때는 분석 버튼을 다시 눌러야 합니다.

## 메시 예제 재현

1. Built-in Render Pipeline을 사용하는 Unity 프로젝트에 TextureCocktail과 Core를 설치합니다.
2. [MeshVisibilityDemo.cs](../examples/MeshVisibilityDemo.cs)를 다운로드하거나 저장소에서 복사해 프로젝트의 `Assets/Editor/MeshVisibilityDemo.cs`에 둡니다. `Editor` 폴더가 없다면 만듭니다.
3. 현재 이름 없는 **Untitled** 씬이 열려 있다면 먼저 저장합니다. 컴파일이 끝나면 **LuticaLab → Examples → Create Mesh Visibility Demo**를 실행합니다.
4. 새 씬의 **Workshop visibility demo** 루트를 선택하고 **LuticaLab → TextureCocktail Mesh Visibility**를 엽니다.
5. **View Source: Current Scene View**, **Surface Sampling: Balanced**를 선택합니다. 다른 씬 메시의 가림과 양면 처리는 끕니다.
6. 열린 출입구가 있는 건물 정면을 보며 **Analyze Current Pose**를 실행합니다. 후보 표시는 **Occluded**만 켜고 **X-ray**를 전환해 벽체 속 구조물과 실내 상자를 살펴봅니다.
7. Scene 뷰를 옆과 뒤로 돌리고, 시점을 바꿀 때마다 **Analyze Current Pose**를 다시 실행합니다. 정면 출입구로 보이던 상자가 뒤에서는 벽에 가려지는지 비교합니다. 실내를 사용하는 월드에서는 내부 시점도 확인하세요.

생성기는 기존에 열린 씬을 유지하면서 별도의 씬을 추가하고 활성화합니다. Unity 2022.3에서 이름 없는 씬이 열려 있으면 먼저 저장하라는 안내를 표시하며, 사용자의 씬을 자동으로 저장하거나 닫지 않습니다. 머티리얼과 메시 파일은 `Assets/TextureCocktailVisibilityDemo/Example` 아래 저장하며, 다시 실행하면 `Example 1`처럼 고유한 폴더를 만들어 기존 에셋을 덮어쓰지 않습니다. 예제를 유지하려면 새 씬도 저장하세요. 생성한 오브젝트는 Undo로 되돌릴 수 있지만, 생성된 에셋 폴더는 Undo로 삭제되지 않습니다.

| 결과 목록 | 확인할 내용 |
| --- | --- |
| `01 Wall-cavity supports` | 두꺼운 벽체 속 기둥·보. 벽에 가려진 구조물 후보를 확인합니다. |
| `02 Exterior walls` | 건물 외벽. 정면은 출입구가 열려 있고 뒤쪽은 막혀 있습니다. |
| `03 Interior crates` | 정면 출입구를 통해 보이지만 뒤에서는 벽에 가려지는 내부 상자. 한 시점의 가려짐만으로 삭제 여부를 판단하면 안 되는 예입니다. |
| `04 Roof and trim` | 건물 지붕과 처마. |
| `05 Door frame and steps` | 출입구 문틀과 계단. |
| `06 Floor and foundation` | 실내 바닥과 기초. |

열린 출입구가 있는 정면은 -Z, 막힌 뒷벽은 +Z입니다. GIF에서는 원본 오브젝트를 회전시키지 않고 Scene 뷰를 회전시킵니다. 결과는 각 시점의 샘플링 결과이며, 모든 각도에서 삭제 가능한 면을 합산하거나 확정한 결과가 아닙니다. 실제 월드에서 실내에 들어갈 수 있거나 문·벽의 배치가 바뀐다면 그 상태에서도 다시 확인하세요.

실제 촬영에서 0°부터 360°까지의 카메라 각도를 검증했습니다. 이 예제는 6 Renderer·400개 삼각형으로 구성되며, 벽 속 기둥·보는 촬영한 모든 시점에서 Visible 0개였습니다. 내부 상자는 정면에서 Visible 24개, 후면에서 0개였습니다. 이 숫자는 예제의 샘플링 결과이며 삭제 가능한 삼각형 수를 뜻하지 않습니다.
