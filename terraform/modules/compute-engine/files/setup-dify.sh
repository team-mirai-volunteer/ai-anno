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
    curl -L "https://github.com/docker/compose/releases/download/v2.29.1/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
    chmod +x /usr/local/bin/docker-compose
fi

# Variables (to be replaced by Terraform)
PROJECT_ID="${PROJECT_ID}"
PROJECT_NAME="${PROJECT_NAME}"
ENVIRONMENT="${ENVIRONMENT}"
REGION="${REGION}"
DB_HOST="${DB_HOST}"
DB_NAME="${DB_NAME}"
DB_USER="${DB_USER}"
GOOGLE_STORAGE_BUCKET="${GOOGLE_STORAGE_BUCKET}"
PLUGIN_STORAGE_BUCKET="${PLUGIN_STORAGE_BUCKET}"
HMAC_ACCESS_KEY_SECRET_ID="${HMAC_ACCESS_KEY_SECRET_ID}"
HMAC_SECRET_KEY_SECRET_ID="${HMAC_SECRET_KEY_SECRET_ID}"

# Fetch secrets from Secret Manager
echo "Fetching secrets from Secret Manager..."
DB_PASSWORD=$(gcloud secrets versions access latest --secret="${PROJECT_NAME}-db-password-${ENVIRONMENT}" --project="${PROJECT_ID}")
SECRET_KEY=$(gcloud secrets versions access latest --secret="${PROJECT_NAME}-dify-secret-${ENVIRONMENT}" --project="${PROJECT_ID}")
PLUGIN_DAEMON_KEY=$(gcloud secrets versions access latest --secret="${PROJECT_NAME}-plugin-daemon-${ENVIRONMENT}" --project="${PROJECT_ID}")
PLUGIN_DIFY_INNER_API_KEY=$(gcloud secrets versions access latest --secret="${PROJECT_NAME}-plugin-api-${ENVIRONMENT}" --project="${PROJECT_ID}")
SERVER_KEY=$(gcloud secrets versions access latest --secret="${PROJECT_NAME}-server-key-${ENVIRONMENT}" --project="${PROJECT_ID}")
DIFY_INNER_API_KEY=$(gcloud secrets versions access latest --secret="${PROJECT_NAME}-dify-inner-api-key-${ENVIRONMENT}" --project="${PROJECT_ID}")

# Create dify directory structure
mkdir -p /opt/dify/{nginx/ssl,volumes/{app/storage,redis/data,plugin-daemon/data}}
cd /opt/dify

# Copy docker-compose and nginx config
cat > docker-compose.yml << 'EOF'
${DOCKER_COMPOSE_CONTENT}
EOF

cat > nginx/nginx.conf << 'EOF'
${NGINX_CONFIG_CONTENT}
EOF

# Fetch HMAC keys from Secret Manager for plugin-daemon
echo "Fetching HMAC keys for plugin-daemon S3 compatibility..."
S3_ACCESS_KEY=$(gcloud secrets versions access latest --secret="$HMAC_ACCESS_KEY_SECRET_ID" --project="${PROJECT_ID}")
S3_SECRET_KEY=$(gcloud secrets versions access latest --secret="$HMAC_SECRET_KEY_SECRET_ID" --project="${PROJECT_ID}")

# GCS Service Account JSON is already base64 encoded in Secret Manager
GOOGLE_STORAGE_SERVICE_ACCOUNT_JSON=$(gcloud secrets versions access latest --secret="${PROJECT_NAME}-gcs-sa-${ENVIRONMENT}" --project="${PROJECT_ID}")

# Create .env file with mixed storage configuration
cat > .env << EOF
# Database
DB_HOST=$DB_HOST
DB_PORT=5432
DB_USERNAME=$DB_USER
DB_PASSWORD=$DB_PASSWORD
DB_DATABASE=$DB_NAME

# Secret key
SECRET_KEY=$SECRET_KEY

# Google Cloud Storage for main services (native mode)
GOOGLE_STORAGE_BUCKET_NAME=$GOOGLE_STORAGE_BUCKET
GOOGLE_STORAGE_SERVICE_ACCOUNT_JSON_BASE64=$GOOGLE_STORAGE_SERVICE_ACCOUNT_JSON

# Plugin storage with S3 compatibility (HMAC keys)
PLUGIN_S3_BUCKET_NAME=$PLUGIN_STORAGE_BUCKET
PLUGIN_S3_ACCESS_KEY=$S3_ACCESS_KEY
PLUGIN_S3_SECRET_KEY=$S3_SECRET_KEY

# Plugin configuration
DIFY_PLUGIN_DAEMON_KEY=$PLUGIN_DAEMON_KEY
DIFY_PLUGIN_DIFY_INNER_API_KEY=$PLUGIN_DIFY_INNER_API_KEY

# Additional Dify configuration
SERVER_KEY=$SERVER_KEY
DIFY_INNER_API_URL=http://api:5001
DIFY_INNER_API_KEY=$DIFY_INNER_API_KEY

# Mail configuration (disabled)
MAIL_TYPE=

EOF

# Set permissions
chmod 600 .env
chown -R 1000:1000 volumes/

# Authenticate with Artifact Registry
gcloud auth configure-docker ${REGION}-docker.pkg.dev

# Start services
docker-compose up -d

# Wait for services to be ready
echo "Waiting for services to start..."
sleep 30

# Check service status
docker-compose ps

echo "Dify setup completed!"
echo "Main services use native Google Storage API"
echo "Plugin daemon uses S3 compatibility mode with endpoint: https://storage.googleapis.com"
echo "You can access Dify at http://$(curl -s http://metadata.google.internal/computeMetadata/v1/instance/network-interfaces/0/access-configs/0/external-ip -H "Metadata-Flavor: Google")"
