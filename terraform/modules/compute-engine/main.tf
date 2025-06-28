# Compute Engine resources for Dify VM deployment

# Storage bucket for VM scripts
resource "google_storage_bucket" "vm_scripts" {
  name          = "${var.project_id}-vm-scripts-${var.environment}"
  location      = var.region
  project       = var.project_id
  force_destroy = true

  versioning {
    enabled = false
  }

  uniform_bucket_level_access = true

  labels = {
    environment = var.environment
    project     = var.project_name
    managed_by  = "terraform"
  }
}

# Upload setup script
resource "google_storage_bucket_object" "setup_script" {
  name   = "setup-dify.sh"
  bucket = google_storage_bucket.vm_scripts.name
  content = templatefile("${path.module}/files/setup-dify.sh", {
    PROJECT_ID            = var.project_id
    PROJECT_NAME          = var.project_name
    ENVIRONMENT           = var.environment
    REGION                = var.region
    DB_HOST               = var.database_host
    DB_NAME               = var.database_name
    DB_USER               = var.database_user
    GOOGLE_STORAGE_BUCKET = var.uploads_bucket_name
    PLUGIN_S3_BUCKET      = var.plugin_s3_bucket
    PLUGIN_AWS_REGION     = var.plugin_aws_region
  })
}

# Grant VM service account access to read startup script
resource "google_storage_bucket_iam_member" "vm_scripts_reader" {
  bucket = google_storage_bucket.vm_scripts.name
  role   = "roles/storage.objectViewer"
  member = "serviceAccount:${google_service_account.dify_vm.email}"
}

# Service account for VM instance
resource "google_service_account" "dify_vm" {
  account_id   = "${var.project_name}-dify-vm-${var.environment}"
  display_name = "Dify VM Service Account for ${var.environment}"
  project      = var.project_id
}

# Note: VM instance is now managed by MIG (see mig.tf)
# This resource block is commented out but kept for reference during migration
# resource "google_compute_instance" "dify" {
#   name         = "${var.project_name}-dify-${var.environment}"
#   machine_type = var.machine_type
#   zone         = var.zone
#   project      = var.project_id
#
#   tags = [
#     "dify-vm",
#     var.environment,
#     "allow-ssh",
#     "allow-http",
#     "allow-https",
#     "allow-health-check"
#   ]
#
#   boot_disk {
#     initialize_params {
#       image = var.boot_disk_image
#       size  = var.boot_disk_size
#       type  = var.boot_disk_type
#     }
#   }
#
#   network_interface {
#     network    = var.network_id
#     subnetwork = var.subnet_id
#
#     # Assign external IP for initial setup
#     access_config {
#       // Ephemeral public IP
#     }
#   }
#
#   service_account {
#     email  = google_service_account.dify_vm.email
#     scopes = ["cloud-platform"]
#   }
#
#   metadata = {
#     ssh-keys = var.ssh_keys
#     startup-script-url = "gs://${google_storage_bucket.vm_scripts.name}/setup-dify.sh"
#   }
#
#   labels = {
#     environment  = var.environment
#     project      = var.project_name
#     service      = "dify"
#     managed_by   = "terraform"
#   }
#
#   allow_stopping_for_update = true
# }

# Firewall rules for VM
resource "google_compute_firewall" "dify_allow_http" {
  name    = "${var.project_name}-dify-allow-http-${var.environment}"
  network = var.network_name
  project = var.project_id

  allow {
    protocol = "tcp"
    ports    = ["80"]
  }

  source_ranges = ["0.0.0.0/0"]
  target_tags   = ["allow-http"]
}

resource "google_compute_firewall" "dify_allow_https" {
  name    = "${var.project_name}-dify-allow-https-${var.environment}"
  network = var.network_name
  project = var.project_id

  allow {
    protocol = "tcp"
    ports    = ["443"]
  }

  source_ranges = ["0.0.0.0/0"]
  target_tags   = ["allow-https"]
}

resource "google_compute_firewall" "dify_allow_ssh" {
  count   = length(var.ssh_source_ranges) > 0 ? 1 : 0
  name    = "${var.project_name}-dify-allow-ssh-${var.environment}"
  network = var.network_name
  project = var.project_id

  allow {
    protocol = "tcp"
    ports    = ["22"]
  }

  # Restrict SSH access to specific IP ranges for security
  source_ranges = var.ssh_source_ranges
  target_tags   = ["allow-ssh"]
}

# IAM bindings for VM service account
resource "google_storage_bucket_iam_member" "vm_uploads_read" {
  bucket = var.uploads_bucket_name
  role   = "roles/storage.objectViewer"
  member = "serviceAccount:${google_service_account.dify_vm.email}"
}

resource "google_storage_bucket_iam_member" "vm_uploads_write" {
  bucket = var.uploads_bucket_name
  role   = "roles/storage.objectUser"
  member = "serviceAccount:${google_service_account.dify_vm.email}"
}

resource "google_storage_bucket_iam_member" "vm_model_cache_read" {
  bucket = var.model_cache_bucket_name
  role   = "roles/storage.objectViewer"
  member = "serviceAccount:${google_service_account.dify_vm.email}"
}

resource "google_storage_bucket_iam_member" "vm_model_cache_write" {
  bucket = var.model_cache_bucket_name
  role   = "roles/storage.objectUser"
  member = "serviceAccount:${google_service_account.dify_vm.email}"
}


# Cloud SQL client permissions
resource "google_project_iam_member" "vm_cloudsql_client" {
  project = var.project_id
  role    = "roles/cloudsql.client"
  member  = "serviceAccount:${google_service_account.dify_vm.email}"
}

# Artifact Registry reader permissions
resource "google_artifact_registry_repository_iam_member" "vm_reader" {
  project    = var.project_id
  location   = var.region
  repository = var.artifact_registry_name
  role       = "roles/artifactregistry.reader"
  member     = "serviceAccount:${google_service_account.dify_vm.email}"
}

