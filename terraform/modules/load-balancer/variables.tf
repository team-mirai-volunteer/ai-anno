variable "project_id" {
  description = "The GCP project ID"
  type        = string
}

variable "project_name" {
  description = "The project name to use as prefix"
  type        = string
}

variable "environment" {
  description = "Environment name (e.g., staging, production)"
  type        = string
}

variable "region" {
  description = "The GCP region for resources"
  type        = string
}

variable "instance_group_url" {
  description = "URL of the managed instance group to use as backend"
  type        = string
}

variable "network_name" {
  description = "Name of the VPC network"
  type        = string
}

variable "domain_name" {
  description = "Domain name for SSL certificate"
  type        = string
}

variable "health_check_path" {
  description = "Path for health check endpoint"
  type        = string
  default     = "/"
}

variable "health_check_port" {
  description = "Port for health check"
  type        = number
  default     = 80
}


variable "enable_cdn" {
  description = "Enable Cloud CDN for static content caching"
  type        = bool
  default     = false
}

variable "backend_timeout_sec" {
  description = "Timeout for backend requests in seconds"
  type        = number
  default     = 30
}

variable "session_affinity" {
  description = "Session affinity type (NONE, CLIENT_IP, CLIENT_IP_PORT_PROTO, etc.)"
  type        = string
  default     = "NONE"
}