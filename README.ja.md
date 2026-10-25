# TextureCocktail

[English](README.md) · [한국어](README.ko.md) · **日本語** · [简体中文](README.zh-CN.md)

日本語版 README です。上のリンクで文書の言語を切り替えられます。スクリーンショット、GIF、ツール比較画像は、各言語で共通の英語ラベル付き資料を使用しています。

Unity エディターでテクスチャの色やエフェクトを調整し、元画像と比較して PNG に保存するツールです。アバターやオブジェクトを複数の方向から調べ、どの検査方向からも見えないポリゴンの候補を確認することもできます。

[インストール](#インストール) · [初めてのテクスチャ編集](#初めてのテクスチャ編集) · [隠れたポリゴンを探す](#隠れたポリゴンを探す) · [開発とリリース（韓国語）](docs/development.md)

![Unity で TextureCocktail を使ってテクスチャを編集している画面](docs/media/texture-editor.png)

スクリーンショットと GIF は、実際に動作している Unity エディターを撮影したものです。以下のツール比較画像には、TextureCocktail で実際にレンダリングしたシェーダーの出力を使用しています。[撮影環境とサンプル（韓国語）](docs/media/capture-notes.md)も参照できます。

## インストール

パッケージが必要とする Unity の最小バージョンは **2022.3** で、このリポジトリのプロジェクトは **2022.3.22f1** を使用しています。TextureCocktail は `luticalab.core` パッケージと組み合わせて使用します。

### VCC でインストールする

1. VRChat Creator Companion で **Settings → Packages → Add Repository** を開きます。
2. 以下のリポジトリ URL を入力して **Add** を押し、表示されるパッケージ情報を確認してリポジトリを追加します。
3. 使用するプロジェクトのパッケージ管理画面で **TextureCocktail** を追加します。依存する Core も一緒にインストールします。
4. Unity でプロジェクトを開き、**LuticaLab → TextureCocktail** メニューを確認します。

```text
https://skid-cocktail.luticalab.dev/index.json
```

[VCC へのリポジトリ追加方法（英語）](https://vcc.docs.vrchat.com/guides/community-repositories/) · [最新リリース](https://github.com/LuticaCANARD/TextureCocktail/releases/latest)

<details>
<summary>ZIP から手動でインストールする</summary>

リリースページから TextureCocktail の ZIP と、同じリリースの Core の ZIP をダウンロードします。展開して、それぞれの `package.json` が以下の位置になるようにプロジェクトへ配置し、Unity を開きます。

```text
YourProject/
  Packages/
    luticalab.core/package.json
    luticalab.texturecocktail/package.json
```

同じパッケージを VCC と手動コピーの両方で重複してインストールしないでください。VCC でインストールしたプロジェクトでは、VCC の更新機能を使用します。

</details>

## 初めてのテクスチャ編集

1. Project ウィンドウで編集するテクスチャを選択し、右クリックして **TextureCocktail → Open with TextureCocktail** を選びます。先にウィンドウを開いた場合は、**Target Texture** にテクスチャをドラッグします。
2. **Quick Shader Selection** で **Filters/Fast Image Converter** を選びます。
3. **Brighten**、**High Contrast**、**Vibrant** などのプリセットを選ぶか、明るさ・コントラスト・彩度・ガンマを調整します。下のプレビューで結果を確認できます。
4. **Save Texture** を押し、`example_edited.png` のような新しい名前で保存します。保存ダイアログでは既定で `Assets` と元のファイル名が提案されるため、元画像を残す場合は別の名前を指定してください。
5. 保存した PNG を、使用するマテリアルのテクスチャスロットに設定します。

![色の設定に応じて変化する TextureCocktail の実際のプレビュー](docs/media/texture-workflow.gif)

UI は**英語・韓国語・日本語・簡体字中国語**に対応しています。初回起動時は OS の言語にかかわらず**英語**で表示されます。**LuticaLab → Setting → Language** で日本語や **简体中文** などに変更できます。明示的に選んだ言語は Unity を再起動しても維持されます。メニューのパス、シェーダー識別子、外部プラグイン名は英語で表示される場合があります。

### 元画像と編集結果を比較する

プレビューの下にある **A/B Test** を押すと、元画像と編集結果を一緒に確認できます。**Side by Side**（左右に並べる）、**Split Slider**（スライダーで分割）、**Toggle**（切り替え）から比較方法を選んでください。

![A/B Test の分割スライダーで Before と After を比較する様子](docs/media/ab-comparison.gif)

A/B ウィンドウの After 画像は、ウィンドウを開いた時点のスナップショットです。編集値を変更した場合は、メインウィンドウで **A/B Test** をもう一度押して更新してください。比較用のマテリアルと Quad を生成することもでき、既定の出力フォルダーは `Assets/TextureCocktailABTest` です。

### 一部の領域だけを編集する

プレビューで **Enable Polygon Mask** を有効にし、3 点以上を配置してから最初の点をクリックするか、**Finish Polygon** を押します。閉じた領域にだけエフェクトが適用されます。頂点やポリゴンの内側をドラッグして領域を調整し、**Reset Polygons** で消去できます。

小さな領域は **Edit Fullscreen** で拡大して作業してください。**LuticaLab → TextureCocktail Mesh Preview** では、対象の Renderer、マテリアルスロット、テクスチャプロパティを指定して、メッシュ上の編集領域を確認できます。

## 作業に合ったツールを選ぶ

メインウィンドウの **Quick Shader Selection** から、以下のツールを使用できます。

| やりたいこと | 選ぶツール | 操作の流れ |
| --- | --- | --- |
| 明るさ・コントラスト・彩度の調整、ぼかし・シャープ化 | **Filters/Fast Image Converter** | テクスチャを指定 → プリセットまたは Filter Type を選択 → 値を調整 |
| 色温度・露出・色合いの補正 | **Color/Color Correction** | プリセットを選択 → グレーディングモードと色の値を調整 |
| 色相・彩度・明度の変更 | **Color/HSV Adjustment** | HSV 変換とオフセットを調整 |
| 2 枚のテクスチャの合成 | **Effects/Texture Blender** | ベースとブレンド用のテクスチャを指定 → 合成モードと量を調整 |
| ピクセル化・ハーフトーン・カートゥーン効果 | **Effects/Artistic Effects** | エフェクトを選択 → 対応する値を調整 |
| エッジや色の特徴抽出 | **Analysis/Feature Extractor** | 抽出モードを選択 → チャンネルと感度を調整 |
| 高さ画像からノーマルマップを生成 | **Generators/Normal Map Generator** | テクスチャを指定 → 強度と高さのスケールを調整 |

### ツールごとの処理結果

手続き的に生成した 512 × 512 の金属製コントロールパネル、汚れテクスチャ、高さ画像を使用しています。各結果は TextureCocktail の実際のシェーダーでレンダリングし、入力と出力を英語ラベル付きの比較画像にまとめています。以下の値でサンプルを再現できます。生成手順は[撮影環境とサンプル（韓国語）](docs/media/capture-notes.md)を参照してください。

#### Fast Image Converter — 明るさとディテール

テクスチャを明るくしたり、細部をぼかしたり、輪郭を鮮明にしたりできます。**Brighten** プリセット（明るさ **0.2**、コントラスト **1.1**、彩度 **1.05**、ガンマ **1**）、**Blur 0.8**、**Sharpen 1.5** を比較しています。

![金属パネルの元画像と Brighten、ぼかし、シャープ化の結果](docs/media/tool-fast-image-converter.png)

#### Color Correction — 暖色と寒色のバリエーション

同じ小物に暖かい色合いと冷たい色合いを付けます。どちらも **Basic** グレーディング、露出 **+0.5** を使用し、色温度は暖色が **+0.9**、寒色が **−0.9** です。

![金属パネルの元画像と暖色・寒色の色補正結果](docs/media/tool-color-correction.png)

#### HSV Adjustment — 表面の色を変更する

陰影を保ちながら塗装面の色を変更します。HSV Convert は **0**、HSV Offset は **(0.25, 0.1, 0)** と **(0.5, 0.1, 0)** を比較しています。

![金属パネルの元画像と色相・彩度オフセットを変えた二つの結果](docs/media/tool-hsv-adjustment.png)

#### Texture Blender — 汚れを重ねる

ベース画像に別の汚れテクスチャを重ね、小物に使用感を加えます。サンプルは **Multiply**、ブレンド量 **0.75**、ブレンドマスク無効を使用しています。

![パネルのベース画像、汚れテクスチャ、Multiply による合成結果](docs/media/tool-texture-blender.png)

#### Artistic Effects — 小物の表現を変える

加工後も細部が見えるよう、まず Color Correction の **Basic**、露出 **+1.5** で入力を明るくしています。中心 **(0.5, 0.5)** の **Pixelate 16**、ドットサイズ **10**・角度 **45°** の **Halftone**、階調数 **8**・エッジしきい値 **0.15** の **Cartoon** を比較しています。

![明るくしたパネルとピクセル化、ハーフトーン、カートゥーンの結果](docs/media/tool-artistic-effects.png)

#### Feature Extractor — 輪郭を確認する

パネルの輪郭を抽出し、確認やグラフィック効果に使えます。**Sobel** は感度 **0.5**、**Canny** は感度 **0.8**。どちらもエッジ色はシアン、強度は **1** です。

![パネルの元画像とシアンの Sobel・Canny エッジ抽出結果](docs/media/tool-feature-extractor.png)

#### Normal Map Generator — 表面の凹凸を作る

グレースケールの高さ画像から法線マップを生成します。高さスケールを **1** に固定し、強度 **2** と **10** を比較しています。強度を上げると高さの変化がより強調されます。

![グレースケールの高さ画像と強度 2・10 の法線マップ](docs/media/tool-normal-map-generator.png)

### 複数のテクスチャに同じ補正を適用する

**LuticaLab → TextureCocktail Batch Processor** を開いてシェーダーを指定します。Project でテクスチャを選択し、**Add Selected Textures** で追加します。共通設定と **Output Path** を確認してから、**Process All Textures** を押します。

一括処理の共通設定は明るさ・コントラスト・彩度・ガンマで、指定したシェーダーに対応するプロパティがある場合に適用されます。既定の出力先は `Assets/ProcessedTextures/<元の名前>_processed.png` です。同名の結果があると上書きされる場合があるため、作業ごとに出力フォルダーを指定してください。

## 隠れたポリゴンを探す

**X・Y・Z 各軸の正負両方向と斜め方向から検査し、検査したすべての方向で遮られた三角形だけを表示**します。1 方向からでも見えた面は候補から除外されます。実際の Light を追加する必要はありません。メッシュを削除せず、整理の対象にできるかを判断するための候補を示します。

![XYZ の正負両方向と斜め方向から検査し、隠れたままの建物ポリゴンだけを示した Unity 画面](docs/media/mesh-visibility.png)

1. **Hierarchy** でアバターやオブジェクトのルートを選択します。Project ウィンドウのプレハブファイルではなく、シーンに配置したオブジェクトを使用してください。
2. **LuticaLab → TextureCocktail Mesh Visibility** を開きます。Mesh Preview ウィンドウの **Find Hidden Polygons…** からも開けます。
3. **Analysis Mode** は既定の **All directions (XYZ + diagonals)** にします。**Direction Coverage** は Quick が 26 方向、既定の Balanced が 98 方向、Detailed が 386 方向です。精度を上げると、既存の検査方向を保ったまま方向が追加されます。
4. **Surface Sampling** を選びます。既定の Balanced は三角形ごとに 4 点、Fast は 1 点、Detailed は 7 点を検査します。必要に応じて **Include other scene renderers as blockers** を有効にし、シーン内のほかのメッシュによる遮蔽も検査します。
5. **Analyze Current Pose** を押します。有効になっている子オブジェクトの MeshRenderer と SkinnedMeshRenderer を、現在のポーズでまとめて解析します。
6. **XYZ ray checks** で **+X、-X、+Y、-Y、+Z、-Z** がそれぞれ検査されたことと、方向ごとに見えた三角形の数を確認します。**Show inspected XYZ ray directions** を有効にすると、Scene ビューにも光線方向の矢印が表示されます。
7. **Hidden in all checked directions** の候補だけが**紫色**で表示されます。**X-ray** でほかのメッシュの裏にある候補も確認でき、**Select** で該当するオブジェクトを選択できます。

XYZ 検査は、水平方向に一周するだけの検査ではありません。上下を含む 6 つの軸方向と斜め方向を検査します。方向の表記は、**モデルへ向かう光線の進行方向**です。たとえば +Z の光線は、モデルの -Z 側から入ります。

### サンプル：建物を 360 度見回しても隠れたままのポリゴンを確認する

小さな作業場の建物で、**厚い壁の中にある柱や梁**と、**出入口から見える屋内の箱の面**を比較します。複数の方向からの結果を集約するため、出入口から一度でも見えた箱の面は候補から除外されます。壁の内部に隠れたままの面は X-ray で確認できます。

![98 方向の検査結果を維持したまま建物の周囲を 360 度見回し、隠れた候補と XYZ 光線を確認する実際の Unity デモ](docs/media/mesh-visibility.gif)

GIF は、**98 方向で一度解析した結果を保ったまま、Scene ビューを 360 度回転**させるデモです。確認用の視点だけを回す場合は再解析不要です。画面に見える面が変わっても、すべての検査方向で隠れていた候補の集合は変わりません。

試すには、[サンプル生成スクリプト](docs/examples/MeshVisibilityDemo.cs)をプロジェクトの `Assets/Editor` にコピーしてください。名前のない Untitled シーンが開いている場合は先に保存し、**LuticaLab → Examples → Create Mesh Visibility Demo** を実行します。別途モデルをダウンロードせずにサンプルシーンを作成できます。必要な設定は[サンプル環境と再現手順（韓国語）](docs/media/capture-notes.md#메시-예제-재현)にまとめています。

| 全方向の解析結果 | 意味 |
| --- | --- |
| Seen in any direction | 検査した方向のうち少なくとも 1 方向で見えた点があるため、候補から除外 |
| 紫色 · Hidden in all checked directions | すべての検査方向で隠れた候補。この面だけを色で表示 |
| Unresolved | すべての検査方向で隠れていると判定できなかった面。候補から除外 |
| Degenerate | 面積がないなど、解析に適さない三角形。候補から除外 |

**「全方向」は、選択した 26・98・386 個の検査方向を指します。あらゆる視点から絶対に見えないことを証明するものではありません。** 小さな露出部分や狭い隙間を見逃す場合があります。屋内に入れる建物は、内部の視点でも確認してください。オブジェクト・メッシュ形状・ポーズ・ブレンドシェイプ・遮蔽用メッシュ・解析設定を変更した場合は、再解析が必要です。

### 特定のカメラや 1 方向だけを検査する

従来の検査は **Analysis Mode: Single view** で使用できます。**View Source** で、現在の Scene ビュー、シーンの Camera、指定方向のいずれかを選びます。方向を指定する場合は、モデルへ向かう光線の進行方向を入力します。

| 単一視点の色と分類 | 意味 |
| --- | --- |
| 赤 · Occluded | 検査した点がほかの面に遮られている |
| オレンジ · Back-facing | 検査視点とは反対側を向いている |
| 青 · Outside view | 指定カメラの視野の外にある |
| Visible | 検査した点で、見える部分が見つかった |
| Degenerate | 面積がないなど、解析に適さない三角形 |

単一視点の結果は、解析時点の視点とポーズに限られます。カメラや Scene ビューを動かした場合は再解析してください。建物の裏からは隠れている箱も、正面の出入口や屋内からは見えることがあります。

どちらのモードも不透明な形状として検査し、透明度、アルファカットアウト、マテリアルのカリング、シェーダーによる変形、カメラのレイヤー、LOD、レンダーパイプラインの効果は再現しません。解析や色の表示によって元のメッシュが変更されることはありません。

## その他のユーティリティとトラブルシューティング

- **LuticaLab → Quick Access Toolbar**：テクスチャを指定し、プリセットボタンを押して処理結果を保存します。
- **LuticaLab → Texture Optimizer**：**Scan Textures** でフォルダーを検査し、推奨設定を確認します。Fix ボタンは実際の Texture Importer 設定を変更します。
- **LuticaLab → AI Connector**：別途起動した Ollama または OpenAI 互換サーバーに接続し、モデルを選んでプロンプトを送信します。テクスチャ編集機能そのものに AI サーバーは不要です。

| 症状 | 確認すること |
| --- | --- |
| LuticaLab メニューがない | Core と TextureCocktail の両方がインストールされているか、Console にコンパイルエラーがないか確認します。 |
| プレビューが表示されない | 対象テクスチャと Quick Shader Selection を確認し、プレビュー領域を開きます。 |
| 保存したテクスチャがオブジェクトに反映されない | 保存した PNG をマテリアルのテクスチャスロットに設定します。 |
| A/B 比較が以前の結果を表示する | メインウィンドウから A/B Test を開き直し、スナップショットを更新します。 |
| メッシュ解析ボタンが無効になっている | 解析対象がシーン上のオブジェクトか確認します。Single view モードでは Scene ビューまたは Camera があるか、指定方向がゼロでないかも確認します。 |

Windows のショートカットは **Ctrl+Alt+T**（メインウィンドウ）と **Ctrl+Alt+B**（一括処理）です。macOS では **Cmd+Option+T/B** を使用します。

## 関連ドキュメント

- [開発・拡張・テスト・リリース自動化（韓国語）](docs/development.md)
- [プラグイン開発ガイド（英語）](Packages/luticalab.texturecocktail/PLUGIN_GUIDE.md)
- [高速画像変換ガイド（韓国語）](Packages/luticalab.texturecocktail/FAST_IMAGE_CONVERSION_KR.md) · [英語版](Packages/luticalab.texturecocktail/FAST_IMAGE_CONVERSION.md)
- [色補正・合成・エフェクトの詳細（英語・韓国語）](Packages/luticalab.texturecocktail/NEW_FEATURES.md)
- [不具合の報告](https://github.com/LuticaCANARD/TextureCocktail/issues)

TextureCocktail は、LuticaLab の SKID-Project に属するサブプロジェクトです。
