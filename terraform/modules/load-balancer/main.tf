# Google Cloud HTTP(S) Load Balancer Module

locals {
  lb_name = "${var.project_name}-lb-${var.environment}"
}

# Reserve a global static IP address for the load balancer
resource "google_compute_global_address" "lb_ip" {
  name    = "${local.lb_name}-ip"
  project = var.project_id
}

# SSL Certificate (managed by Google) - only if domain is provided and SSL is enabled
resource "google_compute_managed_ssl_certificate" "ssl_cert" {
  count   = var.enable_ssl && var.domain_name != "" ? 1 : 0
  name    = "${local.lb_name}-ssl-cert"
  project = var.project_id

  managed {
    domains = [var.domain_name]
  }

  lifecycle {
    create_before_destroy = true
  }
}

# Health check for the backend service
resource "google_compute_health_check" "health_check" {
  name                = "${local.lb_name}-health-check"
  project             = var.project_id
  check_interval_sec  = 30    # 開発中のため間隔を延長
  timeout_sec         = 20    # タイムアウトを延長
  healthy_threshold   = 1     # すぐに健全と判定
  unhealthy_threshold = 10    # 不健全判定を遅らせる

  # HTTPヘルスチェックを使用
  http_health_check {
    port         = var.health_check_port
    request_path = var.health_check_path
  }
}

# Backend service configuration
resource "google_compute_backend_service" "backend" {
  name                  = "${local.lb_name}-backend"
  project               = var.project_id
  protocol              = "HTTP"
  port_name             = "http"
  timeout_sec           = var.backend_timeout_sec
  health_checks         = [google_compute_health_check.health_check.id]
  load_balancing_scheme = "EXTERNAL"
  session_affinity      = var.session_affinity

  backend {
    group           = var.instance_group_url
    balancing_mode  = "UTILIZATION"
    capacity_scaler = 1.0
    max_utilization = 0.8
  }

  # Enable CDN if requested
  dynamic "cdn_policy" {
    for_each = var.enable_cdn ? [1] : []
    content {
      cache_mode = "CACHE_ALL_STATIC"
      default_ttl = 3600
      max_ttl     = 86400
      
      cache_key_policy {
        include_host         = true
        include_protocol     = true
        include_query_string = false
      }
    }
  }

  log_config {
    enable      = true
    sample_rate = 1.0
  }
}

# URL map for routing
resource "google_compute_url_map" "url_map" {
  name            = "${local.lb_name}-url-map"
  project         = var.project_id
  default_service = google_compute_backend_service.backend.id
}

# HTTPS proxy - only if SSL is enabled
resource "google_compute_target_https_proxy" "https_proxy" {
  count            = var.enable_ssl ? 1 : 0
  name             = "${local.lb_name}-https-proxy"
  project          = var.project_id
  url_map          = google_compute_url_map.url_map.id
  ssl_certificates = var.domain_name != "" ? [google_compute_managed_ssl_certificate.ssl_cert[0].id] : []
}

# HTTP proxy
resource "google_compute_target_http_proxy" "http_proxy" {
  name    = "${local.lb_name}-http-proxy"
  project = var.project_id
  url_map = var.enable_ssl ? google_compute_url_map.http_redirect[0].id : google_compute_url_map.url_map.id
}

# URL map for HTTP to HTTPS redirect - only if SSL is enabled
resource "google_compute_url_map" "http_redirect" {
  count   = var.enable_ssl ? 1 : 0
  name    = "${local.lb_name}-http-redirect"
  project = var.project_id

  default_url_redirect {
    strip_query            = false
    https_redirect         = true
    redirect_response_code = "MOVED_PERMANENTLY_DEFAULT"
  }
}

# Global forwarding rule for HTTPS - only if SSL is enabled
resource "google_compute_global_forwarding_rule" "https_forwarding_rule" {
  count                 = var.enable_ssl ? 1 : 0
  name                  = "${local.lb_name}-https-forwarding-rule"
  project               = var.project_id
  ip_protocol           = "TCP"
  load_balancing_scheme = "EXTERNAL"
  port_range            = "443"
  target                = google_compute_target_https_proxy.https_proxy[0].id
  ip_address            = google_compute_global_address.lb_ip.id
}

# Global forwarding rule for HTTP
resource "google_compute_global_forwarding_rule" "http_forwarding_rule" {
  name                  = "${local.lb_name}-http-forwarding-rule"
  project               = var.project_id
  ip_protocol           = "TCP"
  load_balancing_scheme = "EXTERNAL"
  port_range            = "80"
  target                = google_compute_target_http_proxy.http_proxy.id
  ip_address            = google_compute_global_address.lb_ip.id
}

# Firewall rule to allow health checks from Google Cloud
resource "google_compute_firewall" "allow_health_checks" {
  name    = "${local.lb_name}-allow-health-checks"
  project = var.project_id
  network = var.network_name

  allow {
    protocol = "tcp"
    ports    = [tostring(var.health_check_port)]
  }

  source_ranges = [
    "35.191.0.0/16",  # Google Cloud health check source IPs
    "130.211.0.0/22"  # Google Cloud health check source IPs
  ]

  target_tags = ["dify-vm", "allow-health-check"]
}
