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

variable "subnet_cidr" {
  description = "CIDR for the main subnet"
  type        = string
  default     = "10.0.0.0/24"
}

variable "services_cidr" {
  description = "CIDR for services secondary range"
  type        = string
  default     = "10.1.0.0/20"
}

variable "pods_cidr" {
  description = "CIDR for pods secondary range"
  type        = string
  default     = "10.2.0.0/16"
}

variable "connector_cidr" {
  description = "CIDR for VPC connector"
  type        = string
  default     = "10.8.0.0/28"
}

variable "connector_min_instances" {
  description = "Minimum instances for VPC connector"
  type        = number
  default     = 2
}

variable "connector_max_instances" {
  description = "Maximum instances for VPC connector"
  type        = number
  default     = 10
}

variable "connector_max_throughput" {
  description = "Maximum throughput for VPC connector in Mbps"
  type        = number
  default     = 1000
}