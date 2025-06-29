locals {
  environment = "staging"
}

# Generate random keys for internal use
resource "random_password" "plugin_daemon_key" {
  length  = 32
  special = false
}

resource "random_password" "plugin_dify_inner_api_key" {
  length  = 32
  special = false
}

resource "random_password" "dify_inner_api_key" {
  length  = 32
  special = false
}

resource "random_password" "redis_password" {
  length  = 32
  special = false
}

resource "random_password" "code_execution_api_key" {
  length  = 32
  special = false
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

  project_id             = var.project_id
  project_name           = var.project_name
  region                 = var.region
  environment            = local.environment
  network_id             = module.networking.network_id
  private_vpc_connection = module.networking.private_vpc_connection

  database_version              = "POSTGRES_17"
  database_tier                 = "db-custom-2-7680"
  availability_type             = "ZONAL"
  disk_size                     = 100
  enable_point_in_time_recovery = false
  deletion_protection           = true
}

module "storage" {
  source = "../../modules/storage"

  project_id   = var.project_id
  project_name = var.project_name
  region       = var.region
  environment  = local.environment

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

  uploads_bucket_name     = module.storage.uploads_bucket_name
  model_cache_bucket_name = module.storage.model_cache_bucket_name
  artifact_registry_name  = module.artifact_registry.repository_name

  database_host = module.cloud_sql.private_ip_address
  database_name = module.cloud_sql.database_name
  database_user = module.cloud_sql.database_user

  # AWS S3 configuration for plugin storage
  plugin_aws_region = var.plugin_aws_region
  plugin_s3_bucket  = var.plugin_s3_bucket

  machine_type      = "e2-standard-8" # 8 vCPUs, 32GB RAM for staging
  boot_disk_size    = 200
  boot_disk_type    = "pd-ssd"
  ssh_source_ranges = var.ssh_source_ranges
}

module "secret_manager" {
  source = "../../modules/secret-manager"

  project_id   = var.project_id
  project_name = var.project_name
  environment  = local.environment

  database_password        = module.cloud_sql.database_password
  dify_secret_key          = var.dify_secret_key
  gcs_service_account_json = module.storage.gcs_service_account_json
  plugin_daemon_key        = random_password.plugin_daemon_key.result
  plugin_inner_api_key     = random_password.plugin_dify_inner_api_key.result
  server_key               = var.dify_server_key
  dify_inner_api_key       = random_password.dify_inner_api_key.result
  vm_service_account_email = module.compute_engine.service_account_email
  init_password            = var.init_password
  redis_password           = random_password.redis_password.result
  code_execution_api_key   = random_password.code_execution_api_key.result
  plugin_s3_access_key     = var.plugin_s3_access_key
  plugin_s3_secret_key     = var.plugin_s3_secret_key
}

module "load_balancer" {
  source = "../../modules/load-balancer"

  project_id         = var.project_id
  project_name       = var.project_name
  environment        = local.environment
  region             = var.region
  network_name       = module.networking.network_name
  instance_group_url = module.compute_engine.instance_group_manager_instance_group

  # Load balancer configuration
  domain_name         = var.domain_name
  enable_ssl          = var.enable_ssl
  enable_cdn          = var.enable_cdn
  health_check_path   = "/console/api/ping"  # Dify health check endpoint
  health_check_port   = 80
  backend_timeout_sec = 30
  session_affinity    = "NONE"  # No session affinity for staging
}

