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
      type          = "SetStorageClass"
      storage_class = "NEARLINE"
    }
  }

  lifecycle_rule {
    condition {
      age = var.model_cache_archive_days
    }
    action {
      type          = "SetStorageClass"
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

resource "google_storage_bucket" "plugin_storage" {
  name          = "${var.project_id}-${var.project_name}-plugins-${var.environment}"
  location      = var.region
  project       = var.project_id
  force_destroy = var.force_destroy

  uniform_bucket_level_access = true

  dynamic "lifecycle_rule" {
    for_each = var.plugin_retention_days > 0 ? [1] : []
    content {
      condition {
        age = var.plugin_retention_days
      }
      action {
        type = "Delete"
      }
    }
  }

  versioning {
    enabled = false
  }
}

resource "google_storage_bucket_iam_member" "plugin_storage_read" {
  bucket = google_storage_bucket.plugin_storage.name
  role   = "roles/storage.objectViewer"
  member = "serviceAccount:${var.cloud_run_service_account}"
}

resource "google_storage_bucket_iam_member" "plugin_storage_write" {
  bucket = google_storage_bucket.plugin_storage.name
  role   = "roles/storage.objectCreator"
  member = "serviceAccount:${var.cloud_run_service_account}"
}

resource "google_storage_bucket_iam_member" "plugin_storage_admin" {
  bucket = google_storage_bucket.plugin_storage.name
  role   = "roles/storage.objectAdmin"
  member = "serviceAccount:${var.cloud_run_service_account}"
}