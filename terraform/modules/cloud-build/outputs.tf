output "service_account_email" {
  description = "The email of the Cloud Build service account"
  value       = google_service_account.cloud_build.email
}

output "service_account_id" {
  description = "The ID of the Cloud Build service account"
  value       = google_service_account.cloud_build.id
}