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
    PROJECT_ID                          = var.project_id
    REGION                              = var.region
    DB_HOST                             = var.database_host
    DB_NAME                             = var.database_name
    DB_USER                             = var.database_user
    DB_PASSWORD                         = var.database_password
    SECRET_KEY                          = var.dify_secret_key
    GOOGLE_STORAGE_BUCKET               = var.uploads_bucket_name
    GOOGLE_STORAGE_SERVICE_ACCOUNT_JSON = base64encode(var.gcs_service_account_json)
    PLUGIN_DAEMON_KEY                   = var.plugin_daemon_key
    PLUGIN_DIFY_INNER_API_KEY           = var.plugin_dify_inner_api_key
    PLUGIN_STORAGE_BUCKET               = var.plugin_storage_bucket_name
    GCS_CREDENTIALS                     = base64encode(var.gcs_service_account_json)
    DOCKER_COMPOSE_CONTENT              = file("${path.module}/files/docker-compose.yml")
    NGINX_CONFIG_CONTENT                = file("${path.module}/files/nginx/nginx.conf")
  })
}

# Service account for VM instance
resource "google_service_account" "dify_vm" {
  account_id   = "${var.project_name}-dify-vm-${var.environment}"
  display_name = "Dify VM Service Account for ${var.environment}"
  project      = var.project_id
}

# VM instance
resource "google_compute_instance" "dify" {
  name         = "${var.project_name}-dify-${var.environment}"
  machine_type = var.machine_type
  zone         = var.zone
  project      = var.project_id

  tags = [
    "dify-vm",
    var.environment,
    "allow-ssh",
    "allow-http",
    "allow-https"
  ]

  boot_disk {
    initialize_params {
      image = var.boot_disk_image
      size  = var.boot_disk_size
      type  = var.boot_disk_type
    }
  }

  network_interface {
    network    = var.network_id
    subnetwork = var.subnet_id

    # Assign external IP for initial setup
    access_config {
      // Ephemeral public IP
    }
  }

  service_account {
    email  = google_service_account.dify_vm.email
    scopes = ["cloud-platform"]
  }

  metadata = {
    ssh-keys = var.ssh_keys
    startup-script-url = "gs://${google_storage_bucket.vm_scripts.name}/setup-dify.sh"
  }

  # Startup script to install Docker and Docker Compose
  metadata_startup_script = <<-EOF
    #!/bin/bash
    set -e

    # Update system
    apt-get update
    apt-get upgrade -y

    # Install Docker
    curl -fsSL https://get.docker.com -o get-docker.sh
    sh get-docker.sh

    # Install Docker Compose
    curl -L "https://github.com/docker/compose/releases/download/v2.29.1/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
    chmod +x /usr/local/bin/docker-compose

    # Add user to docker group
    usermod -aG docker ${var.vm_user}

    # Create dify directory
    mkdir -p /opt/dify
    chown ${var.vm_user}:${var.vm_user} /opt/dify

    # Log completion
    echo "VM setup completed" > /var/log/startup-script.log
  EOF

  labels = {
    environment  = var.environment
    project      = var.project_name
    service      = "dify"
    managed_by   = "terraform"
  }

  allow_stopping_for_update = true
}

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

resource "google_storage_bucket_iam_member" "vm_plugin_storage_admin" {
  bucket = var.plugin_storage_bucket_name
  role   = "roles/storage.admin"
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