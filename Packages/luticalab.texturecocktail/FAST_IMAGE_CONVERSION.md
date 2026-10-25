# Fast 2D Texture Conversion and Image Transformation

Shortcuts below use Windows notation. On macOS, use Cmd+Option instead of Ctrl+Alt.

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
1. Open TextureCocktail window (Ctrl+Alt+T)
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
1. Open TextureCocktail window (Ctrl+Alt+T)
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
1. Open Batch Processor (Ctrl+Alt+B or LuticaLab/TextureCocktail Batch Processor)
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
- `Ctrl+Alt+T`: Open TextureCocktail main window
- `Ctrl+Alt+B`: Open Batch Processor

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

### Example 1: Quick Contrast and Saturation Adjustment
1. Select a texture in the Project window.
2. Right-click → TextureCocktail → Quick Convert with Fast Converter.
3. Choose the PNG location and filename in the save dialog.
4. The result is saved with brightness 0, contrast 1.2, and saturation 1.1.

### Example 2: Extract Edges
1. Open TextureCocktail (Ctrl+Alt+T)
2. Select FeatureExtractor shader
3. Select your texture
4. Choose "Edge Detection" mode
5. Adjust sensitivity
6. Save result

### Example 3: Batch Process
1. Open Batch Processor (Ctrl+Alt+B)
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
Use **Edit Fullscreen** below the preview to inspect the edited result in a larger window. The **View** button beside the target texture opens the original image.

### Features
- **Edit Fullscreen**: Inspect the edited result and adjust polygon masks in a separate window
- **Fit to Window**: Auto-scales images to fit the window
- **Zoom Controls**: 10% to 400% zoom with slider and +/- buttons
- **Navigate**: Scroll to explore zoomed images
- **Quick Access**: "View" button next to target texture field

### Usage
1. Click **Edit Fullscreen** below the preview to open the edited result.
2. Turn off **Fit to Window** to use the zoom slider and +/- buttons.
3. Scroll to explore the zoomed image. Clicking and dragging on the image edits polygon masks.
4. To view the original, click **View** beside the target texture in the main window.

### Keyboard Controls
- Use zoom slider or +/- buttons to adjust zoom level
- Toggle "Fit to Window" for automatic scaling

## Credits

Developed for Texture Cocktail by LuticaLab
Part of the SKID-Project ecosystem
