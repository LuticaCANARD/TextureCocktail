# New Shader Features - 새로운 셰이더 기능

Based on the FastImageConverter and FeatureExtractor shaders, three additional powerful features have been implemented.

FastImageConverter와 FeatureExtractor 셰이더를 참고하여 3가지 강력한 기능이 추가되었습니다.

## 1. ColorCorrection (색보정 및 그레이딩)

Professional color correction and grading tool for cinematic looks.

영화 같은 룩을 위한 프로페셔널 색보정 및 그레이딩 도구입니다.

### Shader: `Hidden/ColorCorrection`

### Features (기능):

**Grading Modes (그레이딩 모드):**
- **Basic**: Temperature, Tint, Exposure controls / 색온도, 틴트, 노출 조절
- **Color Grading**: Advanced Lift/Gamma/Gain, Color Balance / 고급 LGG 및 컬러 밸런스
- **Split Toning**: Creative shadow/highlight coloring / 창의적인 섀도우/하이라이트 컬러링

**Quick Presets (빠른 프리셋):**
- Reset (초기화)
- Warm (따뜻하게)
- Cool (차갑게)
- Vintage (빈티지)
- Cinematic (시네마틱)
- High Key (하이키)
- Low Key (로우키)

**Parameters (파라미터):**
- Temperature (-1 to 1) / 색온도
- Tint (-1 to 1) / 틴트
- Exposure (-3 to 3 EV) / 노출
- Lift/Gamma/Gain (Color controls) / 색상 컨트롤
- Color Balance (Shadows/Midtones/Highlights) / 컬러 밸런스
- Split Toning (Shadow/Highlight tones) / 스플릿 토닝
- Hue Shift (-180° to 180°) / 휘도 시프트
- Vibrance (-1 to 1) / 바이브런스

### Usage Example (사용 예):
```
1. Open TextureCocktail (Alt+T) / TextureCocktail 열기
2. Select Hidden/ColorCorrection shader / 셰이더 선택
3. Choose a preset (e.g., "Cinematic") / 프리셋 선택
4. Fine-tune parameters / 파라미터 미세 조정
5. Save result / 결과 저장
```

---

## 2. TextureBlender (텍스처 블렌더)

Advanced texture blending with multiple blend modes and masking support.

다양한 블렌드 모드와 마스킹을 지원하는 고급 텍스처 블렌딩 도구입니다.

### Shader: `Hidden/TextureBlender`

### Features (기능):

**12 Blend Modes (12가지 블렌드 모드):**
1. **Normal**: Direct replacement / 직접 대체
2. **Multiply**: Darkens / 어둡게
3. **Screen**: Lightens / 밝게
4. **Overlay**: Contrast based on base / 베이스 기반 대비
5. **Soft Light**: Subtle contrast / 미묘한 대비
6. **Hard Light**: Strong contrast / 강한 대비
7. **Color Dodge**: Brightens base / 베이스 밝게
8. **Color Burn**: Darkens base / 베이스 어둡게
9. **Darken**: Keeps darker colors / 어두운 색 유지
10. **Lighten**: Keeps lighter colors / 밝은 색 유지
11. **Difference**: Creative subtraction / 창의적 뺄셈
12. **Exclusion**: Softer difference / 부드러운 차이

**UV Controls (UV 컨트롤):**
- Scale (스케일): Adjust blend texture size / 블렌드 텍스처 크기 조정
- Offset (오프셋): Move blend texture position / 블렌드 텍스처 위치 이동
- Rotation (회전): Rotate blend texture (0-360°) / 블렌드 텍스처 회전

**Masking (마스킹):**
- Enable/Disable mask / 마스크 활성화/비활성화
- Channel selection (R/G/B/A) / 채널 선택
- Per-pixel blend control / 픽셀별 블렌드 조절

**Parameters (파라미터):**
- Blend Amount (0 to 1) / 블렌드 양 (0-100%)
- Base Texture / 베이스 텍스처
- Blend Texture / 블렌드 텍스처
- Mask Texture (optional) / 마스크 텍스처 (선택사항)

### Usage Example (사용 예):
```
1. Select base texture / 베이스 텍스처 선택
2. Select Hidden/TextureBlender shader / 셰이더 선택
3. Set blend texture / 블렌드 텍스처 설정
4. Choose blend mode (e.g., Multiply) / 블렌드 모드 선택
5. Adjust blend amount / 블렌드 양 조정
6. Optional: Add mask texture / 선택사항: 마스크 추가
```

---

## 3. ArtisticEffects (아티스틱 효과)

Creative artistic filters for unique visual styles.

독특한 비주얼 스타일을 위한 창의적인 예술적 필터입니다.

### Shader: `Hidden/ArtisticEffects`

### Effects (효과):

**1. Pixelate (픽셀화)**
- Creates retro pixel art look / 레트로 픽셀 아트 룩 생성
- Pixel Size: 1-100 / 픽셀 크기
- Perfect for 8-bit/16-bit style / 8비트/16비트 스타일에 완벽

**2. Posterize (포스터라이즈)**
- Reduces color levels for poster effect / 포스터 효과를 위한 색상 레벨 감소
- Color Levels: 2-256 / 색상 레벨
- Creates graphic design look / 그래픽 디자인 룩 생성

**3. Halftone (하프톤)**
- Simulates printing dot pattern / 인쇄 도트 패턴 시뮬레이션
- Dot Size: 1-20 / 도트 크기
- Dot Angle: 0-360° / 도트 각도
- Comic book and newspaper effect / 만화책 및 신문 효과

**4. Oil Paint (오일 페인트)**
- Creates painterly brush stroke effect / 회화 같은 붓 터치 효과 생성
- Radius: 1-10 / 반경
- Impressionist painting look / 인상주의 회화 룩

**5. Emboss (엠보스)**
- Creates raised 3D relief effect / 입체적인 3D 릴리프 효과 생성
- Strength: 0-5 / 강도
- Carved or stamped appearance / 조각 또는 스탬프 외관

**6. Cartoon (카툰)**
- Cell-shaded comic book style / 셀 셰이딩 만화책 스타일
- Edge Threshold: 0-1 / 엣지 임계값
- Color Steps: 2-10 / 색상 단계
- Anime and cartoon look / 애니메이션 및 만화 룩

### Usage Example (사용 예):
```
1. Open TextureCocktail / TextureCocktail 열기
2. Select Hidden/ArtisticEffects shader / 셰이더 선택
3. Choose effect (e.g., Cartoon) / 효과 선택
4. Adjust effect parameters / 효과 파라미터 조정
5. Preview and save / 미리보기 및 저장
```

---

## Technical Details (기술 세부사항)

### Performance (성능)
- All shaders GPU-accelerated / 모든 셰이더 GPU 가속
- Real-time preview / 실시간 미리보기
- Optimized for Unity 2022.3+ / Unity 2022.3+ 최적화

### Compatibility (호환성)
- Works with TextureCocktail main window / TextureCocktail 메인 창과 호환
- Batch processing support / 일괄 처리 지원
- Quick access toolbar integration / 빠른 액세스 도구 모음 통합

### Shader Keywords (셰이더 키워드)
- **ColorCorrection**: COLOR_GRADING, SPLIT_TONING
- **TextureBlender**: _BLENDMODE_* (12 modes)
- **ArtisticEffects**: PIXELATE, POSTERIZE, HALFTONE, OILPAINT, EMBOSS, CARTOON

---

## Workflow Integration (워크플로우 통합)

### With FastImageConverter (FastImageConverter와 함께):
1. Use FastImageConverter for basic adjustments / 기본 조정에 FastImageConverter 사용
2. Apply ColorCorrection for professional grading / 프로페셔널 그레이딩에 ColorCorrection 적용
3. Add ArtisticEffects for creative look / 창의적인 룩에 ArtisticEffects 추가

### With FeatureExtractor (FeatureExtractor와 함께):
1. Extract edges with FeatureExtractor / FeatureExtractor로 엣지 추출
2. Use as mask in TextureBlender / TextureBlender에서 마스크로 사용
3. Blend with original for stylized effects / 원본과 블렌드하여 스타일화된 효과

---

## Examples (예제)

### Example 1: Cinematic Look (시네마틱 룩)
```
1. ColorCorrection shader / ColorCorrection 셰이더
2. Select "Cinematic" preset / "Cinematic" 프리셋 선택
3. Adjust color balance for mood / 분위기에 맞게 컬러 밸런스 조정
4. Fine-tune split toning / 스플릿 토닝 미세 조정
```

### Example 2: Vintage Comic Book (빈티지 만화책)
```
1. ArtisticEffects shader / ArtisticEffects 셰이더
2. Select Halftone effect / Halftone 효과 선택
3. Adjust dot size and angle / 도트 크기 및 각도 조정
4. Optionally add Cartoon effect / 선택적으로 Cartoon 효과 추가
```

### Example 3: Texture Layering (텍스처 레이어링)
```
1. TextureBlender shader / TextureBlender 셰이더
2. Load base and blend textures / 베이스 및 블렌드 텍스처 로드
3. Choose Overlay blend mode / Overlay 블렌드 모드 선택
4. Adjust UV controls for positioning / 위치 지정을 위한 UV 컨트롤 조정
5. Add mask for selective blending / 선택적 블렌딩을 위한 마스크 추가
```

---

## Credits (크레딧)

Developed for Texture Cocktail by LuticaLab
Based on FastImageConverter and FeatureExtractor shader architecture
Part of the SKID-Project ecosystem

LuticaLab의 Texture Cocktail을 위해 개발됨
FastImageConverter 및 FeatureExtractor 셰이더 아키텍처 기반
SKID-Project 에코시스템의 일부
