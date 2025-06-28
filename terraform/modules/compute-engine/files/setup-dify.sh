#!/bin/bash
set -e

# Install Docker and Docker Compose if not already installed
if ! command -v docker &> /dev/null; then
    echo "Installing Docker..."
    apt-get update
    apt-get upgrade -y
    curl -fsSL https://get.docker.com -o get-docker.sh
    sh get-docker.sh
    rm get-docker.sh
fi

if ! command -v docker-compose &> /dev/null; then
    echo "Installing Docker Compose..."
    curl -L "https://github.com/docker/compose/releases/download/v2.37.1/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
    chmod +x /usr/local/bin/docker-compose
fi

# Variables (to be replaced by Terraform)
# These variables are template placeholders that Terraform will replace when creating the startup script
PROJECT_ID="${PROJECT_ID}"                                  # GCP Project ID
PROJECT_NAME="${PROJECT_NAME}"                              # Project name (e.g., "ai-anno")
ENVIRONMENT="${ENVIRONMENT}"                                # Environment name (e.g., "dev", "prod")
REGION="${REGION}"                                          # GCP Region (e.g., "asia-northeast1")
DB_HOST="${DB_HOST}"                                        # Cloud SQL instance connection name
DB_NAME="${DB_NAME}"                                        # Database name
DB_USER="${DB_USER}"                                        # Database username
GOOGLE_STORAGE_BUCKET="${GOOGLE_STORAGE_BUCKET}"            # GCS bucket for Dify file uploads
PLUGIN_STORAGE_BUCKET="${PLUGIN_STORAGE_BUCKET}"            # GCS bucket for plugin storage
HMAC_ACCESS_KEY_SECRET_ID="${HMAC_ACCESS_KEY_SECRET_ID}"    # Secret Manager ID for HMAC access key
HMAC_SECRET_KEY_SECRET_ID="${HMAC_SECRET_KEY_SECRET_ID}"    # Secret Manager ID for HMAC secret key

# Fetch secrets from Secret Manager
echo "Fetching secrets from Secret Manager..."
DB_PASSWORD=$(gcloud secrets versions access latest --secret="${PROJECT_NAME}-db-password-${ENVIRONMENT}" --project="${PROJECT_ID}")
SECRET_KEY=$(gcloud secrets versions access latest --secret="${PROJECT_NAME}-dify-secret-${ENVIRONMENT}" --project="${PROJECT_ID}")
PLUGIN_DAEMON_KEY=$(gcloud secrets versions access latest --secret="${PROJECT_NAME}-plugin-daemon-${ENVIRONMENT}" --project="${PROJECT_ID}")
PLUGIN_DIFY_INNER_API_KEY=$(gcloud secrets versions access latest --secret="${PROJECT_NAME}-plugin-api-${ENVIRONMENT}" --project="${PROJECT_ID}")
SERVER_KEY=$(gcloud secrets versions access latest --secret="${PROJECT_NAME}-server-key-${ENVIRONMENT}" --project="${PROJECT_ID}")
DIFY_INNER_API_KEY=$(gcloud secrets versions access latest --secret="${PROJECT_NAME}-dify-inner-api-key-${ENVIRONMENT}" --project="${PROJECT_ID}")
INIT_PASSWORD=$(gcloud secrets versions access latest --secret="${PROJECT_NAME}-init-password-${ENVIRONMENT}" --project="${PROJECT_ID}")
REDIS_PASSWORD=$(gcloud secrets versions access latest --secret="${PROJECT_NAME}-redis-password-${ENVIRONMENT}" --project="${PROJECT_ID}")
CODE_EXECUTION_API_KEY=$(gcloud secrets versions access latest --secret="${PROJECT_NAME}-code-execution-api-key-${ENVIRONMENT}" --project="${PROJECT_ID}")

# Clone Dify 1.4.3 from git
echo "Cloning Dify 1.4.3..."
cd /opt
git clone --branch 1.4.3 --depth 1 https://github.com/langgenius/dify.git
cd /opt/dify

# Create necessary directories
mkdir -p volumes/{app/storage,redis/data,plugin-daemon/data}

# Fetch HMAC keys from Secret Manager for plugin-daemon
echo "Fetching HMAC keys for plugin-daemon S3 compatibility..."
S3_ACCESS_KEY=$(gcloud secrets versions access latest --secret="$HMAC_ACCESS_KEY_SECRET_ID" --project="${PROJECT_ID}")
S3_SECRET_KEY=$(gcloud secrets versions access latest --secret="$HMAC_SECRET_KEY_SECRET_ID" --project="${PROJECT_ID}")

# GCS Service Account JSON is already base64 encoded in Secret Manager
GOOGLE_STORAGE_SERVICE_ACCOUNT_JSON_BASE64=$(gcloud secrets versions access latest --secret="${PROJECT_NAME}-gcs-sa-${ENVIRONMENT}" --project="${PROJECT_ID}")

# Copy the default .env.example
cp docker/.env.example docker/.env

# Append environment variables to .env file
cat >> docker/.env << EOF

# Database Configuration
DB_USERNAME=$DB_USER
DB_PASSWORD=$DB_PASSWORD
DB_HOST=$DB_HOST
DB_PORT=5432
DB_DATABASE=$DB_NAME
PGUSER=$DB_USER
POSTGRES_PASSWORD=$DB_PASSWORD
POSTGRES_DB=$DB_NAME

# Dify Configuration
SECRET_KEY=$SECRET_KEY
INIT_PASSWORD=$INIT_PASSWORD
REDIS_PASSWORD=$REDIS_PASSWORD
PLUGIN_DAEMON_KEY=$PLUGIN_DAEMON_KEY
PLUGIN_DIFY_INNER_API_KEY=$PLUGIN_DIFY_INNER_API_KEY
CODE_EXECUTION_API_KEY=$CODE_EXECUTION_API_KEY

# Storage Configuration
GOOGLE_STORAGE_BUCKET_NAME=$GOOGLE_STORAGE_BUCKET
GOOGLE_STORAGE_SERVICE_ACCOUNT_JSON_BASE64=$GOOGLE_STORAGE_SERVICE_ACCOUNT_JSON_BASE64

# Celery Configuration
CELERY_BROKER_URL=redis://:$REDIS_PASSWORD@redis:6379/1

# Plugin Storage Configuration (S3 compatibility mode)
PLUGIN_STORAGE_TYPE=s3
PLUGIN_S3_ENDPOINT=https://storage.googleapis.com
PLUGIN_STORAGE_OSS_BUCKET=$PLUGIN_STORAGE_BUCKET
PLUGIN_AWS_ACCESS_KEY=$S3_ACCESS_KEY
PLUGIN_AWS_SECRET_KEY=$S3_SECRET_KEY
PLUGIN_AWS_REGION=$REGION
PLUGIN_S3_USE_PATH_STYLE=false

# API URLs Configuration
CONSOLE_API_URL=https://stg-ai-anno.ngo-go.com
CONSOLE_WEB_URL=https://stg-ai-anno.ngo-go.com
SERVICE_API_URL=https://stg-ai-anno.ngo-go.com
APP_API_URL=https://stg-ai-anno.ngo-go.com
APP_WEB_URL=https://stg-ai-anno.ngo-go.com
FILES_URL=https://stg-ai-anno.ngo-go.com
CONSOLE_CORS_ALLOW_ORIGINS=https://stg-ai-anno.ngo-go.com
WEB_API_CORS_ALLOW_ORIGINS=https://stg-ai-anno.ngo-go.com
EOF

# Set permissions
chmod 600 docker/.env
chown -R 1000:1000 volumes/

# Authenticate with Artifact Registry
gcloud auth configure-docker ${REGION}-docker.pkg.dev

# Start services using the docker directory
cd docker
docker compose up -d

# Wait for services to be ready
echo "Waiting for services to start..."
sleep 30

# Check service status
docker compose ps

echo "Dify setup completed!"
echo "Main services use native Google Storage API"
echo "Plugin daemon uses S3 compatibility mode with endpoint: https://storage.googleapis.com"
echo "You can access Dify at http://$(curl -s http://metadata.google.internal/computeMetadata/v1/instance/network-interfaces/0/access-configs/0/external-ip -H "Metadata-Flavor: Google")"
