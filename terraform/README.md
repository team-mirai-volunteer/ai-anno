# Dify on Google Cloud Platform - Terraform

このTerraformコードは、Google Cloud Platform上にDifyを構築するためのInfrastructure as Codeです。

## アーキテクチャ

- **Cloud Run**: メインアプリケーション、Worker、Sandboxサービス
- **Cloud SQL**: PostgreSQL 15 (pgvector拡張付き)
- **Cloud Storage**: ファイルアップロード、モデルキャッシュ、バックアップ
- **VPC**: プライベートネットワーク構成
- **Artifact Registry**: Dockerイメージリポジトリ

### 特徴

- RedisをCloud Run内のサイドカーコンテナとして実行（コスト最適化）
- YouTube Connector等の拡張サービスの追加を考慮した設計
- ステージング/本番環境の分離
- 自動スケーリング対応

## 前提条件

- Google Cloud SDKのインストール
- Terraform 1.5.0以上
- 適切な権限を持つGCPプロジェクト
- Terraform Cloudアカウント（state管理用）

## セットアップ

### 1. GCPプロジェクトの準備

```bash
# 必要なAPIを有効化
gcloud services enable compute.googleapis.com
gcloud services enable cloudrun.googleapis.com
gcloud services enable sqladmin.googleapis.com
gcloud services enable artifactregistry.googleapis.com
gcloud services enable secretmanager.googleapis.com
gcloud services enable servicenetworking.googleapis.com
gcloud services enable vpcaccess.googleapis.com
```

### 2. サービスアカウントの作成

```bash
# Storage用のサービスアカウント作成
gcloud iam service-accounts create dify-storage-sa \
  --display-name="Dify Storage Service Account"

# 必要な権限を付与
gcloud projects add-iam-policy-binding YOUR_PROJECT_ID \
  --member="serviceAccount:dify-storage-sa@YOUR_PROJECT_ID.iam.gserviceaccount.com" \
  --role="roles/storage.objectAdmin"

# キーをダウンロード
gcloud iam service-accounts keys create dify-storage-key.json \
  --iam-account=dify-storage-sa@YOUR_PROJECT_ID.iam.gserviceaccount.com
```

### 3. Terraformの設定

```bash
# ステージング環境へ移動
cd terraform/environments/staging

# terraform.tfvarsを作成
cp terraform.tfvars.example terraform.tfvars

# terraform.tfvarsを編集して必要な値を設定
# - project_id
# - project_number
# - dify_secret_key (openssl rand -base64 32 で生成)
# - gcs_service_account_json (上記でダウンロードしたJSONの内容)
```

### 4. Terraformの実行

```bash
# 初期化
terraform init

# プランの確認
terraform plan

# 適用
terraform apply
```

## Dockerイメージのビルドとプッシュ

```bash
# Artifact Registryへの認証
gcloud auth configure-docker asia-northeast1-docker.pkg.dev

# イメージのビルドとプッシュ（例）
docker build -t asia-northeast1-docker.pkg.dev/YOUR_PROJECT_ID/dify-docker-staging/api:latest ./api
docker push asia-northeast1-docker.pkg.dev/YOUR_PROJECT_ID/dify-docker-staging/api:latest
```

## 環境変数

主要な環境変数は`terraform/environments/staging/main.tf`で設定されています。追加の環境変数が必要な場合は、`common_env_vars`に追加してください。

## トラブルシューティング

### Cloud SQLへの接続エラー

- VPCコネクタが正しく設定されているか確認
- Cloud SQL Admin APIが有効になっているか確認
- サービスアカウントに必要な権限があるか確認

### ストレージアクセスエラー

- GCSサービスアカウントのJSONが正しく設定されているか確認
- バケット名が正しいか確認
- サービスアカウントに必要な権限があるか確認

## 今後の拡張

- YouTube Connectorサービスの追加
- Cloud CDNの統合
- Cloud Armorによるセキュリティ強化
- Cloud Monitoringダッシュボードの設定