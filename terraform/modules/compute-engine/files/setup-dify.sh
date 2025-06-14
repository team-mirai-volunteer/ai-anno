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

# Fetch secrets from Secret Manager
echo "Fetching secrets from Secret Manager..."
DB_PASSWORD=$(gcloud secrets versions access latest --secret="${PROJECT_NAME}-db-password-${ENVIRONMENT}" --project="${PROJECT_ID}")
SECRET_KEY=$(gcloud secrets versions access latest --secret="${PROJECT_NAME}-dify-secret-${ENVIRONMENT}" --project="${PROJECT_ID}")
# GCS Service Account JSON needs to be base64 encoded for .env file
GCS_SA_JSON=$(gcloud secrets versions access latest --secret="${PROJECT_NAME}-gcs-sa-${ENVIRONMENT}" --project="${PROJECT_ID}")
GOOGLE_STORAGE_SERVICE_ACCOUNT_JSON=$(echo "$GCS_SA_JSON" | base64 -w 0)
PLUGIN_DAEMON_KEY=$(gcloud secrets versions access latest --secret="${PROJECT_NAME}-plugin-daemon-${ENVIRONMENT}" --project="${PROJECT_ID}")
PLUGIN_DIFY_INNER_API_KEY=$(gcloud secrets versions access latest --secret="${PROJECT_NAME}-plugin-api-${ENVIRONMENT}" --project="${PROJECT_ID}")
# Fetch additional secrets for Dify
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

# Create .env file with database and storage configuration
cat > .env << EOF
# Database
DB_HOST=$DB_HOST
DB_PORT=5432
DB_USERNAME=$DB_USER
DB_PASSWORD=$DB_PASSWORD
DB_DATABASE=$DB_NAME

# Secret key
SECRET_KEY=$SECRET_KEY

# Google Cloud Storage
GOOGLE_STORAGE_BUCKET=$GOOGLE_STORAGE_BUCKET
GOOGLE_STORAGE_SERVICE_ACCOUNT_JSON=$GOOGLE_STORAGE_SERVICE_ACCOUNT_JSON

# Plugin configuration
DIFY_PLUGIN_DAEMON_KEY=$PLUGIN_DAEMON_KEY
DIFY_PLUGIN_DIFY_INNER_API_KEY=$PLUGIN_DIFY_INNER_API_KEY
PLUGIN_STORAGE_BUCKET=$PLUGIN_STORAGE_BUCKET

# Additional Dify configuration
SERVER_KEY=$SERVER_KEY
DIFY_INNER_API_URL=http://api:5001
DIFY_INNER_API_KEY=$DIFY_INNER_API_KEY

# Mail configuration (disabled - set MAIL_TYPE=smtp and add SMTP_SERVER/SMTP_PORT to enable)
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
echo "You can access Dify at http://$(curl -s http://metadata.google.internal/computeMetadata/v1/instance/network-interfaces/0/access-configs/0/external-ip -H "Metadata-Flavor: Google")"
