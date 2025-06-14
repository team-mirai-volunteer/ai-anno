# MIG outputs
output "instance_group_manager_id" {
  description = "ID of the managed instance group"
  value       = google_compute_region_instance_group_manager.dify.id
}

output "instance_group_manager_self_link" {
  description = "Self link of the managed instance group"
  value       = google_compute_region_instance_group_manager.dify.self_link
}

output "instance_group_manager_instance_group" {
  description = "Instance group URL of the managed instance group"
  value       = google_compute_region_instance_group_manager.dify.instance_group
}

output "instance_template_self_link" {
  description = "Self link of the instance template"
  value       = google_compute_instance_template.dify.self_link
}

output "service_account_email" {
  description = "Email of the VM service account"
  value       = google_service_account.dify_vm.email
}

# Legacy outputs for compatibility (will be removed in future)
output "instance_name" {
  description = "Base name for instances in the MIG"
  value       = "${var.project_name}-dify-${var.environment}"
}

output "zone" {
  description = "Zone of the instance (deprecated - MIG is regional)"
  value       = var.zone
}