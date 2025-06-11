variable "project_id" {
  description = "The GCP project ID"
  type        = string
}

variable "project_name" {
  description = "The project name to use as prefix"
  type        = string
  default     = "ai-anno"
}

variable "environment" {
  description = "Environment name"
  type        = string
}