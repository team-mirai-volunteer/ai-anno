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

output "plugin_storage_bucket_name" {
  description = "Name of the plugin storage bucket"
  value       = google_storage_bucket.plugin_storage.name
}

output "plugin_storage_bucket_url" {
  description = "URL of the plugin storage bucket"
  value       = google_storage_bucket.plugin_storage.url
}