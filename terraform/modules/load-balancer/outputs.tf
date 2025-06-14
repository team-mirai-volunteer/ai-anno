output "load_balancer_ip" {
  description = "The IP address of the load balancer"
  value       = google_compute_global_address.lb_ip.address
}

output "load_balancer_ip_address_name" {
  description = "The name of the IP address resource"
  value       = google_compute_global_address.lb_ip.name
}

output "load_balancer_url" {
  description = "The URL to access the application through the load balancer"
  value       = var.enable_ssl && var.domain_name != "" ? "https://${var.domain_name}" : "http://${google_compute_global_address.lb_ip.address}"
}

output "backend_service_id" {
  description = "The ID of the backend service"
  value       = google_compute_backend_service.backend.id
}

output "backend_service_name" {
  description = "The name of the backend service"
  value       = google_compute_backend_service.backend.name
}

# Note: instance_group_id output removed as we now only use MIG

output "health_check_id" {
  description = "The ID of the health check"
  value       = google_compute_health_check.health_check.id
}

output "ssl_certificate_id" {
  description = "The ID of the SSL certificate (if SSL is enabled)"
  value       = var.enable_ssl && var.domain_name != "" ? google_compute_managed_ssl_certificate.ssl_cert[0].id : null
}

output "ssl_certificate_name" {
  description = "The name of the SSL certificate (if SSL is enabled)"
  value       = var.enable_ssl && var.domain_name != "" ? google_compute_managed_ssl_certificate.ssl_cert[0].name : null
}