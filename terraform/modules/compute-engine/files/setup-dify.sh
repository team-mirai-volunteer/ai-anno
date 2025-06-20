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

# Export environment variables that will be used by docker compose
export DB_USERNAME=$DB_USER
export DB_PASSWORD=$DB_PASSWORD
export DB_HOST=$DB_HOST
export DB_PORT=5432
export DB_DATABASE=$DB_NAME
export PGUSER=$DB_USER
export POSTGRES_PASSWORD=$DB_PASSWORD
export POSTGRES_DB=$DB_NAME
export SECRET_KEY=$SECRET_KEY
export GOOGLE_STORAGE_BUCKET_NAME=$GOOGLE_STORAGE_BUCKET_NAME
export GOOGLE_STORAGE_SERVICE_ACCOUNT_JSON_BASE64=$GOOGLE_STORAGE_SERVICE_ACCOUNT_JSON_BASE64
export PLUGIN_DAEMON_KEY=$PLUGIN_DAEMON_KEY
export PLUGIN_DIFY_INNER_API_KEY=$PLUGIN_DIFY_INNER_API_KEY

# For plugin daemon S3 compatibility mode
export PLUGIN_STORAGE_TYPE=s3
export PLUGIN_S3_ENDPOINT=https://storage.googleapis.com
export PLUGIN_S3_BUCKET_NAME=$PLUGIN_STORAGE_BUCKET_NAME
export PLUGIN_AWS_ACCESS_KEY=$S3_ACCESS_KEY
export PLUGIN_AWS_SECRET_KEY=$S3_SECRET_KEY
export PLUGIN_AWS_REGION=$REGION
export PLUGIN_S3_USE_PATH_STYLE=false

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
