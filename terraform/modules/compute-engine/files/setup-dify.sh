#!/bin/bash
set -e

# Variables (to be replaced by Terraform)
PROJECT_ID="${PROJECT_ID}"
REGION="${REGION}"
DB_HOST="${DB_HOST}"
DB_NAME="${DB_NAME}"
DB_USER="${DB_USER}"
DB_PASSWORD="${DB_PASSWORD}"
SECRET_KEY="${SECRET_KEY}"
GOOGLE_STORAGE_BUCKET="${GOOGLE_STORAGE_BUCKET}"
GOOGLE_STORAGE_SERVICE_ACCOUNT_JSON="${GOOGLE_STORAGE_SERVICE_ACCOUNT_JSON}"
PLUGIN_DAEMON_KEY="${PLUGIN_DAEMON_KEY}"
PLUGIN_DIFY_INNER_API_KEY="${PLUGIN_DIFY_INNER_API_KEY}"
PLUGIN_STORAGE_BUCKET="${PLUGIN_STORAGE_BUCKET}"
GCS_CREDENTIALS="${GCS_CREDENTIALS}"

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
DB_HOST=${DB_HOST}
DB_PORT=5432
DB_USERNAME=${DB_USER}
DB_PASSWORD=${DB_PASSWORD}
DB_DATABASE=${DB_NAME}

# Secret key
SECRET_KEY=${SECRET_KEY}

# Google Cloud Storage
GOOGLE_STORAGE_BUCKET=${GOOGLE_STORAGE_BUCKET}
GOOGLE_STORAGE_SERVICE_ACCOUNT_JSON=${GOOGLE_STORAGE_SERVICE_ACCOUNT_JSON}

# Plugin configuration
DIFY_PLUGIN_DAEMON_KEY=${PLUGIN_DAEMON_KEY}
DIFY_PLUGIN_DIFY_INNER_API_KEY=${PLUGIN_DIFY_INNER_API_KEY}
PLUGIN_STORAGE_BUCKET=${PLUGIN_STORAGE_BUCKET}
GCS_CREDENTIALS=${GCS_CREDENTIALS}

# Mail configuration
MAIL_TYPE=smtp
MAIL_DEFAULT_SEND_FROM=noreply@example.com

# Code execution limits
CODE_MAX_NUMBER=3
CODE_MIN_NUMBER=0
CODE_MAX_STRING_LENGTH=80000
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