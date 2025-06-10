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

variable "cloud_run_service_account" {
  description = "Service account email for Cloud Run"
  type        = string
}

variable "keep_versions_count" {
  description = "Number of recent versions to keep"
  type        = number
  default     = 10
}

variable "delete_versions_older_than_days" {
  description = "Delete versions older than this many days"
  type        = number
  default     = 90
}