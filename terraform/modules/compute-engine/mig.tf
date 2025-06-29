# Managed Instance Group resources for Dify VM deployment

# Instance template
resource "google_compute_instance_template" "dify" {
  name_prefix = "${var.project_name}-dify-${var.environment}-"
  description = "Instance template for Dify VM"
  project     = var.project_id

  tags = [
    "dify-vm",
    var.environment,
    "allow-ssh",
    "allow-http",
    "allow-https",
    "allow-health-check"
  ]

  machine_type = var.machine_type

  disk {
    source_image = var.boot_disk_image
    disk_size_gb = var.boot_disk_size
    disk_type    = var.boot_disk_type
    boot         = true
    auto_delete  = true
  }

  network_interface {
    network    = var.network_id
    subnetwork = var.subnet_id

    # Assign external IP for initial setup
    access_config {
      // Ephemeral public IP
    }
  }

  service_account {
    email  = google_service_account.dify_vm.email
    scopes = ["cloud-platform"]
  }

  metadata = {
    startup-script-url = "gs://${google_storage_bucket.vm_scripts.name}/setup-dify.sh"
    # シャットダウン時のグレースフルな終了処理
    shutdown-script = <<-EOT
      #!/bin/bash
      # Difyコンテナの停止（グレースフルシャットダウン）
      cd /opt/dify && docker compose down --timeout 60
      
      # 残っているコネクションの完了を待つ
      sleep 5
    EOT
  }

  labels = {
    environment = var.environment
    project     = var.project_name
    service     = "dify"
    managed_by  = "terraform"
  }

  lifecycle {
    create_before_destroy = true
  }
}

# Health check for MIG（起動時間を考慮した設定）
resource "google_compute_health_check" "dify_mig" {
  name                = "${var.project_name}-dify-mig-health-${var.environment}"
  check_interval_sec  = 30  # チェック間隔を30秒に延長
  timeout_sec         = 10  # タイムアウトを10秒に延長
  unhealthy_threshold = 3
  healthy_threshold   = 2
  project             = var.project_id

  http_health_check {
    port         = 80
    request_path = "/"
    response     = ""
  }
}

# Regional Managed Instance Group with size=1
resource "google_compute_region_instance_group_manager" "dify" {
  name               = "${var.project_name}-dify-mig-${var.environment}"
  base_instance_name = "${var.project_name}-dify-${var.environment}"
  region             = var.region
  project            = var.project_id

  version {
    instance_template = google_compute_instance_template.dify.self_link
  }

  target_size = 1

  named_port {
    name = "http"
    port = 80
  }

  named_port {
    name = "https"
    port = 443
  }

  # 自動復旧ポリシー（アプリケーション起動時間を考慮）
  auto_healing_policies {
    health_check      = google_compute_health_check.dify_mig.self_link
    initial_delay_sec = 300 # アプリケーション起動に5分の猶予を設定
  }

  update_policy {
    type                           = "PROACTIVE"
    minimal_action                 = "REPLACE"
    most_disruptive_allowed_action = "REPLACE"
    max_surge_fixed                = 3 # リージョナルMIGでは最低でもゾーン数が必要（通常3ゾーン）
    max_unavailable_fixed          = 0 # ダウンタイムなし
    replacement_method             = "SUBSTITUTE"
  }

  wait_for_instances = true
  wait_for_instances_status = "STABLE"

  lifecycle {
    create_before_destroy = true
  }
}

# Firewall rule for health checks
resource "google_compute_firewall" "dify_allow_health_check" {
  name    = "${var.project_name}-dify-allow-health-check-${var.environment}"
  network = var.network_name
  project = var.project_id

  allow {
    protocol = "tcp"
    ports    = ["80", "443"]
  }

  # Google Cloud health check ranges
  source_ranges = ["35.191.0.0/16", "130.211.0.0/22"]
  target_tags   = ["allow-health-check"]
}
