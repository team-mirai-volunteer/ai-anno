variable "project_id" {
  description = "The GCP project ID"
  type        = string
}

variable "project_name" {
  description = "The project name to use as prefix"
  type        = string
  default     = "dify"
}

variable "region" {
  description = "The GCP region"
  type        = string
}

variable "environment" {
  description = "Environment name"
  type        = string
}

variable "force_destroy" {
  description = "Allow destruction of buckets with content"
  type        = bool
  default     = false
}

variable "uploads_retention_days" {
  description = "Days to retain uploaded files"
  type        = number
  default     = 90
}

variable "model_cache_retention_days" {
  description = "Days before moving model cache to NEARLINE"
  type        = number
  default     = 30
}

variable "model_cache_archive_days" {
  description = "Days before moving model cache to ARCHIVE"
  type        = number
  default     = 365
}

variable "backup_retention_days" {
  description = "Days to retain backup files"
  type        = number
  default     = 30
}

