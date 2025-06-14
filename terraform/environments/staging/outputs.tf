output "dify_vm_name" {
  description = "Name of the Dify VM instance"
  value       = module.compute_engine.instance_name
}

output "dify_vm_internal_ip" {
  description = "Internal IP of the Dify VM"
  value       = module.compute_engine.internal_ip
}

output "dify_vm_external_ip" {
  description = "External IP of the Dify VM (if assigned)"
  value       = module.compute_engine.external_ip
}

output "dify_vm_zone" {
  description = "Zone where the Dify VM is deployed"
  value       = module.compute_engine.zone
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