resource "google_service_account" "cloud_build" {
  account_id   = "${var.project_name}-build-sa-${var.environment}"
  display_name = "Cloud Build Service Account for ${var.environment}"
  project      = var.project_id
}

resource "google_project_iam_member" "cloud_build_permissions" {
  for_each = toset([
    "roles/cloudbuild.builds.builder",
    "roles/artifactregistry.writer",
    "roles/storage.objectAdmin",
    "roles/logging.logWriter"
  ])
  project = var.project_id
  role    = each.value
  member  = "serviceAccount:${google_service_account.cloud_build.email}"
}

# Enable Cloud Build API
resource "google_project_service" "cloud_build" {
  project            = var.project_id
  service            = "cloudbuild.googleapis.com"
  disable_on_destroy = false
}

# Enable Container Registry API (required for Artifact Registry)
resource "google_project_service" "container_registry" {
  project            = var.project_id
  service            = "containerregistry.googleapis.com"
  disable_on_destroy = false
}
