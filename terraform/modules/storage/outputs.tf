output "uploads_bucket_name" {
  description = "Name of the uploads storage bucket"
  value       = google_storage_bucket.uploads.name
}

output "uploads_bucket_url" {
  description = "URL of the uploads storage bucket"
  value       = google_storage_bucket.uploads.url
}

output "model_cache_bucket_name" {
  description = "Name of the model cache storage bucket"
  value       = google_storage_bucket.model_cache.name
}

output "model_cache_bucket_url" {
  description = "URL of the model cache storage bucket"
  value       = google_storage_bucket.model_cache.url
}

output "backups_bucket_name" {
  description = "Name of the backups storage bucket"
  value       = google_storage_bucket.backups.name
}

output "backups_bucket_url" {
  description = "URL of the backups storage bucket"
  value       = google_storage_bucket.backups.url
}

output "gcs_service_account_json" {
  value = try(google_service_account_key.dify_gcs.private_key, "")
  sensitive = true
  description = "Base64 encoded service account key for GCS access"
}

output "gcs_service_account_email" {
  value = try(google_service_account.dify_gcs.email, "")
  description = "Email of the GCS service account"
}

output "manifest_images_bucket_name" {
  description = "Name of the manifest images storage bucket"
  value       = google_storage_bucket.manifest_images.name
}

output "manifest_images_bucket_url" {
  description = "URL of the manifest images storage bucket"
  value       = google_storage_bucket.manifest_images.url
}

output "manifest_images_public_url" {
  description = "Public URL prefix for accessing manifest images"
  value       = "https://storage.googleapis.com/${google_storage_bucket.manifest_images.name}"
}

output "manifest_images_service_account_json" {
  value     = google_service_account_key.manifest_images_uploader.private_key
  sensitive = true
  description = "Base64 encoded service account key for manifest images upload"
}

output "manifest_images_service_account_email" {
  value = google_service_account.manifest_images_uploader.email
  description = "Email of the manifest images service account"
}

