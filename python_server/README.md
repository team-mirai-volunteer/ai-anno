# AIあんの バックエンド環境構築ガイド

## 概要

このガイドでは、AIあんののバックエンド環境を一から構築し、質問応答システムを動作させるまでの手順を詳しく説明します。

## 前提条件

- Ubuntu 20.04以上
- Python 3.12
- Poetry（Python依存関係管理）
- PostgreSQL
- Google Cloud Platform アカウント（Gemini API用）

## 1. 基本セットアップ

### 1.1 依存パッケージのインストール

```bash
# Poetryで依存関係をインストール
poetry install

# システム依存関係のインストール
sudo apt update
sudo apt install -y poppler-utils postgresql postgresql-contrib
```

### 1.2 環境変数の設定

```bash
# .envファイルを作成
cp .env.example .env
```

`.env`ファイルを編集して以下の設定を行います：

```env
# データベース設定
PG_HOST=localhost
PG_PORT=5432
PG_DATABASE=aituber_dev
PG_USER=aituber_user
PG_PASSWORD=your_password

# Google API設定（必須）
GOOGLE_API_KEY=your_google_api_key

# その他のAPI設定（オプション）
AZURE_SPEECH_KEY=your_azure_key
ELEVENLABS_API_KEY=your_elevenlabs_key
GOOGLE_APPLICATION_CREDENTIALS=path/to/credentials.json
GOOGLE_DRIVE_FOLDER_ID=your_drive_folder_id
```

## 2. Google API キーの取得と設定

### 2.1 Google Cloud Consoleでの設定
1. [Google Cloud Console](https://console.cloud.google.com/)にアクセス
2. 新しいプロジェクトを作成または既存プロジェクトを選択
3. 「APIs & Services」→「Library」で「Generative Language API」を有効化
4. 「APIs & Services」→「Credentials」でAPIキーを作成
5. **重要**: 請求先アカウントを設定（無料枠を超える場合があります）

### 2.2 環境変数への設定
```bash
export GOOGLE_API_KEY="your_actual_api_key_here"
```

## 3. PostgreSQLデータベースの設定

### 3.1 PostgreSQLサービスの開始
```bash
sudo systemctl start postgresql
sudo systemctl enable postgresql
```

### 3.2 データベースとユーザーの作成
```bash
sudo -u postgres psql
```

PostgreSQL内で以下を実行：
```sql
CREATE DATABASE aituber_dev;
CREATE USER aituber_user WITH PASSWORD 'your_password';
GRANT ALL PRIVILEGES ON DATABASE aituber_dev TO aituber_user;
\q
```

### 3.3 データベースマイグレーション
```bash
poetry run alembic upgrade head
```

## 4. ナレッジベースの構築

### 4.1 サンプル画像のダウンロード
PDF（を画像化した）ファイルを取得します：

```bash
poetry run python -m src.cli.import_pdf
poetry run python -m src.cli.import_docs_csv
```

### 4.2 FAISSナレッジベースの作成
```bash
poetry run python -m src.cli.save_faiss_knowledge_db
poetry run python -m src.cli.save_faiss_db
```

または、まとめて実行：
```bash
make setup/resources
```

## 5. サーバーの起動

```bash
# 開発サーバーの起動
poetry run uvicorn src.web.api:app --host 127.0.0.1 --port 7200

# または
make run
```

## 6. 動作確認

### 6.1 基本的なAPIテスト
```bash
curl -X POST "http://127.0.0.1:7200/reply" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "inputtext=デジタル民主主義について教えてください"
```

期待される応答例：
```json
{
  "response_text": "こんにちは！AIあんのです。デジタル民主主義について...",
  "image_filename": "slide_56.png"
}
```

### 6.2 ナレッジベースの確認
```bash
# FAISSデータベースの確認
ls -la faiss_knowledge/
ls -la faiss_qa/
```

以下のファイルが存在することを確認：
- `faiss_knowledge/index.faiss`
- `faiss_knowledge/index.pkl`
- `faiss_qa/index.faiss`
- `faiss_qa/index.pkl`

## 7. トラブルシューティング

### 7.1 よくある問題と解決方法

#### Google API Quota Exceeded エラー
```
Error: quota exceeded
```
**解決方法**: Google Cloud Consoleで請求先アカウントを設定

#### PostgreSQL接続エラー
```
Error: could not connect to server
```
**解決方法**: 
1. PostgreSQLサービスの状態確認: `sudo systemctl status postgresql`
2. 接続設定の確認: `.env`ファイルの設定を再確認

#### FAISS インデックスが見つからない
```
Error: No such file or directory: 'faiss_knowledge/index.faiss'
```
**解決方法**: `make setup/resources`を再実行

#### 依存関係エラー
```
Error: No module named 'xxx'
```
**解決方法**: `poetry install`を再実行

### 7.2 ログの確認方法

```bash
# サーバーログの確認
poetry run uvicorn src.web.api:app --host 127.0.0.1 --port 7200 --log-level debug

# 特定のログファイルの確認
tail -f logs/interaction.log
```

## 8. 技術仕様

### 8.1 システム構成
- **Web Framework**: FastAPI
- **AI Model**: Google Generative AI (Gemini 1.5 Pro)
- **Vector Search**: FAISS + Google Embeddings
- **Database**: PostgreSQL
- **Package Management**: Poetry
- **Architecture**: RAG (Retrieval-Augmented Generation)

### 8.2 データベース内容
- **政策マニフェスト**: 118件（東京イノベーション政策の5本柱）
- **FAQ**: 253件（ブロードリスニング用想定FAQ）
- **スライド**: 107枚（政策説明用画像）

## 9. 成功確認チェックリスト

- [ ] PostgreSQLデータベースが正常に動作
- [ ] Google API キーが正しく設定され、請求アカウントが有効
- [ ] FAISSナレッジベースが構築済み（118件 + 253件）
- [ ] サーバーが正常に起動（ポート7200）
- [ ] APIエンドポイント `/reply` が応答
- [ ] 質問に対して適切な日本語回答が生成される
- [ ] 関連するスライド画像が正しく参照される
- [ ] ログが正常に出力される

## 10. 既知の問題と改善点

### フォールバック機能の制限
現在のシステムでは、政策外の質問や関連文書が見つからない場合に、常に同じスライド（`slide_1.png`：子育て支援）がフォールバック応答として返されます。

**技術的詳細**:
- `src/get_faiss_vector.py:160`: `DEFAULT_FALLBACK_KNOWLEDGE_METADATA = {"row": 1, "image": "slide_1.png"}`
- `src/gpt.py:23`: `DEFAULT_FALLBACK_HAL_KNOWLEDGE_METADATA = {"row": 1, "image": "unknown.png"}`

**改善提案**:
- 多様なフォールバックスライドの準備
- ランダム選択機能の実装
- コンテキスト考慮型フォールバック

## 11. RAG の評価

```bash
poetry run python -m src.cli.save_faiss_db --for-eval  # 評価する際はtrain/test split用に --for-eval オプションを追加
poetry run python -m src.cli.rag_evaluation.evaluate
```


## 音声合成・対話の検証環境（streamlit環境）について
APIサーバーに加えてstreamlitアプリを立ち上げることで、ローカルで音声合成や音声対話を試すことが出来ます。

```
make streamlit
```

streamlitアプリを上記のコマンドで立ち上げた後、ブラウザで`http://localhost:8501`にアクセスしてください。
ログイン情報は、`./streamlit/auth.yml` のusernameとパスワードを参照してください。


## ディレクトリ構成

```
├── README.md
├── poetry.lock
├── pyproject.toml
├── pytest.ini
├── PDF  # マニフェストデータ
│   ├── ...
│   └── 東京都知事選挙2024マニフェストデック.pdf
├── Text
│   └── ...
├── faiss_knowledge
├── faiss_knowledge_manifest_demo_csv_db
├── faiss_qa
├── faiss_qa_db
├── qa_datasets
├── log
│   └── ... # 対話ログ(csv, json)
├── src
│   └── ... # ソースコード
└── tests
    └── ... # テストコード
```

## 利用パッケージのライセンスについて
音声合成に利用している `azure-cognitiveservices-speech` をダウンロードした時点で、Microsoft社のライセンスに同意したものとみなされます。
詳細は[PyPI](https://pypi.org/project/azure-cognitiveservices-speech/)上のLicence informationを参照してください。
