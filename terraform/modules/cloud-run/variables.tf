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

variable "vpc_connector_id" {
  description = "VPC connector ID"
  type        = string
}

variable "database_connection_name" {
  description = "Cloud SQL instance connection name"
  type        = string
}

variable "database_host" {
  description = "Database host"
  type        = string
}

variable "database_name" {
  description = "Database name"
  type        = string
}

variable "database_user" {
  description = "Database user"
  type        = string
}

variable "database_password" {
  description = "Database password"
  type        = string
  sensitive   = true
}

variable "plugin_daemon_key" {
  description = "Plugin daemon key"
  type        = string
  sensitive   = true
}

variable "plugin_dify_inner_api_key" {
  description = "Dify inner API key for plugin"
  type        = string
  sensitive   = true
}

variable "plugin_storage_bucket" {
  description = "Google Cloud Storage bucket for plugin storage"
  type        = string
}

variable "nginx_image" {
  description = "Docker image for nginx"
  type        = string
}

variable "api_image" {
  description = "Docker image for API"
  type        = string
}

variable "web_image" {
  description = "Docker image for web"
  type        = string
}

variable "worker_image" {
  description = "Docker image for worker"
  type        = string
}

variable "plugin_daemon_image" {
  description = "Docker image for plugin daemon"
  type        = string
}

variable "sandbox_image" {
  description = "Docker image for sandbox"
  type        = string
}

variable "common_env_vars" {
  description = "Common environment variables for all containers"
  type        = map(string)
  default     = {}
}

variable "main_min_instances" {
  description = "Minimum instances for main service"
  type        = number
  default     = 1
}

variable "main_max_instances" {
  description = "Maximum instances for main service"
  type        = number
  default     = 100
}

variable "main_cpu" {
  description = "CPU allocation for main container"
  type        = string
  default     = "2"
}

variable "main_memory" {
  description = "Memory allocation for main container"
  type        = string
  default     = "4Gi"
}

variable "worker_min_instances" {
  description = "Minimum instances for worker service"
  type        = number
  default     = 0
}

variable "worker_max_instances" {
  description = "Maximum instances for worker service"
  type        = number
  default     = 50
}

variable "worker_cpu" {
  description = "CPU allocation for worker container"
  type        = string
  default     = "2"
}

variable "worker_memory" {
  description = "Memory allocation for worker container"
  type        = string
  default     = "4Gi"
}

variable "sandbox_min_instances" {
  description = "Minimum instances for sandbox service"
  type        = number
  default     = 0
}

variable "sandbox_max_instances" {
  description = "Maximum instances for sandbox service"
  type        = number
  default     = 10
}

variable "sandbox_cpu" {
  description = "CPU allocation for sandbox container"
  type        = string
  default     = "1"
}

variable "sandbox_memory" {
  description = "Memory allocation for sandbox container"
  type        = string
  default     = "2Gi"
}

variable "web_min_instances" {
  description = "Minimum instances for web service"
  type        = number
  default     = 1
}

variable "web_max_instances" {
  description = "Maximum instances for web service"
  type        = number
  default     = 10
}

variable "web_cpu" {
  description = "CPU allocation for web container"
  type        = string
  default     = "1"
}

variable "web_memory" {
  description = "Memory allocation for web container"
  type        = string
  default     = "2Gi"
}

variable "plugin_daemon_min_instances" {
  description = "Minimum instances for daemon service"
  type        = number
  default     = 1
}

variable "plugin_daemon_max_instances" {
  description = "Maximum instances for daemon service"
  type        = number
  default     = 100
}

variable "plugin_daemon_cpu" {
  description = "CPU allocation for daemon container"
  type        = string
  default     = "2"
}

variable "plugin_daemon_memory" {
  description = "Memory allocation for daemon container"
  type        = string
  default     = "4Gi"
}

