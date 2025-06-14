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
# ------------------------------
# Common Variables
# ------------------------------
CONSOLE_API_URL=
CONSOLE_WEB_URL=
SERVICE_API_URL=
APP_API_URL=
APP_WEB_URL=
FILES_URL=

# ------------------------------
# Server Configuration
# ------------------------------
LOG_LEVEL=INFO
LOG_FILE=/app/logs/server.log
LOG_FILE_MAX_SIZE=20
LOG_FILE_BACKUP_COUNT=5
LOG_DATEFORMAT=%Y-%m-%d %H:%M:%S
LOG_TZ=UTC

DEBUG=false
FLASK_DEBUG=false
ENABLE_REQUEST_LOGGING=False
SECRET_KEY=$SECRET_KEY
INIT_PASSWORD=
DEPLOY_ENV=PRODUCTION
CHECK_UPDATE_URL=https://updates.dify.ai
OPENAI_API_BASE=https://api.openai.com/v1
MIGRATION_ENABLED=true
FILES_ACCESS_TIMEOUT=300
ACCESS_TOKEN_EXPIRE_MINUTES=60
REFRESH_TOKEN_EXPIRE_DAYS=30
APP_MAX_ACTIVE_REQUESTS=0
APP_MAX_EXECUTION_TIME=1200

# ------------------------------
# Container Startup Related Configuration
# Only effective when starting with docker image or docker-compose.
# ------------------------------
DIFY_BIND_ADDRESS=0.0.0.0
DIFY_PORT=5001
SERVER_WORKER_AMOUNT=7
SERVER_WORKER_CLASS=gevent
SERVER_WORKER_CONNECTIONS=10
CELERY_WORKER_CLASS=
GUNICORN_TIMEOUT=360
CELERY_WORKER_AMOUNT=
CELERY_MAX_WORKERS=
CELERY_MIN_WORKERS=
API_TOOL_DEFAULT_CONNECT_TIMEOUT=10
API_TOOL_DEFAULT_READ_TIMEOUT=60

# -------------------------------
# Datasource Configuration
# --------------------------------
ENABLE_WEBSITE_JINAREADER=true
ENABLE_WEBSITE_FIRECRAWL=true
ENABLE_WEBSITE_WATERCRAWL=true

# ------------------------------
# Database Configuration
# The database uses PostgreSQL. Please use the public schema.
# It is consistent with the configuration in the 'db' service below.
# ------------------------------
DB_USERNAME=$DB_USER
DB_PASSWORD=$DB_PASSWORD
DB_HOST=$DB_HOST
DB_PORT=5432
DB_DATABASE=$DB_NAME
SQLALCHEMY_POOL_SIZE=30
SQLALCHEMY_POOL_RECYCLE=3600
SQLALCHEMY_ECHO=false
POSTGRES_MAX_CONNECTIONS=100
POSTGRES_SHARED_BUFFERS=128MB
POSTGRES_WORK_MEM=4MB
POSTGRES_MAINTENANCE_WORK_MEM=64MB
POSTGRES_EFFECTIVE_CACHE_SIZE=4096MB

# ------------------------------
# Redis Configuration
# This Redis configuration is used for caching and for pub/sub during conversation.
# ------------------------------
REDIS_HOST=redis
REDIS_PORT=6379
REDIS_USERNAME=
REDIS_PASSWORD=difyai123456
REDIS_USE_SSL=false
REDIS_DB=0
REDIS_USE_SENTINEL=false
REDIS_SENTINELS=
REDIS_SENTINEL_SERVICE_NAME=
REDIS_SENTINEL_USERNAME=
REDIS_SENTINEL_PASSWORD=
REDIS_SENTINEL_SOCKET_TIMEOUT=0.1
REDIS_USE_CLUSTERS=false
REDIS_CLUSTERS=
REDIS_CLUSTERS_PASSWORD=

# ------------------------------
# Celery Configuration
# ------------------------------
CELERY_BROKER_URL=redis://:difyai123456@redis:6379/1
BROKER_USE_SSL=false
CELERY_USE_SENTINEL=false
CELERY_SENTINEL_MASTER_NAME=
CELERY_SENTINEL_SOCKET_TIMEOUT=0.1

# ------------------------------
# CORS Configuration
# Used to set the front-end cross-domain access policy.
# ------------------------------
WEB_API_CORS_ALLOW_ORIGINS=*
CONSOLE_CORS_ALLOW_ORIGINS=*

# ------------------------------
# File Storage Configuration
# ------------------------------
STORAGE_TYPE=opendal
OPENDAL_SCHEME=fs
OPENDAL_FS_ROOT=storage

S3_ENDPOINT=
S3_REGION=us-east-1
S3_BUCKET_NAME=difyai
S3_ACCESS_KEY=
S3_SECRET_KEY=
S3_USE_AWS_MANAGED_IAM=false

AZURE_BLOB_ACCOUNT_NAME=difyai
AZURE_BLOB_ACCOUNT_KEY=difyai
AZURE_BLOB_CONTAINER_NAME=difyai-container
AZURE_BLOB_ACCOUNT_URL=https://<your_account_name>.blob.core.windows.net

GOOGLE_STORAGE_BUCKET_NAME=$GOOGLE_STORAGE_BUCKET_NAME
GOOGLE_STORAGE_SERVICE_ACCOUNT_JSON_BASE64=$GOOGLE_STORAGE_SERVICE_ACCOUNT_JSON_BASE64

ALIYUN_OSS_BUCKET_NAME=your-bucket-name
ALIYUN_OSS_ACCESS_KEY=your-access-key
ALIYUN_OSS_SECRET_KEY=your-secret-key
ALIYUN_OSS_ENDPOINT=https://oss-ap-southeast-1-internal.aliyuncs.com
ALIYUN_OSS_REGION=ap-southeast-1
ALIYUN_OSS_AUTH_VERSION=v4
ALIYUN_OSS_PATH=your-path

TENCENT_COS_BUCKET_NAME=your-bucket-name
TENCENT_COS_SECRET_KEY=your-secret-key
TENCENT_COS_SECRET_ID=your-secret-id
TENCENT_COS_REGION=your-region
TENCENT_COS_SCHEME=your-scheme

OCI_ENDPOINT=https://your-object-storage-namespace.compat.objectstorage.us-ashburn-1.oraclecloud.com
OCI_BUCKET_NAME=your-bucket-name
OCI_ACCESS_KEY=your-access-key
OCI_SECRET_KEY=your-secret-key
OCI_REGION=us-ashburn-1

HUAWEI_OBS_BUCKET_NAME=your-bucket-name
HUAWEI_OBS_SECRET_KEY=your-secret-key
HUAWEI_OBS_ACCESS_KEY=your-access-key
HUAWEI_OBS_SERVER=your-server-url

VOLCENGINE_TOS_BUCKET_NAME=your-bucket-name
VOLCENGINE_TOS_SECRET_KEY=your-secret-key
VOLCENGINE_TOS_ACCESS_KEY=your-access-key
VOLCENGINE_TOS_ENDPOINT=your-server-url
VOLCENGINE_TOS_REGION=your-region

BAIDU_OBS_BUCKET_NAME=your-bucket-name
BAIDU_OBS_SECRET_KEY=your-secret-key
BAIDU_OBS_ACCESS_KEY=your-access-key
BAIDU_OBS_ENDPOINT=your-server-url

SUPABASE_BUCKET_NAME=your-bucket-name
SUPABASE_API_KEY=your-access-key
SUPABASE_URL=your-server-url

# ------------------------------
# Vector Database Configuration
# ------------------------------
VECTOR_STORE=weaviate

WEAVIATE_ENDPOINT=http://weaviate:8080
WEAVIATE_API_KEY=WVF5YThaHlkYwhGUSmCRgsX3tD5ngdN8pkih

QDRANT_URL=http://qdrant:6333
QDRANT_API_KEY=difyai123456
QDRANT_CLIENT_TIMEOUT=20
QDRANT_GRPC_ENABLED=false
QDRANT_GRPC_PORT=6334
QDRANT_REPLICATION_FACTOR=1

MILVUS_URI=http://host.docker.internal:19530
MILVUS_DATABASE=
MILVUS_TOKEN=
MILVUS_USER=
MILVUS_PASSWORD=
MILVUS_ENABLE_HYBRID_SEARCH=False
MILVUS_ANALYZER_PARAMS=

MYSCALE_HOST=myscale
MYSCALE_PORT=8123
MYSCALE_USER=default
MYSCALE_PASSWORD=
MYSCALE_DATABASE=dify
MYSCALE_FTS_PARAMS=

COUCHBASE_CONNECTION_STRING=couchbase://couchbase-server
COUCHBASE_USER=Administrator
COUCHBASE_PASSWORD=password
COUCHBASE_BUCKET_NAME=Embeddings
COUCHBASE_SCOPE_NAME=_default

PGVECTOR_HOST=pgvector
PGVECTOR_PORT=5432
PGVECTOR_USER=postgres
PGVECTOR_PASSWORD=difyai123456
PGVECTOR_DATABASE=dify
PGVECTOR_MIN_CONNECTION=1
PGVECTOR_MAX_CONNECTION=5
PGVECTOR_PG_BIGM=false
PGVECTOR_PG_BIGM_VERSION=1.2-20240606

VASTBASE_HOST=vastbase
VASTBASE_PORT=5432
VASTBASE_USER=dify
VASTBASE_PASSWORD=Difyai123456
VASTBASE_DATABASE=dify
VASTBASE_MIN_CONNECTION=1
VASTBASE_MAX_CONNECTION=5

PGVECTO_RS_HOST=pgvecto-rs
PGVECTO_RS_PORT=5432
PGVECTO_RS_USER=postgres
PGVECTO_RS_PASSWORD=difyai123456
PGVECTO_RS_DATABASE=dify

ANALYTICDB_KEY_ID=your-ak
ANALYTICDB_KEY_SECRET=your-sk
ANALYTICDB_REGION_ID=cn-hangzhou
ANALYTICDB_INSTANCE_ID=gp-ab123456
ANALYTICDB_ACCOUNT=testaccount
ANALYTICDB_PASSWORD=testpassword
ANALYTICDB_NAMESPACE=dify
ANALYTICDB_NAMESPACE_PASSWORD=difypassword
ANALYTICDB_HOST=gp-test.aliyuncs.com
ANALYTICDB_PORT=5432
ANALYTICDB_MIN_CONNECTION=1
ANALYTICDB_MAX_CONNECTION=5

TIDB_VECTOR_HOST=tidb
TIDB_VECTOR_PORT=4000
TIDB_VECTOR_USER=
TIDB_VECTOR_PASSWORD=
TIDB_VECTOR_DATABASE=dify

TIDB_ON_QDRANT_URL=http://127.0.0.1
TIDB_ON_QDRANT_API_KEY=dify
TIDB_ON_QDRANT_CLIENT_TIMEOUT=20
TIDB_ON_QDRANT_GRPC_ENABLED=false
TIDB_ON_QDRANT_GRPC_PORT=6334
TIDB_PUBLIC_KEY=dify
TIDB_PRIVATE_KEY=dify
TIDB_API_URL=http://127.0.0.1
TIDB_IAM_API_URL=http://127.0.0.1
TIDB_REGION=regions/aws-us-east-1
TIDB_PROJECT_ID=dify
TIDB_SPEND_LIMIT=100

CHROMA_HOST=127.0.0.1
CHROMA_PORT=8000
CHROMA_TENANT=default_tenant
CHROMA_DATABASE=default_database
CHROMA_AUTH_PROVIDER=chromadb.auth.token_authn.TokenAuthClientProvider
CHROMA_AUTH_CREDENTIALS=

ORACLE_USER=dify
ORACLE_PASSWORD=dify
ORACLE_DSN=oracle:1521/FREEPDB1
ORACLE_CONFIG_DIR=/app/api/storage/wallet
ORACLE_WALLET_LOCATION=/app/api/storage/wallet
ORACLE_WALLET_PASSWORD=dify
ORACLE_IS_AUTONOMOUS=false

RELYT_HOST=db
RELYT_PORT=5432
RELYT_USER=postgres
RELYT_PASSWORD=difyai123456
RELYT_DATABASE=postgres

OPENSEARCH_HOST=opensearch
OPENSEARCH_PORT=9200
OPENSEARCH_SECURE=true
OPENSEARCH_VERIFY_CERTS=true
OPENSEARCH_AUTH_METHOD=basic
OPENSEARCH_USER=admin
OPENSEARCH_PASSWORD=admin
OPENSEARCH_AWS_REGION=ap-southeast-1
OPENSEARCH_AWS_SERVICE=aoss

TENCENT_VECTOR_DB_URL=http://127.0.0.1
TENCENT_VECTOR_DB_API_KEY=dify
TENCENT_VECTOR_DB_TIMEOUT=30
TENCENT_VECTOR_DB_USERNAME=dify
TENCENT_VECTOR_DB_DATABASE=dify
TENCENT_VECTOR_DB_SHARD=1
TENCENT_VECTOR_DB_REPLICAS=2
TENCENT_VECTOR_DB_ENABLE_HYBRID_SEARCH=false

ELASTICSEARCH_HOST=0.0.0.0
ELASTICSEARCH_PORT=9200
ELASTICSEARCH_USERNAME=elastic
ELASTICSEARCH_PASSWORD=elastic
KIBANA_PORT=5601

BAIDU_VECTOR_DB_ENDPOINT=http://127.0.0.1:5287
BAIDU_VECTOR_DB_CONNECTION_TIMEOUT_MS=30000
BAIDU_VECTOR_DB_ACCOUNT=root
BAIDU_VECTOR_DB_API_KEY=dify
BAIDU_VECTOR_DB_DATABASE=dify
BAIDU_VECTOR_DB_SHARD=1
BAIDU_VECTOR_DB_REPLICAS=3

VIKINGDB_ACCESS_KEY=your-ak
VIKINGDB_SECRET_KEY=your-sk
VIKINGDB_REGION=cn-shanghai
VIKINGDB_HOST=api-vikingdb.xxx.volces.com
VIKINGDB_SCHEMA=http
VIKINGDB_CONNECTION_TIMEOUT=30
VIKINGDB_SOCKET_TIMEOUT=30

LINDORM_URL=http://lindorm:30070
LINDORM_USERNAME=lindorm
LINDORM_PASSWORD=lindorm
LINDORM_QUERY_TIMEOUT=1

OCEANBASE_VECTOR_HOST=oceanbase
OCEANBASE_VECTOR_PORT=2881
OCEANBASE_VECTOR_USER=root@test
OCEANBASE_VECTOR_PASSWORD=difyai123456
OCEANBASE_VECTOR_DATABASE=test
OCEANBASE_CLUSTER_NAME=difyai
OCEANBASE_MEMORY_LIMIT=6G
OCEANBASE_ENABLE_HYBRID_SEARCH=false

OPENGAUSS_HOST=opengauss
OPENGAUSS_PORT=6600
OPENGAUSS_USER=postgres
OPENGAUSS_PASSWORD=Dify@123
OPENGAUSS_DATABASE=dify
OPENGAUSS_MIN_CONNECTION=1
OPENGAUSS_MAX_CONNECTION=5
OPENGAUSS_ENABLE_PQ=false

HUAWEI_CLOUD_HOSTS=https://127.0.0.1:9200
HUAWEI_CLOUD_USER=admin
HUAWEI_CLOUD_PASSWORD=admin

UPSTASH_VECTOR_URL=https://xxx-vector.upstash.io
UPSTASH_VECTOR_TOKEN=dify

TABLESTORE_ENDPOINT=https://instance-name.cn-hangzhou.ots.aliyuncs.com
TABLESTORE_INSTANCE_NAME=instance-name
TABLESTORE_ACCESS_KEY_ID=xxx
TABLESTORE_ACCESS_KEY_SECRET=xxx

# ------------------------------
# Knowledge Configuration
# ------------------------------
UPLOAD_FILE_SIZE_LIMIT=15
UPLOAD_FILE_BATCH_LIMIT=5
ETL_TYPE=dify
UNSTRUCTURED_API_URL=
UNSTRUCTURED_API_KEY=
SCARF_NO_ANALYTICS=true

# ------------------------------
# Model Configuration
# ------------------------------
PROMPT_GENERATION_MAX_TOKENS=512
CODE_GENERATION_MAX_TOKENS=1024
PLUGIN_BASED_TOKEN_COUNTING_ENABLED=false

# ------------------------------
# Multi-modal Configuration
# ------------------------------
MULTIMODAL_SEND_FORMAT=base64
UPLOAD_IMAGE_FILE_SIZE_LIMIT=10
UPLOAD_VIDEO_FILE_SIZE_LIMIT=100
UPLOAD_AUDIO_FILE_SIZE_LIMIT=50

# ------------------------------
# Sentry Configuration
# Used for application monitoring and error log tracking.
# ------------------------------
SENTRY_DSN=
API_SENTRY_DSN=
API_SENTRY_TRACES_SAMPLE_RATE=1.0
API_SENTRY_PROFILES_SAMPLE_RATE=1.0
WEB_SENTRY_DSN=

# ------------------------------
# Notion Integration Configuration
# Variables can be obtained by applying for Notion integration: https://www.notion.so/my-integrations
# ------------------------------
NOTION_INTEGRATION_TYPE=public
NOTION_CLIENT_SECRET=
NOTION_CLIENT_ID=
NOTION_INTERNAL_SECRET=

# ------------------------------
# Mail related configuration
# ------------------------------
MAIL_TYPE=
MAIL_DEFAULT_SEND_FROM=

RESEND_API_URL=https://api.resend.com
RESEND_API_KEY=your-resend-api-key

SMTP_SERVER=
SMTP_PORT=465
SMTP_USERNAME=
SMTP_PASSWORD=
SMTP_USE_TLS=true
SMTP_OPPORTUNISTIC_TLS=false

# ------------------------------
# Others Configuration
# ------------------------------
INDEXING_MAX_SEGMENTATION_TOKENS_LENGTH=4000
INVITE_EXPIRY_HOURS=72
RESET_PASSWORD_TOKEN_EXPIRY_MINUTES=5

CODE_EXECUTION_ENDPOINT=http://sandbox:8194
CODE_EXECUTION_API_KEY=dify-sandbox
CODE_MAX_NUMBER=9223372036854775807
CODE_MIN_NUMBER=-9223372036854775808
CODE_MAX_DEPTH=5
CODE_MAX_PRECISION=20
CODE_MAX_STRING_LENGTH=80000
CODE_MAX_STRING_ARRAY_LENGTH=30
CODE_MAX_OBJECT_ARRAY_LENGTH=30
CODE_MAX_NUMBER_ARRAY_LENGTH=1000
CODE_EXECUTION_CONNECT_TIMEOUT=10
CODE_EXECUTION_READ_TIMEOUT=60
CODE_EXECUTION_WRITE_TIMEOUT=10
TEMPLATE_TRANSFORM_MAX_LENGTH=80000

WORKFLOW_MAX_EXECUTION_STEPS=500
WORKFLOW_MAX_EXECUTION_TIME=1200
WORKFLOW_CALL_MAX_DEPTH=5
MAX_VARIABLE_SIZE=204800
WORKFLOW_PARALLEL_DEPTH_LIMIT=3
WORKFLOW_FILE_UPLOAD_LIMIT=10
WORKFLOW_NODE_EXECUTION_STORAGE=rdbms

HTTP_REQUEST_NODE_MAX_BINARY_SIZE=10485760
HTTP_REQUEST_NODE_MAX_TEXT_SIZE=1048576
HTTP_REQUEST_NODE_SSL_VERIFY=True

SSRF_PROXY_HTTP_URL=http://ssrf_proxy:3128
SSRF_PROXY_HTTPS_URL=http://ssrf_proxy:3128

LOOP_NODE_MAX_COUNT=100
MAX_TOOLS_NUM=10
MAX_PARALLEL_LIMIT=10
MAX_ITERATIONS_NUM=99

# ------------------------------
# Environment Variables for web Service
# ------------------------------
TEXT_GENERATION_TIMEOUT_MS=60000

# ------------------------------
# Environment Variables for db Service
# ------------------------------

PGUSER=${DB_USERNAME}
# The password for the default postgres user.
POSTGRES_PASSWORD=${DB_PASSWORD}
# The name of the default postgres database.
POSTGRES_DB=${DB_DATABASE}
# postgres data directory
PGDATA=/var/lib/postgresql/data/pgdata

# ------------------------------
# Environment Variables for sandbox Service
# ------------------------------
SANDBOX_API_KEY=dify-sandbox
SANDBOX_GIN_MODE=release
SANDBOX_WORKER_TIMEOUT=15
SANDBOX_ENABLE_NETWORK=true
SANDBOX_HTTP_PROXY=http://ssrf_proxy:3128
SANDBOX_HTTPS_PROXY=http://ssrf_proxy:3128
SANDBOX_PORT=8194

# ------------------------------
# Environment Variables for weaviate Service
# (only used when VECTOR_STORE is weaviate)
# ------------------------------
WEAVIATE_PERSISTENCE_DATA_PATH=/var/lib/weaviate
WEAVIATE_QUERY_DEFAULTS_LIMIT=25
WEAVIATE_AUTHENTICATION_ANONYMOUS_ACCESS_ENABLED=true
WEAVIATE_DEFAULT_VECTORIZER_MODULE=none
WEAVIATE_CLUSTER_HOSTNAME=node1
WEAVIATE_AUTHENTICATION_APIKEY_ENABLED=true
WEAVIATE_AUTHENTICATION_APIKEY_ALLOWED_KEYS=WVF5YThaHlkYwhGUSmCRgsX3tD5ngdN8pkih
WEAVIATE_AUTHENTICATION_APIKEY_USERS=hello@dify.ai
WEAVIATE_AUTHORIZATION_ADMINLIST_ENABLED=true
WEAVIATE_AUTHORIZATION_ADMINLIST_USERS=hello@dify.ai

# ------------------------------
# Environment Variables for Chroma
# (only used when VECTOR_STORE is chroma)
# ------------------------------
CHROMA_SERVER_AUTHN_CREDENTIALS=difyai123456
CHROMA_SERVER_AUTHN_PROVIDER=chromadb.auth.token_authn.TokenAuthenticationServerProvider
CHROMA_IS_PERSISTENT=TRUE

# ------------------------------
# Environment Variables for Oracle Service
# (only used when VECTOR_STORE is oracle)
# ------------------------------
ORACLE_PWD=Dify123456
ORACLE_CHARACTERSET=AL32UTF8

# ------------------------------
# Environment Variables for milvus Service
# (only used when VECTOR_STORE is milvus)
# ------------------------------
ETCD_AUTO_COMPACTION_MODE=revision
ETCD_AUTO_COMPACTION_RETENTION=1000
ETCD_QUOTA_BACKEND_BYTES=4294967296
ETCD_SNAPSHOT_COUNT=50000
MINIO_ACCESS_KEY=minioadmin
MINIO_SECRET_KEY=minioadmin
ETCD_ENDPOINTS=etcd:2379
MINIO_ADDRESS=minio:9000
MILVUS_AUTHORIZATION_ENABLED=true

# ------------------------------
# Environment Variables for pgvector / pgvector-rs Service
# (only used when VECTOR_STORE is pgvector / pgvector-rs)
# ------------------------------
PGVECTOR_PGUSER=postgres
PGVECTOR_POSTGRES_PASSWORD=difyai123456
PGVECTOR_POSTGRES_DB=dify
PGVECTOR_PGDATA=/var/lib/postgresql/data/pgdata

# ------------------------------
# Environment Variables for opensearch
# (only used when VECTOR_STORE is opensearch)
# ------------------------------
OPENSEARCH_DISCOVERY_TYPE=single-node
OPENSEARCH_BOOTSTRAP_MEMORY_LOCK=true
OPENSEARCH_JAVA_OPTS_MIN=512m
OPENSEARCH_JAVA_OPTS_MAX=1024m
OPENSEARCH_INITIAL_ADMIN_PASSWORD=Qazwsxedc!@#123
OPENSEARCH_MEMLOCK_SOFT=-1
OPENSEARCH_MEMLOCK_HARD=-1
OPENSEARCH_NOFILE_SOFT=65536
OPENSEARCH_NOFILE_HARD=65536

# ------------------------------
# Environment Variables for Nginx reverse proxy
# ------------------------------
NGINX_SERVER_NAME=_
NGINX_HTTPS_ENABLED=false
NGINX_PORT=80
NGINX_SSL_PORT=443
NGINX_SSL_CERT_FILENAME=dify.crt
NGINX_SSL_CERT_KEY_FILENAME=dify.key
NGINX_SSL_PROTOCOLS=TLSv1.1 TLSv1.2 TLSv1.3

NGINX_WORKER_PROCESSES=auto
NGINX_CLIENT_MAX_BODY_SIZE=15M
NGINX_KEEPALIVE_TIMEOUT=65

NGINX_PROXY_READ_TIMEOUT=3600s
NGINX_PROXY_SEND_TIMEOUT=3600s

NGINX_ENABLE_CERTBOT_CHALLENGE=false

# ------------------------------
# Certbot Configuration
# ------------------------------
CERTBOT_EMAIL=your_email@example.com
CERTBOT_DOMAIN=your_domain.com
CERTBOT_OPTIONS=

# ------------------------------
# Environment Variables for SSRF Proxy
# ------------------------------
SSRF_HTTP_PORT=3128
SSRF_COREDUMP_DIR=/var/spool/squid
SSRF_REVERSE_PROXY_PORT=8194
SSRF_SANDBOX_HOST=sandbox
SSRF_DEFAULT_TIME_OUT=5
SSRF_DEFAULT_CONNECT_TIME_OUT=5
SSRF_DEFAULT_READ_TIME_OUT=5
SSRF_DEFAULT_WRITE_TIME_OUT=5

# ------------------------------
# docker env var for specifying vector db type at startup
# (based on the vector db type, the corresponding docker
# compose profile will be used)
# if you want to use unstructured, add ',unstructured' to the end
# ------------------------------
COMPOSE_PROFILES=${VECTOR_STORE:-weaviate}

# ------------------------------
# Docker Compose Service Expose Host Port Configurations
# ------------------------------
EXPOSE_NGINX_PORT=80
EXPOSE_NGINX_SSL_PORT=443

# ----------------------------------------------------------------------------
# ModelProvider & Tool Position Configuration
# Used to specify the model providers and tools that can be used in the app.
# ----------------------------------------------------------------------------
POSITION_TOOL_PINS=
POSITION_TOOL_INCLUDES=
POSITION_TOOL_EXCLUDES=
POSITION_PROVIDER_PINS=
POSITION_PROVIDER_INCLUDES=
POSITION_PROVIDER_EXCLUDES=

CSP_WHITELIST=
CREATE_TIDB_SERVICE_JOB_ENABLED=false
MAX_SUBMIT_COUNT=100
TOP_K_MAX_VALUE=10

# ------------------------------
# Plugin Daemon Configuration
# ------------------------------
DB_PLUGIN_DATABASE=dify_plugin
EXPOSE_PLUGIN_DAEMON_PORT=5002
PLUGIN_DAEMON_PORT=5002
PLUGIN_DAEMON_KEY=$PLUGIN_DAEMON_KEY
PLUGIN_DAEMON_URL=http://plugin_daemon:5002
PLUGIN_MAX_PACKAGE_SIZE=52428800
PLUGIN_PPROF_ENABLED=false

PLUGIN_DEBUGGING_HOST=0.0.0.0
PLUGIN_DEBUGGING_PORT=5003
EXPOSE_PLUGIN_DEBUGGING_HOST=localhost
EXPOSE_PLUGIN_DEBUGGING_PORT=5003

PLUGIN_DIFY_INNER_API_KEY=$PLUGIN_DIFY_INNER_API_KEY
PLUGIN_DIFY_INNER_API_URL=http://api:5001

ENDPOINT_URL_TEMPLATE=http://localhost/e/{hook_id}

MARKETPLACE_ENABLED=true
MARKETPLACE_API_URL=https://marketplace.dify.ai

FORCE_VERIFYING_SIGNATURE=true

PLUGIN_PYTHON_ENV_INIT_TIMEOUT=120
PLUGIN_MAX_EXECUTION_TIMEOUT=600
PIP_MIRROR_URL=

PLUGIN_STORAGE_TYPE=local
PLUGIN_STORAGE_LOCAL_ROOT=/app/storage
PLUGIN_WORKING_PATH=/app/storage/cwd
PLUGIN_INSTALLED_PATH=plugin
PLUGIN_PACKAGE_CACHE_PATH=plugin_packages
PLUGIN_MEDIA_CACHE_PATH=assets
PLUGIN_STORAGE_OSS_BUCKET=
PLUGIN_S3_USE_AWS=
PLUGIN_S3_USE_AWS_MANAGED_IAM=false
PLUGIN_S3_ENDPOINT=
PLUGIN_S3_USE_PATH_STYLE=false
PLUGIN_AWS_ACCESS_KEY=
PLUGIN_AWS_SECRET_KEY=
PLUGIN_AWS_REGION=
PLUGIN_AZURE_BLOB_STORAGE_CONTAINER_NAME=
PLUGIN_AZURE_BLOB_STORAGE_CONNECTION_STRING=
PLUGIN_TENCENT_COS_SECRET_KEY=
PLUGIN_TENCENT_COS_SECRET_ID=
PLUGIN_TENCENT_COS_REGION=
PLUGIN_ALIYUN_OSS_REGION=
PLUGIN_ALIYUN_OSS_ENDPOINT=
PLUGIN_ALIYUN_OSS_ACCESS_KEY_ID=
PLUGIN_ALIYUN_OSS_ACCESS_KEY_SECRET=
PLUGIN_ALIYUN_OSS_AUTH_VERSION=v4
PLUGIN_ALIYUN_OSS_PATH=
PLUGIN_VOLCENGINE_TOS_ENDPOINT=
PLUGIN_VOLCENGINE_TOS_ACCESS_KEY=
PLUGIN_VOLCENGINE_TOS_SECRET_KEY=
PLUGIN_VOLCENGINE_TOS_REGION=

# ------------------------------
# OTLP Collector Configuration
# ------------------------------
ENABLE_OTEL=false
OTLP_BASE_ENDPOINT=http://localhost:4318
OTLP_API_KEY=
OTEL_EXPORTER_OTLP_PROTOCOL=
OTEL_EXPORTER_TYPE=otlp
OTEL_SAMPLING_RATE=0.1
OTEL_BATCH_EXPORT_SCHEDULE_DELAY=5000
OTEL_MAX_QUEUE_SIZE=2048
OTEL_MAX_EXPORT_BATCH_SIZE=512
OTEL_METRIC_EXPORT_INTERVAL=60000
OTEL_BATCH_EXPORT_TIMEOUT=10000
OTEL_METRIC_EXPORT_TIMEOUT=30000

ALLOW_EMBED=false
QUEUE_MONITOR_THRESHOLD=200
QUEUE_MONITOR_ALERT_EMAILS=
QUEUE_MONITOR_INTERVAL=30
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
