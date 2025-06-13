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
    # Database configuration
    DB_USERNAME = var.database_user
    DB_HOST     = var.database_host
    DB_PORT     = "5432"
    DB_DATABASE = var.database_name

    # PostgreSQL specific
    POSTGRES_USER = var.database_user
    POSTGRES_DB   = var.database_name
    POSTGRES_HOST = var.database_host
    POSTGRES_PORT = "5432"

    # Redis configuration
    REDIS_HOST     = "localhost"
    REDIS_PORT     = "6379"
    REDIS_USERNAME = ""
    REDIS_PASSWORD = ""
    REDIS_USE_SSL  = "false"
    REDIS_DB       = "0"

    # Celery configuration
    CELERY_BROKER_URL = "redis://localhost:6379/1"
    BROKER_USE_SSL    = "false"

    # SSRF Protection
    SSRF_PROXY_HTTP_URL  = "http://localhost:3128"
    SSRF_PROXY_HTTPS_URL = "http://localhost:3128"

    # Environment
    ENVIRONMENT = var.environment
  })
}

resource "google_cloud_run_v2_service" "dify_service" {
  name                = "${var.project_name}-dify-service-${var.environment}"
  location            = var.region
  project             = var.project_id
  deletion_protection = false

  template {
    service_account       = google_service_account.cloud_run.email
    execution_environment = "EXECUTION_ENVIRONMENT_GEN2"

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
      resources {
        limits = {
          cpu    = "0.5"
          memory = "1Gi"
        }
      }
      ports {
        name           = "http1"
        container_port = 80
      }
      depends_on = ["dify-web", "dify-api", "dify-plugin-daemon", "redis"]
      startup_probe {
        timeout_seconds   = 30
        period_seconds    = 30
        failure_threshold = 1
        tcp_socket {
          port = 80
        }
      }
    }

    containers {
      name  = "dify-api"
      image = var.api_image


      resources {
        cpu_idle = true
        limits = {
          cpu    = "1.0"
          memory = var.main_memory
        }
      }


      env {
        name  = "MODE"
        value = "api"
      }

      env {
        name  = "EDITION"
        value = "SELF_HOSTED"
      }

      env {
        name  = "PLUGIN_DAEMON_URL"
        value = "http://127.0.0.1:5002"
      }

      env {
        name  = "ENDPOINT_URL_TEMPLATE"
        value = "http://127.0.0.1/e/{hook_id}"
      }

      env {
        name  = "SENTRY_DSN"
        value = ""
      }

      env {
        name  = "PLUGIN_REMOTE_INSTALL_HOST"
        value = "127.0.0.1"
      }

      env {
        name  = "PLUGIN_REMOTE_INSTALL_PORT"
        value = 5003
      }

      env {
        name  = "PLUGIN_MAX_PACKAGE_SIZE"
        value = 52428800
      }

      env {
        name  = "INNER_API_KEY_FOR_PLUGIN"
        value = var.plugin_dify_inner_api_key
      }

      dynamic "env" {
        for_each = local.env_vars
        content {
          name  = env.key
          value = env.value
        }
      }

      env {
        name = "DB_PASSWORD"
        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.db_password.secret_id
            version = "latest"
          }
        }
      }

      env {
        name = "POSTGRES_PASSWORD"
        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.db_password.secret_id
            version = "latest"
          }
        }
      }

      startup_probe {
        timeout_seconds   = 30
        period_seconds    = 30
        failure_threshold = 1
        tcp_socket {
          port = 5001
        }
      }
    }

    containers {
      name  = "dify-web"
      image = var.web_image


      resources {
        cpu_idle = true
        limits = {
          cpu    = "0.5"
          memory = var.web_memory
        }
      }


      env {
        name  = "CONSOLE_API_URL"
        value = ""
      }

      env {
        name  = "APP_API_URL"
        value = ""
      }

      env {
        name  = "SENTRY_DSN"
        value = ""
      }


      env {
        name  = "CSP_WHITELIST"
        value = ""
      }

      env {
        name  = "MARKETPLACE_API_URL"
        value = "https://marketplace.dify.ai"
      }

      env {
        name  = "MARKETPLACE_URL"
        value = "https://marketplace.dify.ai"
      }

      env {
        name  = "TOP_K_MAX_VALUE"
        value = ""
      }

      env {
        name  = "INDEXING_MAX_SEGMENTATION_TOKENS_LENGTH"
        value = ""
      }

      env {
        name  = "PM2_INSTANCES"
        value = 2
      }

      # NOTE: Changing PM2_HOME is required for pm2 to work properly on Cloud Run Gen2 environment because of permission issues
      env {
        name  = "PM2_HOME"
        value = "/app/web/.pm2"
      }

      env {
        name  = "LOOP_NODE_MAX_COUNT"
        value = 100
      }

      env {
        name  = "MAX_TOOLS_NUM"
        value = 10
      }

      startup_probe {
        timeout_seconds   = 30
        period_seconds    = 30
        failure_threshold = 1
        tcp_socket {
          port = 3000
        }
      }
    }

    containers {
      name  = "dify-plugin-daemon"
      image = var.plugin_daemon_image


      resources {
        cpu_idle = true
        limits = {
          cpu    = "1.0"
          memory = var.plugin_daemon_memory
        }
      }


      dynamic "env" {
        for_each = local.env_vars
        content {
          name  = env.key
          value = env.value
        }
      }

      env {
        name  = "SERVER_PORT"
        value = 5002
      }

      env {
        name  = "SERVER_KEY"
        value = var.plugin_daemon_key
      }

      env {
        name  = "MAX_PLUGIN_PACKAGE_SIZE"
        value = 52428800
      }

      env {
        name  = "PPROF_ENABLED"
        value = false
      }

      env {
        name  = "DIFY_INNER_API_URL"
        value = "http://127.0.0.1:5001"
      }
      env {
        name  = "DIFY_INNER_API_KEY"
        value = var.plugin_dify_inner_api_key
      }
      env {
        name  = "PLUGIN_REMOTE_INSTALLING_HOST"
        value = "0.0.0.0"
      }
      env {
        name  = "PLUGIN_REMOTE_INSTALLING_PORT"
        value = 5003
      }

      env {
        name  = "PLUGIN_WORKING_PATH"
        value = "/tmp/plugin-storage/cwd"
      }

      env {
        name  = "PLUGIN_STORAGE_TYPE"
        value = "google-storage"
      }

      env {
        name  = "PLUGIN_STORAGE_OSS_BUCKET"
        value = var.plugin_storage_bucket
      }

      env {
        name  = "FORCE_VERIFYING_SIGNATURE"
        value = true
      }
      env {
        name  = "PYTHON_ENV_INIT_TIMEOUT"
        value = 120
      }
      env {
        name  = "PLUGIN_MAX_EXECUTION_TIMEOUT"
        value = 600
      }
      env {
        name  = "PIP_MIRROR_URL"
        value = ""
      }

      startup_probe {
        timeout_seconds   = 30
        period_seconds    = 30
        failure_threshold = 1
        tcp_socket {
          port = 5002
        }
      }
    }

    containers {
      name  = "dify-worker"
      image = var.worker_image

      resources {
        cpu_idle = true
        limits = {
          cpu    = "1.0"
          memory = var.main_memory
        }
      }


      env {
        name  = "MODE"
        value = "worker"
      }

      env {
        name  = "EDITION"
        value = "SELF_HOSTED"
      }

      env {
        name  = "PLUGIN_DAEMON_URL"
        value = "http://127.0.0.1:5002"
      }

      env {
        name  = "ENDPOINT_URL_TEMPLATE"
        value = "http://127.0.0.1/e/{hook_id}"
      }

      env {
        name  = "SENTRY_DSN"
        value = ""
      }

      env {
        name  = "PLUGIN_REMOTE_INSTALL_HOST"
        value = "127.0.0.1"
      }

      env {
        name  = "PLUGIN_REMOTE_INSTALL_PORT"
        value = 5003
      }

      env {
        name  = "PLUGIN_MAX_PACKAGE_SIZE"
        value = 52428800
      }

      env {
        name  = "INNER_API_KEY_FOR_PLUGIN"
        value = var.plugin_dify_inner_api_key
      }

      dynamic "env" {
        for_each = local.env_vars
        content {
          name  = env.key
          value = env.value
        }
      }

      env {
        name = "DB_PASSWORD"
        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.db_password.secret_id
            version = "latest"
          }
        }
      }

      env {
        name = "POSTGRES_PASSWORD"
        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.db_password.secret_id
            version = "latest"
          }
        }
      }

      # TODO: 確認して private IP で接続できるか
      volume_mounts {
        name       = "cloudsql"
        mount_path = "/cloudsql"
      }
    }

    containers {
      name  = "redis"
      image = "redis:6-alpine"

      resources {
        cpu_idle = true
        limits = {
          cpu    = "1"
          memory = "128Mi"
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

    containers {
      name  = "ssrf-proxy"
      image = "ubuntu/squid:latest"


      resources {
        cpu_idle = true
        limits = {
          cpu    = "0.25"
          memory = "512Mi"
        }
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
      ports {
        name           = "http1"
        container_port = 8080
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
  secret         = google_secret_manager_secret.db_password.id
  secret_data_wo = var.database_password
}

resource "google_cloud_run_service_iam_member" "main_public" {
  location = google_cloud_run_v2_service.dify_service.location
  service  = google_cloud_run_v2_service.dify_service.name
  role     = "roles/run.invoker"
  member   = "allUsers"
  project  = var.project_id
}

