# IAP SSH access firewall rule
resource "google_compute_firewall" "iap_ssh" {
  name    = "${var.project_name}-iap-ssh-${var.environment}"
  network = var.network_name
  project = var.project_id

  allow {
    protocol = "tcp"
    ports    = ["22"]
  }

  # Google IAP IP ranges
  source_ranges = ["35.235.240.0/20"]
  target_tags   = ["dify-vm"]
}
