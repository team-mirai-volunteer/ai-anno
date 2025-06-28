# GCS Service Account for Dify application

resource "google_service_account" "dify_gcs" {
  account_id   = "${var.project_name}-gcs-${var.environment}"
  display_name = "Dify GCS Service Account (${var.environment})"
  project      = var.project_id
  description  = "Service account for Dify application to access Google Cloud Storage"
}

# Create service account key
resource "google_service_account_key" "dify_gcs" {
  service_account_id = google_service_account.dify_gcs.name
  key_algorithm      = "KEY_ALG_RSA_2048"
}

# Grant storage permissions
resource "google_project_iam_member" "dify_gcs_storage_admin" {
  project = var.project_id
  role    = "roles/storage.objectAdmin"
  member  = "serviceAccount:${google_service_account.dify_gcs.email}"
}

resource "google_project_iam_member" "dify_gcs_bucket_reader" {
  project = var.project_id
  role    = "roles/storage.objectViewer"
  member  = "serviceAccount:${google_service_account.dify_gcs.email}"
}

# Outputs are defined in outputs.tf
