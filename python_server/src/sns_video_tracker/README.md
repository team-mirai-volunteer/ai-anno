# SNS Video Tracker

Instagram/TikTokなどのSNSでのショート動画の再生回数をトラックし、データをGoogle Spreadsheetに出力するシステムです。

## 機能

- **Instagram動画データ収集**: 再生回数、いいね数、コメント数などを取得
- **TikTok動画データ収集**: 再生回数、いいね数、コメント数、シェア数などを取得
- **Google Spreadsheet連携**: 収集したデータを自動的にスプレッドシートに書き込み
- **バッチ処理**: 複数の動画を一度に処理
- **データ更新**: 既存の動画データを最新情報で更新
- **ヘッドレススクレイピング**: Seleniumを使用した安定したデータ収集

## セットアップ

### 1. 依存関係のインストール

```bash
pip install -r requirements.txt
```

### 2. Google Credentials の設定

1. Google Cloud Consoleでプロジェクトを作成
2. Google Sheets API と Google Drive API を有効化
3. サービスアカウントを作成し、JSONキーファイルをダウンロード
4. JSONファイルを `gcp_credentials.json` として保存

### 3. Google Spreadsheet の準備

1. 新しいGoogle Spreadsheetを作成
2. サービスアカウントのメールアドレスにスプレッドシートの編集権限を付与
3. スプレッドシートのURLをコピー

### 4. 環境変数の設定

`.env.example` を `.env` にコピーして設定を編集：

```bash
cp .env.example .env
```

```env
GOOGLE_CREDENTIALS_PATH=gcp_credentials.json
SPREADSHEET_URL=https://docs.google.com/spreadsheets/d/YOUR_SPREADSHEET_ID/edit
HEADLESS_MODE=true
REQUEST_DELAY=2.0
MAX_RETRIES=3
LOG_LEVEL=INFO
```

## 使用方法

### コマンドライン インターフェース

#### スプレッドシートの初期化
```bash
python -m sns_video_tracker.cli init
```

#### 単一動画のトラッキング
```bash
python -m sns_video_tracker.cli track --url "https://www.instagram.com/p/EXAMPLE_POST_ID/"
```

#### 複数動画のトラッキング
```bash
python -m sns_video_tracker.cli track --urls \
  "https://www.instagram.com/p/POST1/" \
  "https://www.tiktok.com/@user/video/123456789"
```

#### ファイルから動画URLを読み込み
```bash
# urls.txt ファイルに動画URLを1行ずつ記載
python -m sns_video_tracker.cli track --file urls.txt
```

#### 既存動画データの更新
```bash
python -m sns_video_tracker.cli update --file urls.txt
```

### Python スクリプトでの使用

```python
from sns_video_tracker import VideoTracker

# トラッカーの初期化
tracker = VideoTracker()

# スプレッドシートの初期化
tracker.initialize_spreadsheet()

# 単一動画のトラッキング
video_data = tracker.track_single_video("https://www.instagram.com/p/EXAMPLE/")
print(tracker.get_video_summary(video_data))

# 複数動画のトラッキング
urls = [
    "https://www.instagram.com/p/POST1/",
    "https://www.tiktok.com/@user/video/123456789"
]
results = tracker.track_multiple_videos(urls)
```

## 収集されるデータ

スプレッドシートには以下の列でデータが保存されます：

| 列名 | 説明 |
|------|------|
| Timestamp | データ収集時刻 |
| Platform | プラットフォーム (Instagram/TikTok) |
| Video_URL | 動画のURL |
| Video_ID | 動画のID |
| Title | 動画のタイトル/キャプション |
| View_Count | 再生回数 |
| Like_Count | いいね数 |
| Comment_Count | コメント数 |
| Share_Count | シェア数 (TikTokのみ) |
| Author | 投稿者名 |
| Duration | 動画の長さ |
| Upload_Date | 投稿日時 |
| Last_Updated | 最終更新日時 |

## Looker Studio での可視化

収集したデータはGoogle SpreadsheetsからLooker Studioに接続して可視化できます：

1. Looker Studio で新しいレポートを作成
2. データソースとしてGoogle Spreadsheetsを選択
3. 作成したスプレッドシートを接続
4. 時系列グラフや比較チャートを作成

## 注意事項

- **利用規約の遵守**: 各プラットフォームの利用規約を必ず確認してください
- **レート制限**: 過度なリクエストを避けるため、適切な間隔でデータ収集を行ってください
- **データの正確性**: スクレイピングによるデータ収集のため、プラットフォームの仕様変更により正確性が影響を受ける可能性があります
- **認証情報の管理**: Google認証情報ファイルは安全に管理してください

## トラブルシューティング

### Chrome Driver エラー
```bash
# Chrome Driver を手動でインストール
pip install webdriver-manager
```

### Google認証エラー
- 認証情報ファイルのパスが正しいか確認
- サービスアカウントにスプレッドシートの編集権限があるか確認
- Google Sheets API が有効になっているか確認

### スクレイピングエラー
- ヘッドレスモードを無効にしてデバッグ: `HEADLESS_MODE=false`
- リクエスト間隔を長くする: `REQUEST_DELAY=5.0`
- 最大リトライ回数を増やす: `MAX_RETRIES=5`

## ライセンス

このプロジェクトはGPL-3.0ライセンスの下で公開されています。
