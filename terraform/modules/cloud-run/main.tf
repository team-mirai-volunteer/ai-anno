resource "google_service_account" "cloud_run" {
  account_id   = "${var.project_name}-run-sa-${var.environment}"
  display_name = "Cloud Run Service Account for ${var.environment}"
  project      = var.project_id
}

resource "google_project_iam_member" "cloud_run_permissions" {
  for_each = toset([
    "roles/cloudsql.client",
    "roles/storage.objectViewer",
    "roles/storage.objectCreator",
    "roles/secretmanager.secretAccessor",
    "roles/logging.logWriter",
    "roles/monitoring.metricWriter"
  ])

  project = var.project_id
  role    = each.value
  member  = "serviceAccount:${google_service_account.cloud_run.email}"
}

locals {
  env_vars = merge(var.common_env_vars, {
    DB_HOST     = var.database_host
    DB_NAME     = var.database_name
    DB_USER     = var.database_user
    ENVIRONMENT = var.environment
  })
}

resource "google_cloud_run_v2_service" "main" {
  name                = "${var.project_name}-main-${var.environment}"
  location            = var.region
  project             = var.project_id
  deletion_protection = false

  template {
    service_account = google_service_account.cloud_run.email

    vpc_access {
      connector = var.vpc_connector_id
      egress    = "PRIVATE_RANGES_ONLY"
    }

    scaling {
      min_instance_count = var.main_min_instances
      max_instance_count = var.main_max_instances
    }

    containers {
      name  = "nginx"
      image = var.nginx_image

      ports {
        container_port = 80
      }

      resources {
        cpu_idle = true
        limits = {
          cpu    = "1"
          memory = "512Mi"
        }
      }
    }

    containers {
      name  = "api"
      image = var.api_image

      ports {
        container_port = 5001
      }

      dynamic "env" {
        for_each = local.env_vars
        content {
          name  = env.key
          value = env.value
        }
      }

      env {
        name = "DATABASE_PASSWORD"
        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.db_password.secret_id
            version = "latest"
          }
        }
      }

      resources {
        cpu_idle = true
        limits = {
          cpu    = var.main_cpu
          memory = var.main_memory
        }
      }

      volume_mounts {
        name       = "cloudsql"
        mount_path = "/cloudsql"
      }
    }

    containers {
      name  = "web"
      image = var.web_image

      ports {
        container_port = 3000
      }

      resources {
        cpu_idle = true
        limits = {
          cpu    = "1"
          memory = "1Gi"
        }
      }
    }

    containers {
      name  = "redis"
      image = "redis:7-alpine"

      ports {
        container_port = 6379
      }

      resources {
        cpu_idle = false
        limits = {
          cpu    = "1"
          memory = "1Gi"
        }
      }

      startup_probe {
        tcp_socket {
          port = 6379
        }
        initial_delay_seconds = 5
        timeout_seconds       = 1
        period_seconds        = 5
        failure_threshold     = 3
      }
    }

    volumes {
      name = "cloudsql"
      cloud_sql_instance {
        instances = [var.database_connection_name]
      }
    }
  }

  traffic {
    type    = "TRAFFIC_TARGET_ALLOCATION_TYPE_LATEST"
    percent = 100
  }
}

resource "google_cloud_run_v2_service" "worker" {
  name                = "${var.project_name}-worker-${var.environment}"
  location            = var.region
  project             = var.project_id
  deletion_protection = false

  template {
    service_account = google_service_account.cloud_run.email

    vpc_access {
      connector = var.vpc_connector_id
      egress    = "PRIVATE_RANGES_ONLY"
    }

    scaling {
      min_instance_count = var.worker_min_instances
      max_instance_count = var.worker_max_instances
    }

    containers {
      name  = "worker"
      image = var.worker_image

      dynamic "env" {
        for_each = local.env_vars
        content {
          name  = env.key
          value = env.value
        }
      }

      env {
        name = "DATABASE_PASSWORD"
        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.db_password.secret_id
            version = "latest"
          }
        }
      }

      resources {
        cpu_idle = true
        limits = {
          cpu    = var.worker_cpu
          memory = var.worker_memory
        }
      }

      volume_mounts {
        name       = "cloudsql"
        mount_path = "/cloudsql"
      }
    }

    volumes {
      name = "cloudsql"
      cloud_sql_instance {
        instances = [var.database_connection_name]
      }
    }
  }

  traffic {
    type    = "TRAFFIC_TARGET_ALLOCATION_TYPE_LATEST"
    percent = 100
  }
}

resource "google_cloud_run_v2_service" "sandbox" {
  name                = "${var.project_name}-sandbox-${var.environment}"
  location            = var.region
  project             = var.project_id
  deletion_protection = false

  template {
    service_account = google_service_account.cloud_run.email

    vpc_access {
      connector = var.vpc_connector_id
      egress    = "PRIVATE_RANGES_ONLY"
    }

    scaling {
      min_instance_count = var.sandbox_min_instances
      max_instance_count = var.sandbox_max_instances
    }

    containers {
      name  = "sandbox"
      image = var.sandbox_image

      resources {
        cpu_idle = true
        limits = {
          cpu    = var.sandbox_cpu
          memory = var.sandbox_memory
        }
      }
    }
  }

  traffic {
    type    = "TRAFFIC_TARGET_ALLOCATION_TYPE_LATEST"
    percent = 100
  }
}

resource "google_secret_manager_secret" "db_password" {
  secret_id = "${var.project_name}-db-password-${var.environment}"
  project   = var.project_id

  replication {
    auto {}
  }
}

resource "google_secret_manager_secret_version" "db_password" {
  secret      = google_secret_manager_secret.db_password.id
  secret_data_wo = var.database_password
}

resource "google_cloud_run_service_iam_member" "main_public" {
  location = google_cloud_run_v2_service.main.location
  service  = google_cloud_run_v2_service.main.name
  role     = "roles/run.invoker"
  member   = "allUsers"
  project  = var.project_id
}