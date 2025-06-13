locals {
  environment = "staging"
}

module "cloud_build" {
  source = "../../modules/cloud-build"

  project_id   = var.project_id
  project_name = var.project_name
  environment  = local.environment
}

module "networking" {
  source = "../../modules/networking"

  project_id   = var.project_id
  project_name = var.project_name
  region       = var.region
  environment  = local.environment

  subnet_cidr              = "10.0.0.0/24"
  services_cidr            = "10.1.0.0/20"
  pods_cidr                = "10.2.0.0/16"
  connector_cidr           = "10.8.0.0/28"
  connector_min_instances  = 2
  connector_max_instances  = 10
  connector_max_throughput = 1000
}

module "cloud_sql" {
  source = "../../modules/cloud-sql"

  project_id                = var.project_id
  project_name              = var.project_name
  region                    = var.region
  environment               = local.environment
  network_id                = module.networking.network_id
  private_vpc_connection    = module.networking.private_vpc_connection
  cloud_run_service_account = module.cloud_run.service_account_email

  database_version              = "POSTGRES_15"
  database_tier                 = "db-custom-2-7680"
  availability_type             = "ZONAL"
  disk_size                     = 100
  enable_point_in_time_recovery = false
  deletion_protection           = false
}

module "storage" {
  source = "../../modules/storage"

  project_id                = var.project_id
  project_name              = var.project_name
  region                    = var.region
  environment               = local.environment
  cloud_run_service_account = module.cloud_run.service_account_email

  force_destroy              = true
  uploads_retention_days     = 30
  model_cache_retention_days = 30
  model_cache_archive_days   = 180
  backup_retention_days      = 14
}

module "artifact_registry" {
  source = "../../modules/artifact-registry"

  project_id                  = var.project_id
  project_name                = var.project_name
  region                      = var.region
  environment                 = local.environment
  cloud_run_service_account   = module.cloud_run.service_account_email
  cloud_build_service_account = module.cloud_build.service_account_email

  keep_versions_count             = 5
  delete_versions_older_than_days = 30
}

module "cloud_run" {
  source = "../../modules/cloud-run"

  project_id                = var.project_id
  project_name              = var.project_name
  region                    = var.region
  environment               = local.environment
  vpc_connector_id          = module.networking.vpc_connector_id
  database_connection_name  = module.cloud_sql.instance_connection_name
  database_host             = module.cloud_sql.private_ip_address
  database_name             = module.cloud_sql.database_name
  database_user             = module.cloud_sql.database_user
  database_password         = module.cloud_sql.database_password
  plugin_daemon_key         = var.dify_plugin_daemon_key
  plugin_dify_inner_api_key = var.dify_plugin_dify_inner_api_key
  plugin_storage_bucket     = module.storage.plugin_storage_bucket_name

  nginx_image         = "nginx:1.28-alpine"
  api_image           = "langgenius/dify-api:1.4.2"
  web_image           = "langgenius/dify-web:1.4.2"
  worker_image        = "langgenius/dify-api:1.4.2"
  plugin_daemon_image = "langgenius/dify-plugin-daemon:0.1.2-serverless"
  sandbox_image       = "langgenius/dify-sandbox:0.2.12"

  common_env_vars = {
    LOG_LEVEL                           = "INFO"
    SECRET_KEY                          = var.dify_secret_key
    STORAGE_TYPE                        = "google-storage"
    GOOGLE_STORAGE_BUCKET               = module.storage.uploads_bucket_name
    GOOGLE_STORAGE_SERVICE_ACCOUNT_JSON = base64encode(var.gcs_service_account_json)
    REDIS_HOST                          = "localhost"
    REDIS_PORT                          = "6379"
    REDIS_DB                            = "0"
    CELERY_BROKER_URL                   = "redis://localhost:6379/1"
    MAIL_TYPE                           = var.mail_type
    MAIL_DEFAULT_SEND_FROM              = var.mail_default_send_from
    CODE_MAX_NUMBER                     = "3"
    CODE_MIN_NUMBER                     = "0"
    CODE_MAX_STRING_LENGTH              = "80000"
    MIGRATION_ENABLED                   = "true"
  }

  main_min_instances          = 1
  main_max_instances          = 10
  main_cpu                    = "2"
  main_memory                 = "4Gi"
  worker_min_instances        = 0
  worker_max_instances        = 5
  worker_cpu                  = "1"
  worker_memory               = "2Gi"
  sandbox_min_instances       = 0
  sandbox_max_instances       = 3
  sandbox_cpu                 = "1"
  sandbox_memory              = "2Gi"
  web_min_instances           = 1
  web_max_instances           = 5
  web_cpu                     = "1"
  web_memory                  = "1Gi"
  plugin_daemon_min_instances = 1
  plugin_daemon_max_instances = 10
  plugin_daemon_cpu           = "2"
  plugin_daemon_memory        = "4Gi"
}

