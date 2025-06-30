output "dify_instance_group_name" {
  description = "Name of the Dify instance group"
  value       = module.compute_engine.instance_group_manager_id
}

output "dify_instance_template" {
  description = "Self link of the instance template"
  value       = module.compute_engine.instance_template_self_link
}

output "dify_instance_base_name" {
  description = "Base name for instances in the MIG"
  value       = module.compute_engine.instance_name
}

# Note: With MIG, individual instance IPs are dynamic. Use the load balancer IP instead.
output "dify_access_note" {
  description = "How to access Dify instances"
  value       = "Access Dify through the load balancer at ${module.load_balancer.load_balancer_url}. Individual instance IPs are managed by the MIG and may change."
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

# Load Balancer outputs
output "load_balancer_ip" {
  description = "The IP address of the load balancer"
  value       = module.load_balancer.load_balancer_ip
}

output "load_balancer_url" {
  description = "The URL to access the application through the load balancer"
  value       = module.load_balancer.load_balancer_url
}

output "backend_service_name" {
  description = "The name of the backend service"
  value       = module.load_balancer.backend_service_name
}

output "ssl_certificate_name" {
  description = "The name of the SSL certificate (if SSL is enabled)"
  value       = module.load_balancer.ssl_certificate_name
}

# Manifest Images Storage outputs
output "manifest_images_bucket_name" {
  description = "Name of the manifest images storage bucket"
  value       = module.storage.manifest_images_bucket_name
}

output "manifest_images_public_url" {
  description = "Public URL prefix for accessing manifest images"
  value       = module.storage.manifest_images_public_url
}

output "manifest_images_service_account_json" {
  description = "Base64 encoded service account key for manifest images upload (sensitive)"
  value       = module.storage.manifest_images_service_account_json
  sensitive   = true
}

output "manifest_images_service_account_email" {
  description = "Email of the manifest images service account"
  value       = module.storage.manifest_images_service_account_email
}