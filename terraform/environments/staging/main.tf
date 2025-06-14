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
  cloud_build_service_account = module.cloud_build.service_account_email

  keep_versions_count             = 5
  delete_versions_older_than_days = 30
}

module "compute_engine" {
  source = "../../modules/compute-engine"

  project_id   = var.project_id
  project_name = var.project_name
  region       = var.region
  zone         = "${var.region}-a"
  environment  = local.environment

  network_id   = module.networking.network_id
  network_name = module.networking.network_name
  subnet_id    = module.networking.subnet_id

  uploads_bucket_name        = module.storage.uploads_bucket_name
  model_cache_bucket_name    = module.storage.model_cache_bucket_name
  plugin_storage_bucket_name = module.storage.plugin_storage_bucket_name
  artifact_registry_name     = module.artifact_registry.repository_name

  database_host              = module.cloud_sql.private_ip_address
  database_name              = module.cloud_sql.database_name
  database_user              = module.cloud_sql.database_user

  # HMAC keys for plugin daemon S3 compatibility
  hmac_access_key_secret_id = module.storage.hmac_access_key_secret_id
  hmac_secret_key_secret_id = module.storage.hmac_secret_key_secret_id

  machine_type    = "e2-standard-8"  # 8 vCPUs, 32GB RAM for staging
  boot_disk_size  = 200
  boot_disk_type  = "pd-ssd"
  ssh_keys        = var.ssh_keys
  ssh_source_ranges = var.ssh_source_ranges
  iap_ssh_users   = var.iap_ssh_users
}

module "secret_manager" {
  source = "../../modules/secret-manager"

  project_id   = var.project_id
  project_name = var.project_name
  environment  = local.environment

  database_password         = module.cloud_sql.database_password
  dify_secret_key           = var.dify_secret_key
  gcs_service_account_json  = module.storage.gcs_service_account_json
  plugin_daemon_key         = var.dify_plugin_daemon_key
  plugin_inner_api_key      = var.dify_plugin_dify_inner_api_key
  server_key                = var.dify_server_key
  dify_inner_api_key        = var.dify_inner_api_key
  vm_service_account_email  = module.compute_engine.service_account_email
}

