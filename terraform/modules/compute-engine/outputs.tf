output "instance_name" {
  description = "Name of the Compute Engine instance"
  value       = google_compute_instance.dify.name
}

output "instance_id" {
  description = "ID of the Compute Engine instance"
  value       = google_compute_instance.dify.instance_id
}

output "internal_ip" {
  description = "Internal IP address of the instance"
  value       = google_compute_instance.dify.network_interface[0].network_ip
}

output "external_ip" {
  description = "External IP address of the instance"
  value       = length(google_compute_instance.dify.network_interface[0].access_config) > 0 ? google_compute_instance.dify.network_interface[0].access_config[0].nat_ip : null
}

output "service_account_email" {
  description = "Email of the VM service account"
  value       = google_service_account.dify_vm.email
}

output "zone" {
  description = "Zone of the instance"
  value       = google_compute_instance.dify.zone
}