# Terraform Cloud Setup Guide

## 1. ワークスペース作成

1. Terraform Cloudで新しいワークスペースを作成
2. **Version Control Workflow** を選択
3. GitHubリポジトリを連携

## 2. ワークスペース設定

### General Settings
- **Execution Mode**: Remote
- **Working Directory**: `terraform/environments/staging`
- **Auto Apply**: 任意（手動承認推奨）

### VCS Settings
- **VCS branch**: `main` または適切なブランチ
- **Include submodules on clone**: No

## 3. 変数設定

### Terraform Variables

| Variable                 | Value                    | Sensitive |
|--------------------------|--------------------------|-----------|
| project_id               | your-gcp-project-id      | No        |
| project_number           | your-gcp-project-number  | No        |
| region                   | asia-northeast1          | No        |
| dify_secret_key          | (生成した32文字のキー)   | Yes       |
| gcs_service_account_json | (サービスアカウントJSON) | Yes       |

#### dify_secret_keyの生成方法
```bash
openssl rand -base64 32
```

#### gcs_service_account_jsonの作成方法
```bash
# サービスアカウント作成
gcloud iam service-accounts create dify-storage-sa \
  --display-name="Dify Storage Service Account"

# 権限付与
gcloud projects add-iam-policy-binding YOUR_PROJECT_ID \
  --member="serviceAccount:dify-storage-sa@YOUR_PROJECT_ID.iam.gserviceaccount.com" \
  --role="roles/storage.objectAdmin"

# キー作成
gcloud iam service-accounts keys create dify-storage-key.json \
  --iam-account=dify-storage-sa@YOUR_PROJECT_ID.iam.gserviceaccount.com

# JSONの内容をコピーしてTerraform Cloudに設定
cat dify-storage-key.json
```

### Environment Variables

| Variable           | Value                                   | Sensitive |
|--------------------|-----------------------------------------|-----------|
| GOOGLE_CREDENTIALS | (Terraform実行用サービスアカウントJSON) | Yes       |

#### GOOGLE_CREDENTIALSの作成方法
```bash
# Terraform実行用サービスアカウント作成
gcloud iam service-accounts create terraform-sa \
  --display-name="Terraform Service Account"

# 必要な権限を付与
PROJECT_ID=your-project-id
SA_EMAIL=terraform-sa@${PROJECT_ID}.iam.gserviceaccount.com

# 基本的な権限
gcloud projects add-iam-policy-binding ${PROJECT_ID} \
  --member="serviceAccount:${SA_EMAIL}" \
  --role="roles/editor"

# Cloud SQL Admin
gcloud projects add-iam-policy-binding ${PROJECT_ID} \
  --member="serviceAccount:${SA_EMAIL}" \
  --role="roles/cloudsql.admin"

# Service Networking Admin
gcloud projects add-iam-policy-binding ${PROJECT_ID} \
  --member="serviceAccount:${SA_EMAIL}" \
  --role="roles/servicenetworking.networksAdmin"

# キー作成
gcloud iam service-accounts keys create terraform-key.json \
  --iam-account=${SA_EMAIL}

# JSONの内容をコピー
cat terraform-key.json
```

## 4. 実行前の準備

### 必要なAPIの有効化
```bash
gcloud services enable compute.googleapis.com
gcloud services enable cloudrun.googleapis.com
gcloud services enable sqladmin.googleapis.com
gcloud services enable artifactregistry.googleapis.com
gcloud services enable secretmanager.googleapis.com
gcloud services enable servicenetworking.googleapis.com
gcloud services enable vpcaccess.googleapis.com
gcloud services enable cloudbuild.googleapis.com
```

### Cloud Buildサービスアカウントの準備
```bash
# Cloud BuildがArtifact Registryにアクセスできるように権限付与
PROJECT_NUMBER=$(gcloud projects describe ${PROJECT_ID} --format="value(projectNumber)")
gcloud projects add-iam-policy-binding ${PROJECT_ID} \
  --member="serviceAccount:${PROJECT_NUMBER}@cloudbuild.gserviceaccount.com" \
  --role="roles/artifactregistry.writer"
```

## 5. 実行

1. Terraform Cloudで **Queue Plan** をクリック
2. プランを確認
3. 問題なければ **Confirm & Apply**

## 6. 実行後の作業

### Dockerイメージのビルドとプッシュ

```bash
# Artifact Registry URLを取得（Terraform outputから）
REGISTRY_URL=$(terraform output -raw artifact_registry_url)

# 認証
gcloud auth configure-docker asia-northeast1-docker.pkg.dev

# Difyのソースコードを取得
git clone https://github.com/langgenius/dify.git
cd dify

# イメージのビルド例
docker build -t ${REGISTRY_URL}/api:latest -f api/Dockerfile .
docker build -t ${REGISTRY_URL}/web:latest -f web/Dockerfile .
docker build -t ${REGISTRY_URL}/worker:latest -f api/Dockerfile . --build-arg EDITION=WORKER
docker build -t ${REGISTRY_URL}/nginx:latest -f docker/nginx/Dockerfile .

# Sandboxイメージ
docker pull langgenius/dify-sandbox:latest
docker tag langgenius/dify-sandbox:latest ${REGISTRY_URL}/sandbox:latest

# プッシュ
docker push ${REGISTRY_URL}/api:latest
docker push ${REGISTRY_URL}/web:latest
docker push ${REGISTRY_URL}/worker:latest
docker push ${REGISTRY_URL}/nginx:latest
docker push ${REGISTRY_URL}/sandbox:latest
```

## トラブルシューティング

### Permission Deniedエラー
- サービスアカウントの権限を確認
- 必要なAPIが有効化されているか確認

### VPC作成エラー
- Service Networking APIが有効か確認
- プロジェクトのquotaを確認

### Cloud SQL接続エラー
- VPCコネクタのCIDRが重複していないか確認
- Private Service Connectionが正しく設定されているか確認
