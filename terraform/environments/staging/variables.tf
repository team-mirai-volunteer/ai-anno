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

variable "dify_server_key" {
  description = "Dify server key"
  type        = string
  sensitive   = true
}

variable "ssh_source_ranges" {
  description = "Source IP ranges allowed for SSH access"
  type        = list(string)
  default     = ["0.0.0.0/0"]
}

# Load Balancer variables
variable "domain_name" {
  description = "Domain name for SSL certificate"
  type        = string
}



variable "init_password" {
  description = "Initial admin password for Dify"
  type        = string
  sensitive   = true
}

# AWS S3 variables for plugin storage
variable "plugin_s3_access_key" {
  description = "AWS S3 access key for plugin storage"
  type        = string
  sensitive   = true
}

variable "plugin_s3_secret_key" {
  description = "AWS S3 secret key for plugin storage"
  type        = string
  sensitive   = true
}

variable "plugin_aws_region" {
  description = "AWS region for S3 bucket"
  type        = string
  default     = "us-east-1"
}

variable "plugin_s3_bucket" {
  description = "AWS S3 bucket name for plugin storage"
  type        = string
}

variable "notion_internal_secret" {
  description = "Notion internal integration secret"
  type        = string
  sensitive   = true
}

