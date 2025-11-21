# Fast 2D Texture Conversion and Image Transformation

This feature adds high-speed image processing and feature extraction capabilities to Texture Cocktail, enabling intuitive real-time texture editing within Unity.

## Features

### 1. Fast Image Converter
A shader-based image converter with real-time preview and quick presets.

**Shader:** `Hidden/FastImageConverter`

**Features:**
- **Quick Presets:** One-click adjustments for common operations
  - Reset: Restore default values
  - Brighten: Increase brightness and saturation
  - Darken: Decrease brightness with enhanced contrast
  - High Contrast: Boost contrast and saturation
  - Desaturate: Convert to grayscale
  - Vibrant: Enhance colors and reduce gamma

- **Adjustable Parameters:**
  - Brightness (-1 to 1)
  - Contrast (0 to 3)
  - Saturation (0 to 2)
  - Gamma (0.1 to 3)
  - Blur Amount (0 to 1)
  - Sharpen Amount (0 to 2)
  - Noise Amount (0 to 1)
  - Vignette Intensity (0 to 1)

- **Filter Modes:**
  - None: Basic adjustments only
  - Edge Detection: Sobel operator edge detection
  - Blur: Box blur filter
  - Sharpen: Edge enhancement filter

**Usage:**
1. Open TextureCocktail window (Alt+T)
2. Select `Hidden/FastImageConverter` shader
3. Select target texture
4. Choose a quick preset or adjust parameters manually
5. Preview in real-time
6. Save the result

### 2. Feature Extractor
Advanced feature extraction with multiple analysis modes.

**Shader:** `Hidden/FeatureExtractor`

**Extraction Modes:**
- **Edge Detection (Sobel):** Fast gradient-based edge detection
- **Canny Edge:** Advanced edge detection with Gaussian blur
- **Color Segmentation:** Quantize colors for analysis
- **Histogram Enhancement:** Improve contrast through histogram equalization

**Features:**
- Analysis channel selection (RGB, Red, Green, Blue, Luminance)
- Histogram visualization
- Real-time feature extraction preview
- Adjustable sensitivity and thresholds

**Usage:**
1. Open TextureCocktail window (Alt+T)
2. Select `Hidden/FeatureExtractor` shader
3. Select target texture
4. Choose extraction mode
5. Adjust settings as needed
6. View histogram (for Histogram Enhancement mode)
7. Save the extracted features

### 3. Batch Texture Processor
Process multiple textures with the same shader settings.

**Features:**
- Add multiple textures to processing queue
- Apply same shader and settings to all textures
- Custom output path
- Progress tracking
- Automatic file naming

**Usage:**
1. Open Batch Processor (Alt+B or LuticaLab/TextureCocktail Batch Processor)
2. Select shader to apply
3. Adjust common parameters
4. Add textures to process
5. Set output path
6. Click "Process All Textures"

### 4. Quick Access Toolbar
Floating toolbar for instant access to common operations.

**Features:**
- One-click operations: Brighten, Darken, High Contrast, Desaturate, Vibrant, Blur, Sharpen, Edge Detect
- Automatic operation configuration
- Direct access to TextureCocktail main window
- Batch processor launcher

**Usage:**
1. Open Quick Access Toolbar (LuticaLab/Quick Access Toolbar)
2. Select or drag texture
3. Click operation button
4. Choose save location
5. Done!

### 5. Keyboard Shortcuts

**Global Shortcuts:**
- `Alt+T`: Open TextureCocktail main window
- `Alt+B`: Open Batch Processor

**Context Menu (Right-click on texture in Project window):**
- TextureCocktail/Open with TextureCocktail
- TextureCocktail/Quick Convert with Fast Converter
- TextureCocktail/Quick Edge Detection

**Material Context Menu:**
- Copy Texture Shader Settings
- Paste Texture Shader Settings

## Technical Details

### Shader Implementation
All shaders are implemented using HLSL and are GPU-accelerated for real-time performance.

**FastImageConverter Shader:**
- Multi-compile directives for filter modes
- Sobel operator for edge detection
- Box blur convolution filter
- Unsharp mask for sharpening
- Per-pixel color adjustments

**FeatureExtractor Shader:**
- Multiple render passes for different extraction modes
- Sobel and Canny edge detection algorithms
- Color quantization
- Histogram equalization

### Editor Scripts
All editor windows inherit from appropriate base classes and follow Unity EditorWindow patterns.

**Editor Features:**
- Reflection-based material property access
- Real-time preview with RenderTexture
- Automatic texture serialization
- Multi-language support (English, Korean, Japanese)

## Performance Considerations

- All operations are GPU-accelerated using Unity shaders
- Real-time preview uses RenderTexture for efficient rendering
- Batch processing processes textures sequentially to avoid memory issues
- Histogram calculation is performed on-demand

## Localization

All UI strings are localized in three languages:
- English (default)
- Korean (한국어)
- Japanese (日本語)

Language files are located in:
`Packages/luticalab.core/Languages/`

## Examples

### Example 1: Quick Brighten
1. Select texture in Project window
2. Right-click → TextureCocktail → Quick Convert with Fast Converter
3. Image is automatically brightened and saved

### Example 2: Extract Edges
1. Open TextureCocktail (Alt+T)
2. Select FeatureExtractor shader
3. Select your texture
4. Choose "Edge Detection" mode
5. Adjust sensitivity
6. Save result

### Example 3: Batch Process
1. Open Batch Processor (Alt+B)
2. Select FastImageConverter shader
3. Set brightness to 0.2, contrast to 1.2
4. Add multiple textures
5. Process all at once

## Future Enhancements

- Additional filter modes (median filter, bilateral filter)
- Advanced histogram analysis (RGB channels, percentiles)
- Custom preset saving and loading
- Undo/Redo support
- Integration with Unity's Undo system
- GPU compute shader implementations for even faster processing
- Machine learning-based feature extraction

## Image Viewer Window

### Overview
Click on any preview image to open it in a dedicated viewer window for detailed inspection.

### Features
- **Click-to-View**: Click preview images to open full-size viewer
- **Fit to Window**: Auto-scales images to fit the window
- **Zoom Controls**: 10% to 400% zoom with slider and +/- buttons
- **Pan & Navigate**: Scroll and drag to explore large images
- **Quick Access**: "View" button next to target texture field

### Usage
1. Hover over any preview image to see "Click to view full size" hint
2. Click the preview to open viewer window
3. Toggle "Fit to Window" or use zoom controls
4. Click and drag to pan when zoomed in
5. View original texture using "View" button

### Keyboard Controls
- Use zoom slider or +/- buttons to adjust zoom level
- Toggle "Fit to Window" for automatic scaling

## Credits

Developed for Texture Cocktail by LuticaLab
Part of the SKID-Project ecosystem
