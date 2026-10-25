# README 미디어 촬영 기록

[한국어 사용자 README](../../README.ko.md) · [English README](../../README.md)

## 환경과 출처

- 촬영일: 2026-10-25
- Unity: **2022.3.22f1**, Windows, Direct3D 11, 에디터 UI 배율 100%
- 텍스처·A/B 화면 패키지: TextureCocktail **0.0.10**, LuticaLab Core **0.0.3**. 메시 화면은 모든 방향 검사 기능을 추가한 변경본에서 촬영했습니다.
- 화면 언어: English. README에서는 필요한 항목에 한국어 이름도 병기합니다.
- 이미지 예제: 저장소의 [`Assets/rety.png`](../../Assets/rety.png). 내용을 보기 쉽게 원본 좌표 `(0, 500)`에서 `1024 × 1024` 영역을 잘라 `768 × 768`로 축소한 임시 PNG를 사용했습니다. 원본 파일은 변경하지 않았습니다.
- 메시 예제: [생성기](../examples/MeshVisibilityDemo.cs)로 만든 두꺼운 외벽, 열린 출입구, 내부 상자가 있는 작은 작업실 건물입니다. 외부 모델이나 텍스처가 필요하지 않습니다. 예제 머티리얼은 **Built-in Render Pipeline의 Standard 셰이더**를 사용합니다.

기존 UI 이미지와 GIF는 실행 중인 Unity의 실제 TextureCocktail 창을 캡처했습니다. 임시 에디터 스크립트로 실제 프리셋 처리 메서드, A/B 슬라이더 값, Scene 뷰 회전과 X-ray 설정을 적용한 뒤 `InternalEditorUtility.ReadScreenPixel`로 기록했습니다. GIF는 **자동 진행한 실제 기능 시연**이며 마우스 조작을 녹화한 영상은 아닙니다. 이 UI 이미지와 GIF는 화면을 다시 그리거나 합성하지 않았습니다. 아래 도구별 비교 이미지는 실제 셰이더 출력에 설명을 붙인 별도의 비교 패널입니다.

텍스처 GIF는 6 fps, 메시 GIF는 10 fps·130프레임·13초이며 256색 팔레트로 변환했습니다. 메시 이미지와 GIF의 크기는 1400 × 1020입니다. GIF의 색상은 팔레트 변환 때문에 PNG나 Unity 원본과 조금 다를 수 있습니다. 임시 캡처 코드는 제품 패키지에 포함되지 않으며, 건물 생성기는 재현용 소스로 별도 제공합니다.

## 화면별 내용

| 파일 | 실제로 보여주는 동작 |
| --- | --- |
| [`texture-editor.png`](texture-editor.png) | Fast Image Converter에서 **Brighten**을 적용한 편집 창. 밝기 0.2, 대비 1.1, 채도 1.05, 감마 1. |
| [`texture-workflow.gif`](texture-workflow.gif) | **Reset → Brighten → Vibrant → Desaturate → Reset → Brighten** 프리셋에 따라 슬라이더와 미리보기가 바뀌는 모습. |
| [`ab-comparison.gif`](ab-comparison.gif) | Brighten 결과를 스냅샷으로 열고 **Split Slider**의 위치를 움직여 Before와 After 비교. |
| [`mesh-visibility.png`](mesh-visibility.png) | 작업실 건물을 **All directions (XYZ + diagonals)**, **Balanced (98 directions)**, **Balanced (4 samples)**로 분석한 결과. 검사한 모든 방향에서 가려진 후보만 보라색으로 표시하고 XYZ 검사 상태를 확인하는 화면. |
| [`mesh-visibility.gif`](mesh-visibility.gif) | 98방향 결과를 한 번 계산한 뒤 유지한 채 건물 주위를 360도 회전하며, 계속 가려진 후보와 XYZ 광선 방향을 확인하는 시연. |

메시 GIF는 제품의 **All directions** 분석을 한 번 실행한 뒤, 같은 결과를 표시하면서 Scene 뷰만 360도 회전한 기록입니다. 후보 표시를 켜고 끄면서 건물 외형과 X-ray로 드러난 벽 뒤의 후보를 비교합니다. 촬영 중 시점별로 다시 분석해 후보를 바꾸지 않습니다. XYZ 진단에는 **+X, -X, +Y, -Y, +Z, -Z** 각각의 검사 여부와 보인 삼각형 수가 나타나며, 광선 방향 표시를 켜면 Scene 뷰의 화살표로도 확인할 수 있습니다.

검사 방향은 정육면체 표면의 격자를 구면 방향으로 정규화한 중첩 집합입니다. 빠르게·균형·정밀은 각각 26·98·386방향이며 모두 XYZ 양방향과 대각선을 포함합니다. 한 방향에서라도 보인 삼각형은 최종 후보에서 제외하고, 검사한 모든 방향에서 가려진 삼각형만 표시합니다. 실제 Light 오브젝트의 조명이나 그림자 설정을 사용하는 검사는 아닙니다.

## 메시 예제 재현

1. Built-in Render Pipeline을 사용하는 Unity 프로젝트에 TextureCocktail과 Core를 설치합니다.
2. [MeshVisibilityDemo.cs](../examples/MeshVisibilityDemo.cs)를 다운로드하거나 저장소에서 복사해 프로젝트의 `Assets/Editor/MeshVisibilityDemo.cs`에 둡니다. `Editor` 폴더가 없다면 만듭니다.
3. 현재 이름 없는 **Untitled** 씬이 열려 있다면 먼저 저장합니다. 컴파일이 끝나면 **LuticaLab → Examples → Create Mesh Visibility Demo**를 실행합니다.
4. 새 씬의 **Workshop visibility demo** 루트를 선택하고 **LuticaLab → TextureCocktail Mesh Visibility**를 엽니다.
5. **Analysis Mode: All directions (XYZ + diagonals)**, **Direction Coverage: Balanced (98 directions)**, **Surface Sampling: Balanced (4 samples)**를 선택합니다. 다른 씬 메시의 가림과 양면 처리는 끕니다.
6. **Analyze Current Pose**를 실행합니다. **XYZ ray checks**에서 ±X·±Y·±Z가 모두 검사되었는지 확인하고 **Show inspected XYZ ray directions**를 켭니다. **X-ray**를 전환해 벽체 속 후보를 살펴봅니다. 출입구를 통해 보인 상자 면은 후보에서 제외됩니다.
7. 다시 분석하지 않고 Scene 뷰를 한 바퀴 돌려 같은 보라색 후보가 유지되는지 확인합니다. 형상·트랜스폼·포즈·가림 메시·분석 설정을 바꾸면 다시 분석합니다. 실내를 사용하는 월드는 **Single view** 모드로 내부 시점도 따로 확인하세요.

생성기는 기존에 열린 씬을 유지하면서 별도의 씬을 추가하고 활성화합니다. Unity 2022.3에서 이름 없는 씬이 열려 있으면 먼저 저장하라는 안내를 표시하며, 사용자의 씬을 자동으로 저장하거나 닫지 않습니다. 머티리얼과 메시 파일은 `Assets/TextureCocktailVisibilityDemo/Example` 아래 저장하며, 다시 실행하면 `Example 1`처럼 고유한 폴더를 만들어 기존 에셋을 덮어쓰지 않습니다. 예제를 유지하려면 새 씬도 저장하세요. 생성한 오브젝트는 Undo로 되돌릴 수 있지만, 생성된 에셋 폴더는 Undo로 삭제되지 않습니다.

| 결과 목록 | 확인할 내용 |
| --- | --- |
| `01 Wall-cavity supports` | 두꺼운 벽체 속 기둥·보. 여러 방향에서도 벽에 가려진 구조물 후보를 확인합니다. |
| `02 Exterior walls` | 건물 외벽. 정면은 출입구가 열려 있고 뒤쪽은 막혀 있습니다. |
| `03 Interior crates` | 출입구를 통해 보이는 면은 모든 방향 결과의 후보에서 제외됩니다. 상자의 개별 면마다 결과가 다를 수 있으므로 오브젝트 전체의 삭제 여부로 해석하지 않습니다. |
| `04 Roof and trim` | 건물 지붕과 처마. |
| `05 Door frame and steps` | 출입구 문틀과 계단. |
| `06 Floor and foundation` | 실내 바닥과 기초. |

열린 출입구가 있는 정면은 -Z, 막힌 뒷벽은 +Z입니다. XYZ 표시는 모델로 들어오는 광선의 진행 방향이므로, 정면에서 들어오는 광선은 +Z로 표시됩니다. GIF에서는 원본 오브젝트를 회전시키지 않고 Scene 뷰를 회전시킵니다.

실제 Unity 분석에서는 **6 Renderer·400개 삼각형·98방향·146,327개 검사 지점**이 기록되었습니다. 어느 방향에서든 보인 면은 **148개**, 모든 검사 방향에서 가려진 후보는 **252개**이며, 판정 보류와 유효하지 않은 면은 모두 0개였습니다. 벽 속 기둥·보는 132개 삼각형 모두 후보였고, 내부 상자는 72개 중 26개가 보임으로 제외되고 46개가 후보로 남았습니다.

| 검사한 광선 방향 | 해당 방향에서 보인 삼각형 수 |
| --- | ---: |
| +X | 21 |
| -X | 21 |
| +Y | 5 |
| -Y | 12 |
| +Z | 39 |
| -Z | 11 |

방향별 개수에는 같은 삼각형이 중복될 수 있으며, 전체 보임 148개는 대각선을 포함한 98방향 결과의 합집합입니다. 따라서 위 여섯 개수를 더해 전체 보임 수를 계산하지 않습니다.

이 결과는 촬영 당시 형상·포즈에 대해 모델 외부의 98방향과 삼각형당 4지점을 검사한 결과입니다. 유한한 방향·표면 샘플만으로 가능한 모든 시점에서 보이지 않음을 증명하거나 삭제 가능성을 확정하지 않습니다. 좁은 틈이나 내부 시점에서 추가로 보일 수 있으며, 실제 월드에서 실내에 들어갈 수 있거나 문·벽의 배치가 바뀐다면 그 상태에서도 확인해야 합니다.


## 도구별 실제 처리 결과 갤러리

일곱 비교 이미지는 영어 설명이 붙은 **실제 셰이더 출력 비교 패널**입니다. 제품 창의 스크린샷과는 구분됩니다. 재현용 코드가 TextureCocktail 편집기를 생성하고, 실제 도구 선택·모드 설정·미리보기 렌더 경로를 거쳐 얻은 픽셀을 PNG로 내보냅니다. 실행기가 그 출력과 영어 제목·설정값을 비교 패널로 배치합니다. 효과 결과를 별도로 그리거나 보정하지 않습니다.

입력은 메모리에서 생성한 512 × 512 금속 패널, 오염 레이어와 회색조 높이 맵입니다. 패널의 도색·이음새·다이얼·나사·경고 띠를 같은 위치에서 비교할 수 있습니다. 인물이나 외부 모델·텍스처는 사용하지 않으며 원본 프로젝트의 에셋과 씬을 변경하지 않습니다. 이미지 안의 표기는 모든 README에서 공통으로 사용하는 영어입니다.

| 비교 이미지 | 실제 적용 설정 |
| --- | --- |
| [Fast Image Converter](tool-fast-image-converter.png) | 원본, Brighten 프리셋, Blur 0.8, Sharpen 1.5 비교. Brighten은 밝기 0.2·대비 1.1·채도 1.05·감마 1. |
| [Color Correction](tool-color-correction.png) | Basic 모드에서 색온도 +0.9와 -0.9, 노출 +0.5 비교. |
| [HSV Adjustment](tool-hsv-adjustment.png) | HSV Offset의 Hue +0.25 또는 +0.5, Saturation +0.1, Value 0. |
| [Texture Blender](tool-texture-blender.png) | 패널과 별도 오염 텍스처를 Multiply, Amount 0.75, 마스크 꺼짐으로 합성. |
| [Artistic Effects](tool-artistic-effects.png) | Color Correction Basic 노출 +1.5로 먼저 밝힌 입력을 공통 사용. Pixelate 크기 16, Halftone 점 크기 10·각도 45°, Cartoon 단계 8·엣지 임계값 0.15. |
| [Feature Extractor](tool-feature-extractor.png) | 청록색 특징 표시로 Edge Detection 민감도 0.5와 Canny Edge 민감도 0.8 비교. |
| [Normal Map Generator](tool-normal-map-generator.png) | 같은 높이 맵을 강도 2와 10, 높이 스케일 1로 변환. |

### 갤러리 재현

저장소를 clone한 Windows 환경에서 이 프로젝트를 Unity에서 닫은 뒤 다음을 실행합니다. 다른 프로젝트는 열어 두어도 됩니다. 그래픽 기능을 사용할 수 있는 Unity Editor가 필요합니다. 화면 캡처를 사용하지 않으므로 다른 창이 앞에 있어도 이미지에 섞이지 않습니다.

```powershell
.\docs\examples\Run-ToolGalleryCapture.ps1 -UnityPath 'C:\Program Files\Unity\Hub\Editor\2022.3.22f1\Editor\Unity.exe'
```

[실행기](../examples/Run-ToolGalleryCapture.ps1)는 [비교 패널 생성기](../examples/ToolGalleryCapture.cs)를 고유한 임시 Editor 폴더로 복사한 뒤 그래픽 기능이 활성화된 Unity 배치 모드에서 실행합니다. `docs/media/tool-*.png` 일곱 파일을 다시 만들고 임시 폴더를 제거합니다. 출력 검사는 처리 결과가 입력과 달라졌는지와 오류 셰이더의 자홍색 화면이 아닌지를 확인하며, 로그는 `Logs/ToolGalleryCapture.log`에 남습니다.
