output "main_service_url" {
  description = "URL of the main Cloud Run service"
  value       = google_cloud_run_v2_service.dify_service.uri
}

output "main_service_name" {
  description = "Name of the main Cloud Run service"
  value       = google_cloud_run_v2_service.dify_service.name
}

output "sandbox_service_name" {
  description = "Name of the sandbox Cloud Run service"
  value       = google_cloud_run_v2_service.sandbox.name
}

output "service_account_email" {
  description = "Email of the Cloud Run service account"
  value       = google_service_account.cloud_run.email
}

