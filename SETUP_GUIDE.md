# Rhino Training Log System - セットアップガイド

完全なシステムセットアップ手順

## システム構成図

```
[受講者] → [Googleフォーム] → [Google Apps Script] → [GCPサーバー]
                                                             ↓
[受講者PC] → [Rhinoプラグイン] ────────────────────→ [GCPサーバー]
                                                             ↓
[講師・管理者] → [Webダッシュボード] ←─────────────[GCPサーバー]
```

---

## 📋 **Step 1: GCPサーバーのセットアップ**

### 1-1. VMへSSH接続

```bash
# GCP Consoleから SSH接続
```

### 1-2. 新しいサーバーのデプロイ

```bash
cd ~/rhinolog-server

# 既存のserver.pyをバックアップ
mv server.py server_old.py

# 新しいサーバーファイルをアップロード
# server_v2.py をVMにアップロードし、server.pyにリネーム
```

または、直接作成：

```bash
nano server.py
# C:\Users\admin\Documents\GitHub\RhinoLog\server\server_v2.py の内容を貼り付け
```

### 1-3. 必要なライブラリのインストール

```bash
source venv/bin/activate
pip install flask flask-cors
```

### 1-4. データベース初期化とサーバー起動

```bash
python server.py
```

初回起動時にSQLiteデータベースが自動作成されます。

### 1-5. 外部IPアドレスの確認

```bash
gcloud compute instances list
```

または、GCP Consoleで確認。例: `34.xxx.xxx.xxx`

このIPアドレスを以下で使用します。

---

## 📝 **Step 2: Googleフォームの作成**

### 2-1. フォーム作成

1. Google Formsで新しいフォームを作成
2. 以下の質問を追加：

**必須項目:**
- Windowsユーザー名（記述式）
- 氏名（記述式）
- メールアドレス（記述式・メール形式）
- 所属（記述式・任意）
- 研修開始日（日付）
- 研修終了日（日付）

### 2-2. Google Apps Scriptの設定

1. フォーム右上「︙」→「スクリプト エディタ」
2. `google-form/Code.gs` の内容を貼り付け
3. **重要**: `SERVER_URL` をVMの外部IPに変更：
   ```javascript
   const SERVER_URL = "http://34.xxx.xxx.xxx:5000/api/user/register";
   ```
4. 保存

### 2-3. トリガー設定

1. スクリプトエディタで「トリガー」（時計アイコン）
2. 「トリガーを追加」
3. 設定:
   - 実行する関数: `onFormSubmit`
   - イベントのソース: フォームから
   - イベントの種類: フォーム送信時
4. 権限の承認

### 2-4. テスト

1. フォームのプレビューを開いてテストデータを送信
2. スクリプトエディタの「実行数」タブでログ確認
3. サーバーログも確認

詳細は `google-form/README.md` を参照。

---

## 🦏 **Step 3: Rhinoプラグインの設定**

### 3-1. サーバーURLの設定

[GELTrainingLogPlugin.cs:57](GELTrainingLog/GELTrainingLogPlugin.cs#L57) を編集：

```csharp
private const string SERVER_URL = "http://34.xxx.xxx.xxx:5000";
```

VMの外部IPアドレスに変更してください。

### 3-2. プラグインのビルド

Visual Studio または Rider でプロジェクトをビルド：

```bash
dotnet build GELTrainingLog.sln --configuration Release
```

### 3-3. プラグインのインストール

1. ビルド後の `.rhp` ファイルを確認：
   ```
   GELTrainingLog\bin\Release\GELTrainingLog.rhp
   ```

2. Rhinoを起動
3. コマンド: `PackageManager`
4. `.rhp` ファイルをインストール

### 3-4. 動作確認

1. Rhinoを再起動
2. コマンドライン出力を確認：
   ```
   GEL Rhino Operation Logger Loaded
   Checking user registration...
   ✓ User registered: [氏名]
   ✓ Training period: 2025-05-19 to 2025-05-30
   ```

---

## 📊 **Step 4: ダッシュボードの配信**

### 4-1. HTMLファイルの配置

```bash
# SSHでVMに接続
cd ~/rhinolog-server
nano dashboard.html
# C:\Users\admin\Documents\GitHub\RhinoLog\server\dashboard.html の内容を貼り付け
```

### 4-2. APIベースURLの設定

`dashboard.html` を編集：

```javascript
const API_BASE = 'http://34.xxx.xxx.xxx:5000/api';
```

### 4-3. Flaskで静的ファイル配信

`server.py` に以下を追加：

```python
from flask import send_from_directory

@app.route('/')
def dashboard():
    return send_from_directory('.', 'dashboard.html')
```

### 4-4. アクセス

ブラウザで `http://34.xxx.xxx.xxx:5000/` にアクセス。

---

## 🚀 **Step 5: systemdサービス化（本番運用）**

サーバーを常時稼働させるため、systemdサービスとして登録：

```bash
sudo nano /etc/systemd/system/rhinolog.service
```

以下を記述（ユーザー名を実際のものに変更）：

```ini
[Unit]
Description=Rhino Log Collection Server
After=network.target

[Service]
Type=simple
User=yishizu
WorkingDirectory=/home/yishizu/rhinolog-server
Environment="PATH=/home/yishizu/rhinolog-server/venv/bin"
ExecStart=/home/yishizu/rhinolog-server/venv/bin/python server.py
Restart=always

[Install]
WantedBy=multi-user.target
```

サービス有効化・起動：

```bash
sudo systemctl daemon-reload
sudo systemctl enable rhinolog
sudo systemctl start rhinolog
sudo systemctl status rhinolog
```

---

## 📖 **使用フロー**

### 受講者側

1. **登録**: Googleフォームで情報を入力
2. **プラグインインストール**: `.rhp` ファイルをインストール
3. **Rhino起動**: 自動的にサーバーに接続し、登録確認
4. **通常使用**: 操作ログが自動記録（ローカルCSV + サーバー）

### 講師・管理者側

1. **ダッシュボードアクセス**: `http://34.xxx.xxx.xxx:5000/`
2. **ユーザー選択**: ドロップダウンから受講者を選択
3. **データ分析**:
   - 総操作数
   - コマンド使用頻度
   - 操作タイムライン
4. **CSV出力**: 詳細分析用にエクスポート

---

## 🔒 **セキュリティ注意事項**

### 現状の構成（開発・テスト用）

- HTTP通信（暗号化なし）
- 認証なし
- ファイアウォールで5000番ポート全開放

### 本番運用時の推奨設定

1. **HTTPS化**
   - Let's Encrypt で証明書取得
   - Nginx リバースプロキシ

2. **認証追加**
   - ダッシュボードにBasic認証またはOAuth

3. **ファイアウォール制限**
   - 特定IPからのみアクセス許可
   ```bash
   gcloud compute firewall-rules update allow-flask --source-ranges=[許可するIP]/32
   ```

4. **定期バックアップ**
   ```bash
   # cronで定期実行
   sqlite3 /home/rhinologs/rhinolog.db ".backup '/home/rhinologs/backup/db_$(date +%Y%m%d).db'"
   ```

---

## 🛠️ **トラブルシューティング**

### プラグインが「User not registered」と表示

- Googleフォームで登録したか確認
- Windowsユーザー名が正しいか確認（`whoami` コマンドで確認）
- サーバーが起動しているか確認

### ダッシュボードにデータが表示されない

- ブラウザの開発者ツールでネットワークエラーを確認
- `API_BASE` のURLが正しいか確認
- CORSエラーの場合、サーバーに `flask-cors` がインストールされているか確認

### サーバーに接続できない

- ファイアウォールルールを確認：
  ```bash
  gcloud compute firewall-rules list
  ```
- VMの外部IPアドレスが正しいか確認
- サーバーが起動しているか確認：
  ```bash
  sudo systemctl status rhinolog
  ```

---

## 📚 **ファイル構成**

```
RhinoLog/
├── GELTrainingLog/
│   ├── GELTrainingLogPlugin.cs  # メインプラグイン
│   └── GELTrainingLog.csproj
├── server/
│   ├── server_v2.py             # 新サーバー（SQLite対応）
│   ├── dashboard.html           # 可視化ダッシュボード
│   └── requirements.txt
├── google-form/
│   ├── Code.gs                  # Google Apps Script
│   └── README.md                # フォーム設定ガイド
└── SETUP_GUIDE.md               # このファイル
```

---

## 🎉 **完成！**

すべてのステップが完了したら、システムが稼働します：

1. ✅ 受講者がGoogleフォームで登録
2. ✅ プラグインが自動的にサーバーからユーザー情報取得
3. ✅ Rhino操作ログがローカル + サーバーに記録
4. ✅ ダッシュボードでリアルタイム分析

何か問題があれば、トラブルシューティングセクションを参照してください。
