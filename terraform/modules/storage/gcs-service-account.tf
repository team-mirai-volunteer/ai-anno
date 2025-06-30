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

# Service Account for Manifest Image Uploads
resource "google_service_account" "manifest_images_uploader" {
  account_id   = "${var.project_name}-manifest-images-${var.environment}"
  display_name = "Manifest Images Uploader (${var.environment})"
  project      = var.project_id
  description  = "Service account for uploading manifest images from Google Colab"
}

# Create service account key for manifest images uploader
resource "google_service_account_key" "manifest_images_uploader" {
  service_account_id = google_service_account.manifest_images_uploader.name
  key_algorithm      = "KEY_ALG_RSA_2048"
}

# Grant storage permissions for manifest images bucket only
resource "google_storage_bucket_iam_member" "manifest_images_uploader_admin" {
  bucket = google_storage_bucket.manifest_images.name
  role   = "roles/storage.objectAdmin"
  member = "serviceAccount:${google_service_account.manifest_images_uploader.email}"
}

# Grant storage viewer permission to list objects
resource "google_storage_bucket_iam_member" "manifest_images_uploader_viewer" {
  bucket = google_storage_bucket.manifest_images.name
  role   = "roles/storage.objectViewer"
  member = "serviceAccount:${google_service_account.manifest_images_uploader.email}"
}
