# HMAC keys for S3 compatibility

# Service account for HMAC keys
resource "google_service_account" "gcs_hmac" {
  account_id   = "${var.project_name}-gcs-hmac-${var.environment}"
  display_name = "GCS HMAC Service Account for ${var.environment}"
  description  = "Service account for GCS S3 compatibility HMAC keys"
  project      = var.project_id
}

# Grant storage admin role to the service account
resource "google_storage_bucket_iam_member" "hmac_uploads_admin" {
  bucket = google_storage_bucket.uploads.name
  role   = "roles/storage.admin"
  member = "serviceAccount:${google_service_account.gcs_hmac.email}"
}

resource "google_storage_bucket_iam_member" "hmac_models_admin" {
  bucket = google_storage_bucket.model_cache.name
  role   = "roles/storage.admin"
  member = "serviceAccount:${google_service_account.gcs_hmac.email}"
}

resource "google_storage_bucket_iam_member" "hmac_plugins_admin" {
  bucket = google_storage_bucket.plugin_storage.name
  role   = "roles/storage.admin"
  member = "serviceAccount:${google_service_account.gcs_hmac.email}"
}

resource "google_storage_bucket_iam_member" "hmac_backups_admin" {
  bucket = google_storage_bucket.backups.name
  role   = "roles/storage.admin"
  member = "serviceAccount:${google_service_account.gcs_hmac.email}"
}

# HMAC key for S3 compatibility
resource "google_storage_hmac_key" "dify_s3_compat" {
  service_account_email = google_service_account.gcs_hmac.email
  project              = var.project_id
}

# Store HMAC keys in Secret Manager
resource "google_secret_manager_secret" "hmac_access_key" {
  secret_id = "${var.project_name}-hmac-access-key-${var.environment}"
  project   = var.project_id

  replication {
    auto {}
  }
}

resource "google_secret_manager_secret_version" "hmac_access_key" {
  secret      = google_secret_manager_secret.hmac_access_key.id
  secret_data = google_storage_hmac_key.dify_s3_compat.access_id
}

resource "google_secret_manager_secret" "hmac_secret_key" {
  secret_id = "${var.project_name}-hmac-secret-key-${var.environment}"
  project   = var.project_id

  replication {
    auto {}
  }
}

resource "google_secret_manager_secret_version" "hmac_secret_key" {
  secret      = google_secret_manager_secret.hmac_secret_key.id
  secret_data = google_storage_hmac_key.dify_s3_compat.secret
}

