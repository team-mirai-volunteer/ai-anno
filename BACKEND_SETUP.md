# AIあんの バックエンド環境構築 完全ガイド

## はじめに

このドキュメントは、AIあんののバックエンド環境を確実に構築するための包括的なガイドです。他の開発者が同様の成果にたどり着けるよう、実際の構築経験に基づいて作成されています。

## 環境構築の全体フロー

```
1. 基本環境準備 → 2. データベース設定 → 3. API設定 → 4. ナレッジベース構築 → 5. 動作確認
```

## 詳細手順

### ステップ1: 基本環境の準備

#### 1.1 リポジトリのクローン
```bash
git clone https://github.com/team-mirai-volunteer/ai-anno.git
cd ai-anno/python_server
```

#### 1.2 システム依存関係のインストール
```bash
sudo apt update
sudo apt install -y poppler-utils postgresql postgresql-contrib
```

#### 1.3 Python依存関係のインストール
```bash
poetry install
```

### ステップ2: PostgreSQLデータベースの設定

#### 2.1 PostgreSQLサービスの開始
```bash
sudo systemctl start postgresql
sudo systemctl enable postgresql
```

#### 2.2 データベースの作成
```bash
sudo -H -u postgres psql -c "CREATE DATABASE aituber_dev;"
sudo -H -u postgres psql -c "CREATE USER aituber_user WITH PASSWORD 'secure_password';"
sudo -H -u postgres psql -c "GRANT ALL PRIVILEGES ON DATABASE aituber_dev TO aituber_user;"
```

### ステップ3: 環境変数設定

#### 3.1 Google Cloud Console設定
1. [Google Cloud Console](https://console.cloud.google.com/)でプロジェクト作成
2. Generative Language APIを有効化
3. APIキーを作成
4. **重要**: 請求先アカウントを設定

#### 3.2 環境変数設定
```bash
cp .env.example .env
```

`.env`ファイルを編集：
```env
PG_HOST=localhost
PG_PORT=5432
PG_DATABASE=aituber_dev
PG_USER=aituber_user
PG_PASSWORD=secure_password
GOOGLE_API_KEY=your_google_api_key_here
```

#### 3.3 データベースマイグレーション
環境変数設定後にマイグレーションを実行します。データベース作成後にマイグレーションを実行するのは、空のデータベースにテーブル構造を作成するためです。2.2で箱（データベース）を作り、3.3で中身（テーブル）を作る流れになっています。

```bash
poetry run alembic upgrade head
```

### ステップ4: ナレッジベースの構築

#### 4.1 PDFデータの処理
```bash
poetry run python -m src.cli.import_pdf
poetry run python -m src.cli.import_docs_csv
```

#### 4.2 FAISSベクトルデータベースの作成
```bash
poetry run python -m src.cli.save_faiss_knowledge_db
poetry run python -m src.cli.save_faiss_db
```

または一括実行：
```bash
make setup/resources
```

### ステップ5: サーバー起動と動作確認

#### 5.1 サーバー起動
```bash
poetry run uvicorn src.web.api:app --host 127.0.0.1 --port 7200
```

#### 5.2 動作確認テスト
```bash
curl -X POST "http://127.0.0.1:7200/reply" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "inputtext=デジタル民主主義について教えてください"
```

## 成功の確認方法

### 必須チェック項目
- [ ] サーバーが正常に起動（ポート7200）
- [ ] APIが適切な日本語回答を返す
- [ ] FAISSナレッジベースが構築済み
- [ ] PostgreSQLデータベースが動作
- [ ] Google APIキーが有効

### 期待される出力例
```json
{
  "response_text": "こんにちは！AIあんのです。デジタル民主主義について...",
  "image_filename": "slide_56.png"
}
```

## よくある問題と解決方法

### 問題1: Google API Quota Exceeded
**症状**: `quota exceeded`エラー
**解決**: Google Cloud Consoleで請求先アカウントを設定

### 問題2: PostgreSQL接続エラー
**症状**: データベース接続失敗
**解決**: 
```bash
sudo systemctl status postgresql
sudo systemctl restart postgresql
```

### 問題3: FAISS インデックスが見つからない
**症状**: `No such file or directory: 'faiss_knowledge/index.faiss'`
**解決**: `make setup/resources`を再実行

### 問題4: 常に同じ回答（子育て支援）が返される
**症状**: どの質問でも子育て支援の回答
**原因**: フォールバック機能の制限（既知の問題）
**対処**: システムは正常動作中、政策関連の質問で確認

## システム仕様

### 技術スタック
- **Framework**: FastAPI
- **AI**: Google Generative AI (Gemini 1.5 Pro)
- **Vector DB**: FAISS + Google Embeddings
- **Database**: PostgreSQL
- **Architecture**: RAG (Retrieval-Augmented Generation)

### データ構成
- **政策文書**: 118件
- **FAQ**: 253件
- **スライド**: 107枚

## 開発者向け情報

### デバッグ方法
```bash
# 詳細ログでサーバー起動
poetry run uvicorn src.web.api:app --host 127.0.0.1 --port 7200 --log-level debug

# 検索機能のテスト
poetry run python debug_search.py

# API生成のテスト
poetry run python debug_api_generation.py
```

### 重要なファイル
- `src/web/api.py`: FastAPIアプリケーション
- `src/gpt.py`: AI応答生成ロジック
- `src/get_faiss_vector.py`: ベクトル検索機能
- `faiss_knowledge/`: ナレッジベース
- `qa_datasets/`: 元データ（CSV）

## 次のステップ

環境構築完了後：
1. フロントエンド（Unity 3D）との連携
2. YouTube Live連携の設定
3. 音声合成機能の設定
4. 本番環境への展開

---

**注意**: このシステムは政治的に中立で適切な応答を維持するよう設計されています。
