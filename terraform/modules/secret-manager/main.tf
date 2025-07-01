# Secret Manager resources for Dify secrets

# Database password secret
resource "google_secret_manager_secret" "database_password" {
  secret_id = "${var.project_name}-db-password-${var.environment}"
  project   = var.project_id

  replication {
    auto {}
  }

  labels = {
    environment = var.environment
    project     = var.project_name
    managed_by  = "terraform"
  }
}

resource "google_secret_manager_secret_version" "database_password" {
  secret      = google_secret_manager_secret.database_password.id
  secret_data = var.database_password
}

# Dify secret key
resource "google_secret_manager_secret" "dify_secret_key" {
  secret_id = "${var.project_name}-dify-secret-${var.environment}"
  project   = var.project_id

  replication {
    auto {}
  }

  labels = {
    environment = var.environment
    project     = var.project_name
    managed_by  = "terraform"
  }
}

resource "google_secret_manager_secret_version" "dify_secret_key" {
  secret      = google_secret_manager_secret.dify_secret_key.id
  secret_data = var.dify_secret_key
}

# GCS service account JSON
resource "google_secret_manager_secret" "gcs_service_account" {
  secret_id = "${var.project_name}-gcs-sa-${var.environment}"
  project   = var.project_id

  replication {
    auto {}
  }

  labels = {
    environment = var.environment
    project     = var.project_name
    managed_by  = "terraform"
  }
}

resource "google_secret_manager_secret_version" "gcs_service_account" {
  secret      = google_secret_manager_secret.gcs_service_account.id
  secret_data = var.gcs_service_account_json
}

# Plugin daemon key
resource "google_secret_manager_secret" "plugin_daemon_key" {
  secret_id = "${var.project_name}-plugin-daemon-${var.environment}"
  project   = var.project_id

  replication {
    auto {}
  }

  labels = {
    environment = var.environment
    project     = var.project_name
    managed_by  = "terraform"
  }
}

resource "google_secret_manager_secret_version" "plugin_daemon_key" {
  secret      = google_secret_manager_secret.plugin_daemon_key.id
  secret_data = var.plugin_daemon_key
}

# Plugin inner API key
resource "google_secret_manager_secret" "plugin_inner_api_key" {
  secret_id = "${var.project_name}-plugin-api-${var.environment}"
  project   = var.project_id

  replication {
    auto {}
  }

  labels = {
    environment = var.environment
    project     = var.project_name
    managed_by  = "terraform"
  }
}

resource "google_secret_manager_secret_version" "plugin_inner_api_key" {
  secret      = google_secret_manager_secret.plugin_inner_api_key.id
  secret_data = var.plugin_inner_api_key
}

# Server key
resource "google_secret_manager_secret" "server_key" {
  secret_id = "${var.project_name}-server-key-${var.environment}"
  project   = var.project_id

  replication {
    auto {}
  }

  labels = {
    environment = var.environment
    project     = var.project_name
    managed_by  = "terraform"
  }
}

resource "google_secret_manager_secret_version" "server_key" {
  secret      = google_secret_manager_secret.server_key.id
  secret_data = var.server_key
}

# Dify inner API key
resource "google_secret_manager_secret" "dify_inner_api_key" {
  secret_id = "${var.project_name}-dify-inner-api-key-${var.environment}"
  project   = var.project_id

  replication {
    auto {}
  }

  labels = {
    environment = var.environment
    project     = var.project_name
    managed_by  = "terraform"
  }
}

resource "google_secret_manager_secret_version" "dify_inner_api_key" {
  secret      = google_secret_manager_secret.dify_inner_api_key.id
  secret_data = var.dify_inner_api_key
}

# Grant VM service account access to secrets
resource "google_secret_manager_secret_iam_member" "vm_access_db_password" {
  project   = var.project_id
  secret_id = google_secret_manager_secret.database_password.secret_id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${var.vm_service_account_email}"
}

resource "google_secret_manager_secret_iam_member" "vm_access_dify_secret" {
  project   = var.project_id
  secret_id = google_secret_manager_secret.dify_secret_key.secret_id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${var.vm_service_account_email}"
}

resource "google_secret_manager_secret_iam_member" "vm_access_gcs_sa" {
  project   = var.project_id
  secret_id = google_secret_manager_secret.gcs_service_account.secret_id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${var.vm_service_account_email}"
}

resource "google_secret_manager_secret_iam_member" "vm_access_plugin_daemon" {
  project   = var.project_id
  secret_id = google_secret_manager_secret.plugin_daemon_key.secret_id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${var.vm_service_account_email}"
}

resource "google_secret_manager_secret_iam_member" "vm_access_plugin_api" {
  project   = var.project_id
  secret_id = google_secret_manager_secret.plugin_inner_api_key.secret_id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${var.vm_service_account_email}"
}

resource "google_secret_manager_secret_iam_member" "vm_access_server_key" {
  project   = var.project_id
  secret_id = google_secret_manager_secret.server_key.secret_id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${var.vm_service_account_email}"
}

resource "google_secret_manager_secret_iam_member" "vm_access_dify_inner_api_key" {
  project   = var.project_id
  secret_id = google_secret_manager_secret.dify_inner_api_key.secret_id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${var.vm_service_account_email}"
}

# Init password secret
resource "google_secret_manager_secret" "init_password" {
  secret_id = "${var.project_name}-init-password-${var.environment}"
  project   = var.project_id

  replication {
    auto {}
  }

  labels = {
    environment = var.environment
    project     = var.project_name
    managed_by  = "terraform"
  }
}

resource "google_secret_manager_secret_version" "init_password" {
  secret      = google_secret_manager_secret.init_password.id
  secret_data = var.init_password
}

# Redis password secret
resource "google_secret_manager_secret" "redis_password" {
  secret_id = "${var.project_name}-redis-password-${var.environment}"
  project   = var.project_id

  replication {
    auto {}
  }

  labels = {
    environment = var.environment
    project     = var.project_name
    managed_by  = "terraform"
  }
}

resource "google_secret_manager_secret_version" "redis_password" {
  secret      = google_secret_manager_secret.redis_password.id
  secret_data = var.redis_password
}

# Code execution API key secret
resource "google_secret_manager_secret" "code_execution_api_key" {
  secret_id = "${var.project_name}-code-execution-api-key-${var.environment}"
  project   = var.project_id

  replication {
    auto {}
  }

  labels = {
    environment = var.environment
    project     = var.project_name
    managed_by  = "terraform"
  }
}

resource "google_secret_manager_secret_version" "code_execution_api_key" {
  secret      = google_secret_manager_secret.code_execution_api_key.id
  secret_data = var.code_execution_api_key
}

# Grant VM service account access to new secrets
resource "google_secret_manager_secret_iam_member" "vm_access_init_password" {
  project   = var.project_id
  secret_id = google_secret_manager_secret.init_password.secret_id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${var.vm_service_account_email}"
}

resource "google_secret_manager_secret_iam_member" "vm_access_redis_password" {
  project   = var.project_id
  secret_id = google_secret_manager_secret.redis_password.secret_id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${var.vm_service_account_email}"
}

resource "google_secret_manager_secret_iam_member" "vm_access_code_execution_api_key" {
  project   = var.project_id
  secret_id = google_secret_manager_secret.code_execution_api_key.secret_id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${var.vm_service_account_email}"
}

# AWS S3 access key for plugin storage
resource "google_secret_manager_secret" "plugin_s3_access_key" {
  secret_id = "${var.project_name}-plugin-s3-access-key-${var.environment}"
  project   = var.project_id

  replication {
    auto {}
  }

  labels = {
    environment = var.environment
    project     = var.project_name
    managed_by  = "terraform"
  }
}

resource "google_secret_manager_secret_version" "plugin_s3_access_key" {
  secret      = google_secret_manager_secret.plugin_s3_access_key.id
  secret_data = var.plugin_s3_access_key
}

# AWS S3 secret key for plugin storage
resource "google_secret_manager_secret" "plugin_s3_secret_key" {
  secret_id = "${var.project_name}-plugin-s3-secret-key-${var.environment}"
  project   = var.project_id

  replication {
    auto {}
  }

  labels = {
    environment = var.environment
    project     = var.project_name
    managed_by  = "terraform"
  }
}

resource "google_secret_manager_secret_version" "plugin_s3_secret_key" {
  secret      = google_secret_manager_secret.plugin_s3_secret_key.id
  secret_data = var.plugin_s3_secret_key
}

# Grant VM service account access to AWS S3 secrets
resource "google_secret_manager_secret_iam_member" "vm_access_plugin_s3_access_key" {
  project   = var.project_id
  secret_id = google_secret_manager_secret.plugin_s3_access_key.secret_id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${var.vm_service_account_email}"
}

resource "google_secret_manager_secret_iam_member" "vm_access_plugin_s3_secret_key" {
  project   = var.project_id
  secret_id = google_secret_manager_secret.plugin_s3_secret_key.secret_id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${var.vm_service_account_email}"
}

# Manifest service account JSON
resource "google_secret_manager_secret" "manifest_service_account" {
  secret_id = "${var.project_name}-manifest-sa-${var.environment}"
  project   = var.project_id

  replication {
    auto {}
  }

  labels = {
    environment = var.environment
    project     = var.project_name
    managed_by  = "terraform"
  }
}

resource "google_secret_manager_secret_version" "manifest_service_account" {
  secret      = google_secret_manager_secret.manifest_service_account.id
  secret_data = var.manifest_service_account_json
}

# Notion internal secret
resource "google_secret_manager_secret" "notion_internal_secret" {
  secret_id = "${var.project_name}-notion-internal-secret-${var.environment}"
  project   = var.project_id

  replication {
    auto {}
  }

  labels = {
    environment = var.environment
    project     = var.project_name
    managed_by  = "terraform"
  }
}

resource "google_secret_manager_secret_version" "notion_internal_secret" {
  secret      = google_secret_manager_secret.notion_internal_secret.id
  secret_data = var.notion_internal_secret
}

# Grant VM service account access to Notion internal secret
resource "google_secret_manager_secret_iam_member" "vm_access_notion_internal_secret" {
  project   = var.project_id
  secret_id = google_secret_manager_secret.notion_internal_secret.secret_id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${var.vm_service_account_email}"
}

