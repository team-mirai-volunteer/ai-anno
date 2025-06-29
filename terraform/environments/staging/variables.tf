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

variable "dify_plugin_daemon_key" {
  description = "Dify Plugin daemon key"
  type        = string
  sensitive   = true
}

variable "dify_plugin_dify_inner_api_key" {
  description = "Dify inner API key for plugin"
  type        = string
  sensitive   = true
}

variable "dify_server_key" {
  description = "Dify server key"
  type        = string
  sensitive   = true
}

variable "dify_inner_api_key" {
  description = "Dify inner API key"
  type        = string
  sensitive   = true
}

variable "ssh_keys" {
  description = "SSH keys for VM access in format 'username:ssh-rsa AAAAB3...'"
  type        = string
  default     = ""
}

variable "ssh_source_ranges" {
  description = "Source IP ranges allowed for SSH access"
  type        = list(string)
  default     = ["0.0.0.0/0"]
}

variable "iap_ssh_users" {
  description = "List of users who can SSH via IAP"
  type        = list(string)
  default     = []
}

# Load Balancer variables
variable "domain_name" {
  description = "Domain name for SSL certificate (leave empty for IP-only access)"
  type        = string
  default     = ""
}

variable "enable_ssl" {
  description = "Enable SSL certificate and HTTPS"
  type        = bool
  default     = false
}

variable "enable_cdn" {
  description = "Enable Cloud CDN for static content caching"
  type        = bool
  default     = false
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

variable "init_password" {
  description = "Initial admin password for Dify"
  type        = string
  sensitive   = true
}

variable "redis_password" {
  description = "Redis password"
  type        = string
  sensitive   = true
}

variable "code_execution_api_key" {
  description = "Code execution API key"
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

