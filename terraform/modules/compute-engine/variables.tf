variable "project_id" {
  description = "The GCP project ID"
  type        = string
}

variable "project_name" {
  description = "The project name"
  type        = string
}

variable "region" {
  description = "The GCP region"
  type        = string
}

variable "zone" {
  description = "The GCP zone for the VM instance"
  type        = string
}

variable "environment" {
  description = "Environment (staging/production)"
  type        = string
}

variable "network_id" {
  description = "The VPC network ID"
  type        = string
}

variable "network_name" {
  description = "The VPC network name"
  type        = string
}

variable "subnet_id" {
  description = "The subnet ID for the VM"
  type        = string
}

variable "machine_type" {
  description = "Machine type for the VM instance"
  type        = string
  default     = "e2-standard-4"
}

variable "boot_disk_image" {
  description = "Boot disk image for the VM"
  type        = string
  default     = "ubuntu-os-cloud/ubuntu-2204-lts"
}

variable "boot_disk_size" {
  description = "Boot disk size in GB"
  type        = number
  default     = 100
}

variable "boot_disk_type" {
  description = "Boot disk type"
  type        = string
  default     = "pd-standard"
}

variable "ssh_keys" {
  description = "SSH keys for VM access"
  type        = string
  default     = ""
}

variable "ssh_source_ranges" {
  description = "Source IP ranges allowed for SSH access"
  type        = list(string)
  default     = ["0.0.0.0/0"]
}

variable "vm_user" {
  description = "Default user for the VM"
  type        = string
  default     = "ubuntu"
}

variable "uploads_bucket_name" {
  description = "Name of the uploads storage bucket"
  type        = string
}

variable "model_cache_bucket_name" {
  description = "Name of the model cache storage bucket"
  type        = string
}

variable "plugin_storage_bucket_name" {
  description = "Name of the plugin storage bucket"
  type        = string
}

variable "artifact_registry_name" {
  description = "Name of the Artifact Registry repository"
  type        = string
}

variable "database_host" {
  description = "Database host address"
  type        = string
}

variable "database_name" {
  description = "Database name"
  type        = string
}

variable "database_user" {
  description = "Database username"
  type        = string
}

variable "database_password" {
  description = "Database password"
  type        = string
  sensitive   = true
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

variable "plugin_daemon_key" {
  description = "Dify Plugin daemon key"
  type        = string
  sensitive   = true
}

variable "plugin_dify_inner_api_key" {
  description = "Dify inner API key for plugin"
  type        = string
  sensitive   = true
}