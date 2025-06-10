resource "google_storage_bucket" "uploads" {
  name          = "${var.project_id}-${var.project_name}-uploads-${var.environment}"
  location      = var.region
  project       = var.project_id
  force_destroy = var.force_destroy

  uniform_bucket_level_access = true

  lifecycle_rule {
    condition {
      age = var.uploads_retention_days
    }
    action {
      type = "Delete"
    }
  }

  versioning {
    enabled = false
  }
}

resource "google_storage_bucket" "model_cache" {
  name          = "${var.project_id}-${var.project_name}-models-${var.environment}"
  location      = var.region
  project       = var.project_id
  force_destroy = var.force_destroy

  uniform_bucket_level_access = true

  lifecycle_rule {
    condition {
      age = var.model_cache_retention_days
    }
    action {
      type = "SetStorageClass"
      storage_class = "NEARLINE"
    }
  }

  lifecycle_rule {
    condition {
      age = var.model_cache_archive_days
    }
    action {
      type = "SetStorageClass"
      storage_class = "ARCHIVE"
    }
  }

  versioning {
    enabled = true
  }
}

resource "google_storage_bucket" "backups" {
  name          = "${var.project_id}-${var.project_name}-backups-${var.environment}"
  location      = var.region
  project       = var.project_id
  force_destroy = var.force_destroy

  uniform_bucket_level_access = true

  lifecycle_rule {
    condition {
      age = var.backup_retention_days
    }
    action {
      type = "Delete"
    }
  }

  versioning {
    enabled = true
  }
}

resource "google_storage_bucket_iam_member" "uploads_read" {
  bucket = google_storage_bucket.uploads.name
  role   = "roles/storage.objectViewer"
  member = "serviceAccount:${var.cloud_run_service_account}"
}

resource "google_storage_bucket_iam_member" "uploads_write" {
  bucket = google_storage_bucket.uploads.name
  role   = "roles/storage.objectCreator"
  member = "serviceAccount:${var.cloud_run_service_account}"
}

resource "google_storage_bucket_iam_member" "model_cache_read" {
  bucket = google_storage_bucket.model_cache.name
  role   = "roles/storage.objectViewer"
  member = "serviceAccount:${var.cloud_run_service_account}"
}

resource "google_storage_bucket_iam_member" "model_cache_write" {
  bucket = google_storage_bucket.model_cache.name
  role   = "roles/storage.objectCreator"
  member = "serviceAccount:${var.cloud_run_service_account}"
}

resource "google_storage_bucket_iam_member" "backups_write" {
  bucket = google_storage_bucket.backups.name
  role   = "roles/storage.objectCreator"
  member = "serviceAccount:${var.cloud_run_service_account}"
}