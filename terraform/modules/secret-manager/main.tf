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