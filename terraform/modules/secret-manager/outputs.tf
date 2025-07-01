output "database_password_secret_id" {
  description = "Secret Manager secret ID for database password"
  value       = google_secret_manager_secret.database_password.id
}

output "dify_secret_key_secret_id" {
  description = "Secret Manager secret ID for Dify secret key"
  value       = google_secret_manager_secret.dify_secret_key.id
}

output "gcs_service_account_secret_id" {
  description = "Secret Manager secret ID for GCS service account"
  value       = google_secret_manager_secret.gcs_service_account.id
}

output "plugin_daemon_key_secret_id" {
  description = "Secret Manager secret ID for plugin daemon key"
  value       = google_secret_manager_secret.plugin_daemon_key.id
}

output "plugin_inner_api_key_secret_id" {
  description = "Secret Manager secret ID for plugin inner API key"
  value       = google_secret_manager_secret.plugin_inner_api_key.id
}

output "manifest_service_account_secret_id" {
  description = "Secret Manager secret ID for manifest service account"
  value       = google_secret_manager_secret.manifest_service_account.id
}