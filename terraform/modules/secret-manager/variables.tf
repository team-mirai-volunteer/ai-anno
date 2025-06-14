variable "project_id" {
  description = "The GCP project ID"
  type        = string
}

variable "project_name" {
  description = "The project name"
  type        = string
}

variable "environment" {
  description = "Environment (staging/production)"
  type        = string
}

variable "database_password" {
  description = "Database password"
  type        = string
  sensitive   = true
}

variable "dify_secret_key" {
  description = "Dify application secret key"
  type        = string
  sensitive   = true
}

variable "gcs_service_account_json" {
  description = "GCS service account JSON"
  type        = string
  sensitive   = true
}

variable "plugin_daemon_key" {
  description = "Plugin daemon key"
  type        = string
  sensitive   = true
}

variable "plugin_inner_api_key" {
  description = "Plugin inner API key"
  type        = string
  sensitive   = true
}

variable "vm_service_account_email" {
  description = "VM service account email"
  type        = string
}