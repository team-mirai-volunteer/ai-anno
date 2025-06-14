# Load Balancer Deployment Guide

This guide walks through deploying the Google Cloud Load Balancer for the AI Anno application.

## Prerequisites

1. Google Cloud Project with billing enabled
2. Terraform installed (version 1.0+)
3. `gcloud` CLI authenticated with appropriate permissions
4. AI Anno application deployed on Compute Engine

## Step 1: Initial Deployment (HTTP-only)

Start with HTTP-only configuration to verify the load balancer works:

1. Configure your `terraform.tfvars`:
```hcl
# Basic configuration
enable_ssl = false
enable_cdn = false
health_check_path = "/"
health_check_port = 80
```

2. Deploy the infrastructure:
```bash
cd terraform/environments/staging
terraform init
terraform plan
terraform apply
```

3. Get the load balancer IP:
```bash
terraform output load_balancer_ip
```

4. Test the load balancer:
```bash
curl http://<LOAD_BALANCER_IP>
```

## Step 2: Enable HTTPS with Custom Domain

1. Update your `terraform.tfvars`:
```hcl
enable_ssl  = true
domain_name = "your-domain.com"
```

2. Apply the changes:
```bash
terraform apply
```

3. Configure DNS:
   - Get the load balancer IP: `terraform output load_balancer_ip`
   - Create an A record in your DNS provider pointing to this IP
   - Wait for DNS propagation (use `nslookup your-domain.com` to verify)

4. Wait for SSL certificate provisioning:
   - This can take up to 60 minutes after DNS is configured
   - Check certificate status in the GCP Console under "Load balancing" > "Certificates"

5. Test HTTPS access:
```bash
curl https://your-domain.com
```

## Step 3: Performance Optimization

### Enable Cloud CDN

For better performance with static assets:

1. Update `terraform.tfvars`:
```hcl
enable_cdn = true
```

2. Apply changes:
```bash
terraform apply
```

3. Configure cache headers in your application for optimal caching.

### Configure Session Affinity

If your application requires sticky sessions:

1. Update `terraform.tfvars`:
```hcl
session_affinity = "CLIENT_IP"  # or "CLIENT_IP_PORT_PROTO"
```

2. Apply changes:
```bash
terraform apply
```

## Health Checks

The load balancer uses health checks to determine backend availability.

### Default Configuration
- Path: `/`
- Port: 80
- Interval: 10 seconds
- Timeout: 5 seconds
- Healthy threshold: 2 consecutive successes
- Unhealthy threshold: 3 consecutive failures

### Custom Health Endpoint

If your application has a dedicated health endpoint:

1. Update `terraform.tfvars`:
```hcl
health_check_path = "/health"  # or "/api/health", etc.
```

2. Ensure your application responds with HTTP 200 OK on this path.

## Troubleshooting

### Health Check Failures

1. Check firewall rules:
```bash
gcloud compute firewall-rules list --filter="name~allow-health-check"
```

2. Verify the instance has correct tags:
```bash
gcloud compute instances describe <instance-name> --zone=<zone> --format="get(tags.items[])"
```

3. Test health check endpoint directly:
```bash
# SSH into the instance
gcloud compute ssh <instance-name> --zone=<zone>

# Test the endpoint
curl http://localhost/health
```

### SSL Certificate Issues

1. Verify DNS configuration:
```bash
nslookup your-domain.com
# Should return the load balancer IP
```

2. Check certificate status:
```bash
gcloud compute ssl-certificates list
gcloud compute ssl-certificates describe <certificate-name>
```

3. Common issues:
   - DNS not pointing to load balancer IP
   - Certificate still provisioning (wait up to 60 minutes)
   - Domain validation failed

### Backend Connection Issues

1. Check backend service status:
```bash
gcloud compute backend-services get-health <backend-service-name> --global
```

2. Verify instance group membership:
```bash
gcloud compute instance-groups list-instances <instance-group-name> --zone=<zone>
```

## Monitoring

### Enable Logging

Logs are automatically enabled for the load balancer. View them in:
- GCP Console > Logging > Logs Explorer
- Filter by resource type: "HTTP(S) Load Balancer"

### Metrics

Monitor load balancer performance in:
- GCP Console > Monitoring > Dashboards
- Key metrics:
  - Request count
  - Error rate
  - Latency
  - Backend health

### Alerts

Set up alerts for:
- High error rates
- Backend unhealthy
- High latency
- SSL certificate expiration

## Cost Optimization

1. **Forwarding Rules**: Each load balancer uses forwarding rules (charged hourly)
2. **Data Processing**: Charged per GB processed
3. **CDN**: Additional charges for cache fills and egress

To reduce costs:
- Use Cloud CDN to reduce backend requests
- Optimize health check intervals
- Consider regional load balancers for single-region deployments

## Security Best Practices

1. **SSL/TLS**: Always use HTTPS in production
2. **Security Policies**: Consider adding Cloud Armor policies
3. **Access Control**: Restrict backend access to load balancer only
4. **DDoS Protection**: Enable Cloud Armor DDoS protection
5. **Monitoring**: Set up alerts for suspicious traffic patterns

## Maintenance

### Certificate Renewal

Google-managed certificates auto-renew 30 days before expiration. No action required.

### Backend Updates

When updating the backend application:
1. The load balancer will automatically detect unhealthy instances
2. Traffic is routed only to healthy instances
3. Use rolling updates to maintain availability

### Scaling

To add more backend instances:
1. Create additional Compute Engine instances
2. Add them to the instance group
3. The load balancer automatically includes them

## Clean Up

To remove the load balancer:

```bash
# Remove from Terraform state
terraform destroy -target=module.load_balancer

# Or remove entirely
terraform destroy
```