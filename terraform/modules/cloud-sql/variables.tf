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

variable "network_id" {
  description = "The VPC network ID"
  type        = string
}

variable "private_vpc_connection" {
  description = "The private VPC connection"
  type        = any
}

variable "database_version" {
  description = "PostgreSQL database version"
  type        = string
  default     = "POSTGRES_15"
}

variable "database_tier" {
  description = "Database instance tier"
  type        = string
  default     = "db-custom-2-7680"
}

variable "availability_type" {
  description = "Availability type (ZONAL or REGIONAL)"
  type        = string
  default     = "ZONAL"
}

variable "disk_size" {
  description = "Disk size in GB"
  type        = number
  default     = 100
}

variable "enable_point_in_time_recovery" {
  description = "Enable point in time recovery"
  type        = bool
  default     = false
}

variable "deletion_protection" {
  description = "Enable deletion protection"
  type        = bool
  default     = false
}