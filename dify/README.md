# Dify Test Environment for AI Anno Backend

このドキュメントでは、AI Annoバックエンドで利用するDifyのテスト環境のセットアップと設定手順を説明します。

## 概要

Difyは、AI Annoのバックエンドで利用するLLMアプリケーション開発プラットフォームです。この環境では、Dockerを使用してDifyを起動し、OpenAIのAPIキーを設定して利用します。

## 前提条件

- Docker Desktop がインストールされていること
- Docker Compose がインストールされていること
- OpenAI APIキー（有効なサブスクリプション付き）

## セットアップ手順

### 1. Difyの起動

プロジェクトのルートディレクトリで以下のコマンドを実行します：

```bash
cd /path/to/ai-anno/dify
docker-compose up -d
```

初回起動時は、必要なDockerイメージのダウンロードに時間がかかる場合があります。

### 2. サービスの確認

以下のコマンドで全てのサービスが正常に起動していることを確認します：

```bash
docker-compose ps
```

主要なサービス：
- `dify-api-1`: APIサーバー（ポート5001）
- `dify-web-1`: Webインターフェース
- `dify-nginx-1`: リバースプロキシ（ポート80）
- `dify-db-1`: PostgreSQLデータベース
- `dify-redis-1`: Redisキャッシュ
- `dify-weaviate-1`: ベクトルデータベース

### 3. Difyへのアクセス

ブラウザで以下のURLにアクセスします：
```
http://localhost
```

### 4. アカウントの新規作成

初回アクセス時は、新規アカウントを作成します：

1. ログイン画面で「アカウントを作成」をクリック
2. メールアドレス、パスワード、ユーザー名を入力
3. アカウントを作成してログイン

### 5. Cartesiaプラグインのインストール

1. **プラグインのインストール**
   - ログイン後、左側のメニューから「プラグイン」をクリック
   - 「ローカルファイルからインストール」を選択
   - `dify/cartesia-tts.difypkg` ファイルを選択してアップロード
   - インストールが完了するまで待機（インストールに少し時間がかかります）

### 6. AI Annoアプリケーションのインポート

1. **スタジオへのアクセス**
   - 左側のメニューから「スタジオ」をクリック
   - 「DSLからインポート」ボタンをクリック

2. **YAMLファイルのインポート**
   - `AIあんの.yml` ファイルを選択
   - 「OpenAIプラグインもインポートする」オプションにチェックを入れる
   - 「インポート」ボタンをクリック

### 7. Cartesia APIキーとVoice IDの設定

1. **モデルプロバイダー設定へのアクセス**
   - 右上の歯車アイコンから「設定」をクリック
   - 「モデルプロバイダー」を選択

2. **Cartesiaの認証設定**
   - プロバイダー一覧から「CartesiaTts」を見つけて「セットアップ」をクリック

3. **APIキーとVoice IDの入力**
   - 「API Key」フィールドに、あなたのCartesia APIキーを入力
   - 「Voice ID」フィールドに、利用するVoiceのIDを入力

### 8. OpenAI APIキーの設定

1. **モデルプロバイダー設定へのアクセス**
   - 右上の歯車アイコンから「設定」をクリック
   - 「モデルプロバイダー」を選択

2. **OpenAIプロバイダーの設定**
   - プロバイダー一覧から「OpenAI」を見つけて「セットアップ」をクリック

3. **APIキーの入力**
   - 「API Key」フィールドに、あなたのOpenAI APIキーを入力
   - APIキーは `sk-` で始まる文字列です
   - OpenAI APIキーは [OpenAI Platform](https://platform.openai.com/api-keys) から取得できます

4. **設定の保存**
   - 「保存」ボタンをクリック
   - 接続テストが成功すると、緑色のチェックマークが表示されます

## システムモデルの設定

1. **モデルプロバイダー設定へのアクセス**
   - 右上の歯車アイコンから「設定」をクリック
   - 「モデルプロバイダー」を選択

2. **システムモデルの設定
   - システムモデル設定をクリック
   - Text-to-音声モデルにCartesitTtsのtts-1を選択

## アプリケーションの作成

1. **新規アプリケーション作成**
   - ダッシュボードから「アプリケーションを作成」をクリック
   - テンプレートを選択、または空のアプリケーションから開始

2. **チャットボットの設定**
   - プロンプトの設定
   - モデルの選択（GPT-4推奨）
   - パラメーター調整（Temperature、Top P等）

3. **ナレッジベースの設定**（オプション）
   - ドキュメントのアップロード
   - ベクトル検索の設定
   - RAG（Retrieval Augmented Generation）の有効化

4. **Text to Speechの有効化**
   - 

## 環境の停止と再起動

### 停止
```bash
docker-compose down
```

### 再起動
```bash
docker-compose up -d
```

## トラブルシューティング

### ログの確認
```bash
# 全サービスのログ
docker-compose logs

# 特定のサービスのログ（例：API）
docker-compose logs api

# リアルタイムログ
docker-compose logs -f
```

### サービスの再起動
```bash
# 特定のサービスのみ再起動
docker-compose restart api
```

### データベースのリセット（注意：全データが削除されます）
```bash
docker-compose down -v
rm -rf volumes/
docker-compose up -d
```

## セキュリティに関する注意事項

- このセットアップは開発・テスト用です
- 本番環境では適切なセキュリティ設定を行ってください
- APIキーは環境変数や秘密管理ツールで管理することを推奨します

## 参考資料

- [Dify公式ドキュメント](https://docs.dify.ai/)
- [OpenAI APIドキュメント](https://platform.openai.com/docs/)
- [AI Anno プロジェクト](../README.md)