# GEL Training Log - Web Dashboard

## 概要

このWebダッシュボードは、ユーザーのレベル分類、CAD経験スコア、学習進捗を可視化するフロントエンドアプリケーションです。

## 主な機能

### 1. ユーザーダッシュボード
- **ユーザー情報表示**: 氏名、メールアドレス、ユーザーレベル、学習グループ
- **スコア表示**:
  - Technical Score (スクリーニングテスト結果)
  - Self-Learning Score (自己学習能力)
  - CAD Experience Score (CAD/BIM/プログラミング経験)
- **可視化チャート**:
  - 経験レベルレーダーチャート (Rhino/Grasshopper)
  - スコア分解バーチャート
  - CADツール・プログラミング言語分布
  - アクティビティログタイムライン

### 2. 全ユーザー一覧
- 全ユーザーのレベル、スコア、学習グループを一覧表示
- 学習グループとレベルでフィルタリング可能
- ユーザーをクリックすると個別ダッシュボードに移動

### 3. インタラクティブ機能
- リアルタイムデータ更新
- レスポンシブデザイン（モバイル対応）
- Chart.jsによる美しいデータ可視化

## ファイル構成

```
server/
├── server_v2.py          # Flask APIサーバー
├── static/
│   └── dashboard.html    # Webダッシュボード（このファイル）
└── DASHBOARD_README.md   # このドキュメント
```

## セットアップ手順

### 1. コマンド分類データの配置

サーバーでワークフローカテゴリ分析を有効にするには、`rhino_commands_actions_classified.json`ファイルを配置します:

```bash
# ローカルからサーバーにファイルをコピー
scp log_data_analysis/00_data/command_classification/rhino_commands_actions_classified.json rhinologs@136.111.186.176:/home/rhinologs/

# または、サーバーのserver_v2.pyと同じディレクトリに配置
```

このファイルには以下のカテゴリが含まれています:
- **Workflow Categories**: 作成、構築、抽出、編集、整理、表示、計測、ファイル管理など
- **Detail Categories**: basic_geometry_creation, surface_from_curves_or_points, geometry_extraction など

### 2. サーバーの起動

```bash
cd /home/rhinologs
python3 server_v2.py
```

起動時に以下のメッセージが表示されればOK:
```
✓ Command classification loaded from: /home/rhinologs/rhino_commands_actions_classified.json
```

サーバーは `http://136.111.186.176:5000` で起動します。

### 3. ダッシュボードへのアクセス

#### オプション A: ローカルでHTMLファイルを開く（推奨）

1. `dashboard.html` をブラウザで直接開く
2. ブラウザのセキュリティ設定でCORS制限を緩和する必要がある場合があります

#### オプション B: Flaskサーバー経由でホスト

server_v2.pyに以下のルートを追加:

```python
@app.route('/')
def index():
    return send_from_directory('static', 'dashboard.html')
```

その後、`http://136.111.186.176:5000/` にアクセス

### 3. ブラウザのCORS設定（必要に応じて）

もしCORSエラーが発生する場合、以下の方法で解決できます:

#### Chrome/Edge
```bash
# Windows
chrome.exe --user-data-dir="C:/Chrome dev session" --disable-web-security --disable-site-isolation-trials

# Mac
open -n -a /Applications/Google\ Chrome.app/Contents/MacOS/Google\ Chrome --args --user-data-dir="/tmp/chrome_dev_test" --disable-web-security
```

または、Chromeの拡張機能「Allow CORS」をインストールしてください。

## 使い方

### ユーザーダッシュボードを見る

1. **ユーザーを選択**: ドロップダウンメニューからユーザーを選択
2. **View User Dashboard** ボタンをクリック
3. ダッシュボードが表示され、以下の情報が確認できます:
   - ユーザー情報カード（レベル、グループ、期間）
   - 4つの主要スコア（総アクション数、Technical、Self-Learning、CAD Experience）
   - 経験レベルレーダーチャート
   - スコア分解バーチャート
   - CADツール・プログラミング言語の円グラフ
   - アクティビティログの横棒グラフ

### 全ユーザー一覧を見る

1. **Show All Users** ボタンをクリック
2. テーブルに全ユーザーが表示されます
3. フィルタリング:
   - **Learning Group**: 特定の学習グループのみ表示
   - **Level**: 特定レベルのユーザーのみ表示
4. テーブルの行をクリックすると、そのユーザーの個別ダッシュボードに移動

## 新機能: ワークフローカテゴリ分析

コマンド分類データ（`rhino_commands_actions_classified.json`）を使用すると、以下の高度な分析が可能になります:

### ダッシュボードに追加される可視化

1. **Workflow Categories Distribution** (円グラフ)
   - 作成、構築、抽出、編集などのワークフローカテゴリ別の使用頻度
   - 各カテゴリの割合をパーセンテージで表示

2. **Detail Categories Breakdown** (横棒グラフ)
   - basic_geometry_creation, surface_from_curves_or_points などの詳細カテゴリ
   - Top 15の詳細カテゴリを使用頻度順に表示

3. **Workflow Timeline** (棒グラフ)
   - 直近100アクションのワークフローカテゴリ分布
   - ツールチップで各カテゴリの具体的なコマンド例を表示

### 分類されるワークフローカテゴリ

- **作成 (Creation)**: Line, Circle, Box などの基本ジオメトリ作成
- **構築 (Construction)**: Loft, Sweep, Revolve などのサーフェス構築
- **抽出 (Extraction)**: ExtractIsocurve, DupEdge などの要素抽出
- **編集 (Editing)**: Move, Rotate, Scale, Trim などの編集操作
- **コピー&ペースト (Copy & Paste)**: Copy, Paste, Mirror などの複製
- **整理 (Organization)**: Layer, Group, SelectAll などの整理操作
- **表示 (Visualization)**: Zoom, Pan, ViewCaptureToFile などの表示操作
- **計測・ドキュメント (Analysis)**: Length, Area, Distance などの計測
- **ファイル管理 (Data Management)**: Open, Save, Import, Export
- **設定・専門機能 (Settings/Specialized)**: Options, Plugins など

## API エンドポイント

ダッシュボードは以下のAPIエンドポイントを使用します:

| エンドポイント | 説明 |
|--------------|------|
| `GET /api/users` | 全ユーザーの一覧とスコア情報を取得 |
| `GET /api/user/<username>` | 特定ユーザーの詳細情報を取得 |
| `GET /api/logs?username=<username>` | 特定ユーザーのアクティビティログを取得 |
| `GET /api/logs/classified?username=<username>` | **新機能**: 分類済みログを取得（ワークフロー・詳細カテゴリ付き） |
| `GET /api/stats/workflow/<username>` | **新機能**: ユーザーのワークフロー統計を取得 |
| `GET /api/groups` | 学習グループ一覧を取得 |

## データ構造

### ユーザー情報
```json
{
  "username": "user_20250501_abc123",
  "full_name": "山田太郎",
  "email": "yamada@example.com",
  "user_level": 3,
  "learning_group": "20250501",
  "rhino_experience": "中級者",
  "grasshopper_experience": "初心者",
  "technical_score": 75.5,
  "self_learning_score": 80.0,
  "cad_experience_score": 48,
  "cad_tools": "AutoCAD;Rhino",
  "modeling_tools": "Blender;3ds Max",
  "programming_languages": "Python;C#"
}
```

### アクティビティログ
```json
{
  "timestamp": "2025-05-01 10:30:15",
  "username": "user_20250501_abc123",
  "action": "Command Started",
  "detail": "Line",
  "document_name": "model_v1.3dm"
}
```

## レベル分類システム

ユーザーは以下の5段階にレベル分けされます:

- **Level 1 (赤)**: 入門レベル - Rhino/GH未経験または総合スコア < 25
- **Level 2 (オレンジ)**: 初級レベル - Rhinoのみ初心者、または総合スコア 25-44
- **Level 3 (黄色)**: 中級レベル - Rhino中級者、または総合スコア 45-64
- **Level 4 (緑)**: 上級レベル - Rhino/GH両方中級以上、または総合スコア 65-79
- **Level 5 (青)**: エキスパート - 全ての条件でエキスパート、総合スコア ≥ 80

### 総合スコアの計算式

```
総合スコア = Technical Score × 0.25
           + CAD Experience Score × 0.15
           + Self-Learning Score × 0.20
           + Rhino/GH Composite Score × 0.40
```

### CAD Experience Score の計算

- **各ツール**: 8点 (例: AutoCAD, Rhino, Revit, Blender)
- **Rhinoボーナス**: +3点
- **Revitボーナス**: +3点
- **プログラミング言語**:
  - Python: 5点 (基本2点 + ボーナス3点)
  - C#: 4点 (基本2点 + ボーナス2点)
  - その他: 2点

## トラブルシューティング

### 問題: データが表示されない

**原因**: サーバーが起動していない、またはCORS制限

**解決策**:
1. サーバーが起動しているか確認: `ps aux | grep server_v2.py`
2. ブラウザのコンソールでエラーを確認 (F12キーでDevToolsを開く)
3. CORS設定を確認、またはブラウザのCORS制限を緩和

### 問題: "Failed to load users" エラー

**原因**: データベースファイルが存在しない、または破損している

**解決策**:
1. データベースファイルの存在確認: `ls -la /home/rhinologs/rhinolog.db`
2. データベースの整合性確認: `sqlite3 /home/rhinologs/rhinolog.db "PRAGMA integrity_check;"`
3. 必要に応じてデータベースを再作成

### 問題: ユーザーのCAD Experience Scoreが0

**原因**: Google Formでの回答がまだ送信されていない、またはデータが未入力

**解決策**:
1. ユーザーがGoogle Formアンケートに回答しているか確認
2. Code.gsが正しくデータを送信しているか確認
3. 手動でユーザー情報を更新:
```python
import sqlite3
conn = sqlite3.connect('/home/rhinologs/rhinolog.db')
c = conn.cursor()
c.execute("""UPDATE users SET
    cad_tools = 'AutoCAD;Rhino',
    modeling_tools = 'Blender',
    programming_languages = 'Python;C#',
    cad_experience_score = 48
    WHERE username = 'user_xxx'""")
conn.commit()
conn.close()
```

## 今後の改善案

- [ ] リアルタイム更新（WebSocket）
- [ ] グループ別の統計ダッシュボード
- [ ] 学習進捗のトレンドグラフ
- [ ] ユーザー比較機能
- [ ] PDFエクスポート機能
- [ ] アクティビティログの詳細フィルタリング
- [ ] コマンド分類（rhino_commands_actions_classified.json）を使った高度な分析

## 関連ファイル

- **server_v2.py**: Flask APIサーバー (lines 309-347: `/api/users` エンドポイント)
- **Code.gs**: Google Formアンケート連携スクリプト
- **ScreeningTest_Code.gs**: スクリーニングテストスコア送信スクリプト
- **dashboard_generator.py**: Python版のダッシュボード生成スクリプト（参考）
- **rhino_commands_actions_classified.json**: コマンド分類マッピング（将来の拡張用）

## サポート

質問や問題がある場合は、システム管理者に連絡してください。

---

**Version**: 1.0
**Last Updated**: 2025-11-12
**Author**: GEL Training Log System
