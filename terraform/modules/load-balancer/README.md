# Google Cloud Load Balancer Module

This module creates a Google Cloud HTTP(S) Load Balancer for the AI Anno application.

## Features

- Global HTTP(S) Load Balancer with static IP
- Optional SSL/TLS support with Google-managed certificates
- Optional Cloud CDN for static content caching
- Health checks for backend instances
- HTTP to HTTPS redirect (when SSL is enabled)
- Session affinity configuration
- Automatic firewall rules for health checks

## Usage

### Basic HTTP Load Balancer

```hcl
module "load_balancer" {
  source = "../../modules/load-balancer"

  project_id    = var.project_id
  project_name  = var.project_name
  environment   = "staging"
  region        = "asia-northeast1"
  zone          = "asia-northeast1-a"
  instance_name = "ai-anno-dify-staging"
  network_name  = "ai-anno-vpc-staging"
}
```

### HTTPS Load Balancer with SSL Certificate

```hcl
module "load_balancer" {
  source = "../../modules/load-balancer"

  project_id    = var.project_id
  project_name  = var.project_name
  environment   = "production"
  region        = "asia-northeast1"
  zone          = "asia-northeast1-a"
  instance_name = "ai-anno-dify-production"
  network_name  = "ai-anno-vpc-production"

  # Enable SSL with domain
  enable_ssl  = true
  domain_name = "ai-anno.example.com"
  
  # Optional: Enable CDN
  enable_cdn = true
  
  # Custom health check
  health_check_path = "/health"
  health_check_port = 80
}
```

## Inputs

| Name | Description | Type | Default | Required |
|------|-------------|------|---------|----------|
| project_id | The GCP project ID | string | - | yes |
| project_name | The project name to use as prefix | string | - | yes |
| environment | Environment name (e.g., staging, production) | string | - | yes |
| region | The GCP region for resources | string | - | yes |
| zone | The GCP zone for the instance group | string | - | yes |
| instance_name | Name of the Compute Engine instance to add to the load balancer | string | - | yes |
| network_name | Name of the VPC network | string | - | yes |
| domain_name | Domain name for SSL certificate (optional) | string | "" | no |
| health_check_path | Path for health check endpoint | string | "/" | no |
| health_check_port | Port for health check | number | 80 | no |
| enable_ssl | Enable SSL certificate and HTTPS | bool | false | no |
| enable_cdn | Enable Cloud CDN for static content caching | bool | false | no |
| backend_timeout_sec | Timeout for backend requests in seconds | number | 30 | no |
| session_affinity | Session affinity type | string | "NONE" | no |

## Outputs

| Name | Description |
|------|-------------|
| load_balancer_ip | The IP address of the load balancer |
| load_balancer_ip_address_name | The name of the IP address resource |
| load_balancer_url | The URL to access the application through the load balancer |
| backend_service_id | The ID of the backend service |
| backend_service_name | The name of the backend service |
| instance_group_id | The ID of the instance group |
| health_check_id | The ID of the health check |
| ssl_certificate_id | The ID of the SSL certificate (if SSL is enabled) |
| ssl_certificate_name | The name of the SSL certificate (if SSL is enabled) |

## DNS Configuration

After creating the load balancer, you need to configure your DNS:

1. Get the load balancer IP address from the output
2. Create an A record pointing your domain to this IP
3. Wait for DNS propagation (can take up to 48 hours)
4. The SSL certificate will be automatically provisioned once DNS is configured

## Health Checks

The load balancer uses health checks to determine backend availability. By default, it checks the root path (`/`) on port 80. You can customize this by setting:

- `health_check_path`: The endpoint to check (e.g., `/health`, `/status`)
- `health_check_port`: The port to check (default: 80)

Make sure your application responds with HTTP 200 OK on the health check endpoint.

## Session Affinity

Session affinity ensures that requests from the same client are sent to the same backend instance. Options include:

- `NONE`: No session affinity (default)
- `CLIENT_IP`: Based on client IP address
- `CLIENT_IP_PORT_PROTO`: Based on client IP, port, and protocol
- `CLIENT_IP_PROTO`: Based on client IP and protocol

## Firewall Rules

The module automatically creates firewall rules to allow:
- Health checks from Google Cloud (35.191.0.0/16, 130.211.0.0/22)
- These rules target instances with tags: `dify-vm` or `allow-health-check`

## Important Notes

1. **SSL Certificate**: When using SSL, it may take up to 60 minutes for the certificate to be provisioned after DNS is configured.
2. **Backend Instance**: The instance must have the appropriate network tags (`dify-vm` or `allow-health-check`) for health checks to work.
3. **CDN**: When CDN is enabled, static content will be cached at edge locations. Configure cache headers in your application appropriately.
4. **Costs**: Load balancers incur charges for forwarding rules, data processing, and (if enabled) CDN usage.