output "main_service_url" {
  description = "URL of the main Dify service"
  value       = module.cloud_run.main_service_url
}

output "database_instance_name" {
  description = "Name of the Cloud SQL instance"
  value       = module.cloud_sql.instance_name
}

output "uploads_bucket_name" {
  description = "Name of the uploads storage bucket"
  value       = module.storage.uploads_bucket_name
}

output "model_cache_bucket_name" {
  description = "Name of the model cache storage bucket"
  value       = module.storage.model_cache_bucket_name
}

output "artifact_registry_url" {
  description = "URL of the Artifact Registry repository"
  value       = module.artifact_registry.repository_url
}

output "vpc_network_name" {
  description = "Name of the VPC network"
  value       = module.networking.network_name
}