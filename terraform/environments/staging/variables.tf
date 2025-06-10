variable "project_id" {
  description = "The GCP project ID"
  type        = string
}

variable "project_number" {
  description = "The GCP project number"
  type        = string
}

variable "project_name" {
  description = "The project name to use as prefix"
  type        = string
  default     = "ai-anno"
}

variable "region" {
  description = "The GCP region for deploying resources"
  type        = string
  default     = "asia-northeast1"
}

variable "dify_secret_key" {
  description = "Secret key for Dify application"
  type        = string
  sensitive   = true
}

variable "gcs_service_account_json" {
  description = "Service account JSON for GCS access"
  type        = string
  sensitive   = true
}

variable "mail_type" {
  description = "Mail service type (smtp, resend)"
  type        = string
  default     = "smtp"
}

variable "mail_default_send_from" {
  description = "Default email sender address"
  type        = string
  default     = "noreply@example.com"
}