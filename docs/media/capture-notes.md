# README 미디어 촬영 기록

[사용자 README](../../README.md)

## 환경과 출처

- 촬영일: 2026-10-25
- Unity: **2022.3.22f1**, Windows, Direct3D 11, 에디터 UI 배율 100%
- 패키지: TextureCocktail **0.0.10**, LuticaLab Core **0.0.3**
- 화면 언어: English. README에서는 필요한 항목에 한국어 이름도 병기합니다.
- 이미지 예제: 저장소의 [`Assets/rety.png`](../../Assets/rety.png). 내용을 보기 쉽게 원본 좌표 `(0, 500)`에서 `1024 × 1024` 영역을 잘라 `768 × 768`로 축소한 임시 PNG를 사용했습니다. 원본 파일은 변경하지 않았습니다.
- 메시 예제: Unity Cube 세 개로 만든 임시 씬입니다. 별도의 아바타나 외부 모델이 필요하지 않습니다.

모든 화면은 실행 중인 Unity의 실제 TextureCocktail 창을 캡처했습니다. 임시 에디터 스크립트로 실제 프리셋 처리 메서드, A/B 슬라이더 값, X-ray 설정을 순서대로 적용한 뒤 `InternalEditorUtility.ReadScreenPixel`로 기록했습니다. GIF는 **자동 진행한 실제 기능 시연**이며 마우스 조작을 녹화한 영상은 아닙니다. UI나 결과 이미지를 다시 그리거나 합성하지 않았습니다.

촬영 후 불필요한 창 아래 여백을 잘랐고, GIF는 6 fps와 256색 팔레트로 변환했습니다. GIF의 색상은 팔레트 변환 때문에 PNG나 Unity 원본과 조금 다를 수 있습니다. 임시 캡처 코드와 예제는 제품 패키지에 포함되지 않습니다.

## 화면별 내용

| 파일 | 실제로 보여주는 동작 |
| --- | --- |
| [`texture-editor.png`](texture-editor.png) | Fast Image Converter에서 **Brighten**을 적용한 편집 창. 밝기 0.2, 대비 1.1, 채도 1.05, 감마 1. |
| [`texture-workflow.gif`](texture-workflow.gif) | **Reset → Brighten → Vibrant → Desaturate → Reset → Brighten** 프리셋에 따라 슬라이더와 미리보기가 바뀌는 모습. |
| [`ab-comparison.gif`](ab-comparison.gif) | Brighten 결과를 스냅샷으로 열고 **Split Slider**의 위치를 움직여 Before와 After 비교. |
| [`mesh-visibility.png`](mesh-visibility.png) | **Direction (0, 0, 1)**, **Balanced (4 samples)**로 분석한 결과. 가려짐 후보만 표시하고 X-ray를 켠 화면. |
| [`mesh-visibility.gif`](mesh-visibility.gif) | 같은 분석 결과에서 **X-ray 끄기 → 켜기 → 끄기**. 앞 패널 뒤의 후보가 투시 표시로 드러나는 모습. |

## 메시 예제 재현

빈 씬에 `Visibility demo` 루트를 만들고 다음 Cube를 자식으로 배치합니다. 회전은 모두 `(0, 0, 0)`입니다.

| 이름 | Position | Scale | 구별용 색 |
| --- | --- | --- | --- |
| `01 Front panel` | `(-0.55, 0, 0)` | `(2.0, 2.7, 0.25)` | 청록 |
| `02 Hidden panel` | `(-0.55, 0, 0.85)` | `(1.5, 2.1, 0.22)` | 황토 |
| `03 Visible side piece` | `(1.35, -0.4, 0.3)` | `(0.75, 1.8, 0.6)` | 초록 |

루트를 선택하고 **LuticaLab → TextureCocktail Mesh Visibility**를 엽니다. View Source는 **Direction**, Ray Direction은 **(0, 0, 1)**, Surface Sampling은 **Balanced**로 설정합니다. 다른 씬 메시의 가림과 양면 처리는 끈 채 **Analyze Current Pose**를 실행합니다.

이 예제의 결과는 **Renderer 3개, 삼각형 36개, Visible 4개, Occluded 2개, Back-facing 30개**입니다. Scene 뷰에서는 결과를 살펴보기 위해 약간 비스듬한 각도를 사용하지만, 분석 기준은 지정한 +Z 방향입니다. **Occluded**만 표시하고 X-ray를 전환하면 뒤 패널의 두 삼각형이 앞 패널을 통해 보입니다. Scene 뷰에서 보이는 각도와 분석한 방향을 구별해 주세요.

이 숫자는 예제 형상에 대한 결과이며 실제 아바타의 절감률이나 삭제 가능한 폴리곤 수를 뜻하지 않습니다.
