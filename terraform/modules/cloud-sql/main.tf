resource "random_password" "db_password" {
  length  = 32
  special = true
}

resource "google_sql_database_instance" "main" {
  name             = "${var.project_name}-db-${var.environment}"
  database_version = var.database_version
  region           = var.region
  project          = var.project_id

  settings {
    tier              = var.database_tier
    availability_type = var.availability_type
    disk_type         = "PD_SSD"
    disk_size         = var.disk_size
    disk_autoresize   = true

    backup_configuration {
      enabled                        = true
      start_time                     = "03:00"
      point_in_time_recovery_enabled = var.enable_point_in_time_recovery
      transaction_log_retention_days = 7
      backup_retention_settings {
        retained_backups = 30
        retention_unit   = "COUNT"
      }
    }

    ip_configuration {
      ipv4_enabled    = false
      private_network = var.network_id
    }

    database_flags {
      name  = "cloudsql.enable_pg_cron"
      value = "on"
    }

    insights_config {
      query_insights_enabled  = true
      query_plans_per_minute  = 5
      query_string_length     = 1024
      record_application_tags = true
      record_client_address   = true
    }

    maintenance_window {
      day          = 7
      hour         = 4
      update_track = "stable"
    }
  }

  deletion_protection = var.deletion_protection

  depends_on = [var.private_vpc_connection]
}

resource "google_sql_database" "main" {
  name     = "main-${var.environment}"
  instance = google_sql_database_instance.main.name
  project  = var.project_id
}

resource "google_sql_user" "main" {
  name     = "main-${var.environment}"
  instance = google_sql_database_instance.main.name
  password = random_password.db_password.result
  project  = var.project_id
}

locals {
  pgvector_init_script = <<-EOT
    CREATE EXTENSION IF NOT EXISTS vector;
    CREATE EXTENSION IF NOT EXISTS pg_trgm;
    CREATE EXTENSION IF NOT EXISTS btree_gin;
  EOT
}

resource "null_resource" "init_pgvector" {
  provisioner "local-exec" {
    command = <<-EOT
      echo '${local.pgvector_init_script}' > /tmp/init_pgvector.sql
      gcloud sql import sql ${google_sql_database_instance.main.name} /tmp/init_pgvector.sql \
        --database=${google_sql_database.main.name} \
        --project=${var.project_id}
      rm /tmp/init_pgvector.sql
    EOT
  }

  depends_on = [
    google_sql_database.main,
    google_sql_user.main
  ]
}
