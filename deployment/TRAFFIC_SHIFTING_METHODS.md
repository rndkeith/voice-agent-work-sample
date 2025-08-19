# Traffic Shifting Implementation Strategies
_© 2025 Keith Williams · Non-code content CC BY-NC-ND 4.0; code blocks MIT._


# Demonstrates multiple approaches for production blue-green deployments

## Method 1: Nginx Ingress Controller (Kubernetes)

### Ingress with Canary Annotations
```yaml
# Primary ingress (current version)
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: voice-agent-ingress
  namespace: voice-agent
  annotations:
    kubernetes.io/ingress.class: "nginx"
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
spec:
  rules:
  - host: voice-agent.company.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: voice-agent-blue  # Current active version
            port:
              number: 80

---
# Canary ingress (new version)
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: voice-agent-ingress-canary
  namespace: voice-agent
  annotations:
    kubernetes.io/ingress.class: "nginx"
    nginx.ingress.kubernetes.io/canary: "true"
    nginx.ingress.kubernetes.io/canary-weight: "25"  # 25% traffic to new version
    nginx.ingress.kubernetes.io/canary-by-header: "X-Canary"
    nginx.ingress.kubernetes.io/canary-by-header-value: "green"
spec:
  rules:
  - host: voice-agent.company.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: voice-agent-green  # New version
            port:
              number: 80
```

### Progressive Traffic Shifting
```bash
# Script coordinates these percentage shifts:
# 0% → 5% → 25% → 50% → 100%

# Each step updates the canary-weight annotation:
kubectl annotate ingress voice-agent-ingress-canary \
  nginx.ingress.kubernetes.io/canary-weight="50" --overwrite
```

## Method 2: Service Mesh (Istio)

### VirtualService with Traffic Splitting
```yaml
apiVersion: networking.istio.io/v1alpha3
kind: VirtualService
metadata:
  name: voice-agent-traffic-split
spec:
  hosts:
  - voice-agent.company.com
  http:
  - match:
    - headers:
        x-canary:
          exact: "true"
    route:
    - destination:
        host: voice-agent-green
      weight: 100
  - route:
    - destination:
        host: voice-agent-blue
      weight: 75  # 75% to current version
    - destination:
        host: voice-agent-green
      weight: 25  # 25% to new version
```

## Method 3: AWS Application Load Balancer

### ALB Target Groups with Weighted Routing
```yaml
# Kubernetes Service pointing to ALB
apiVersion: v1
kind: Service
metadata:
  name: voice-agent-alb
  annotations:
    service.beta.kubernetes.io/aws-load-balancer-type: "nlb"
    service.beta.kubernetes.io/aws-load-balancer-nlb-target-type: "ip"
    # ALB controller manages weighted routing
    alb.ingress.kubernetes.io/target-group-attributes: |
      load_balancing.algorithm.type=weighted_round_robin
spec:
  type: LoadBalancer
  ports:
  - port: 80
    targetPort: 8080
  selector:
    app: voice-agent
```

```terraform
# Terraform for ALB weighted target groups
resource "aws_lb_target_group" "voice_agent_blue" {
  name     = "voice-agent-blue"
  port     = 80
  protocol = "HTTP"
  vpc_id   = var.vpc_id
  
  health_check {
    path = "/healthz"
    matcher = "200"
  }
}

resource "aws_lb_target_group" "voice_agent_green" {
  name     = "voice-agent-green"
  port     = 80
  protocol = "HTTP"
  vpc_id   = var.vpc_id
  
  health_check {
    path = "/healthz"
    matcher = "200"
  }
}

resource "aws_lb_listener_rule" "traffic_split" {
  listener_arn = aws_lb_listener.main.arn
  
  action {
    type = "forward"
    forward {
      target_group {
        arn    = aws_lb_target_group.voice_agent_blue.arn
        weight = 75  # Managed by traffic-shift.sh via AWS CLI
      }
      target_group {
        arn    = aws_lb_target_group.voice_agent_green.arn
        weight = 25
      }
    }
  }
  
  condition {
    path_pattern {
      values = ["/*"]
    }
  }
}
```

## Method 4: DNS-based Traffic Shifting

### Route53 Weighted Routing
```bash
# Create weighted DNS records
aws route53 change-resource-record-sets --hosted-zone-id Z1234567890 --change-batch '{
  "Changes": [{
    "Action": "UPSERT",
    "ResourceRecordSet": {
      "Name": "voice-agent.company.com",
      "Type": "A",
      "SetIdentifier": "blue-version",
      "Weight": 75,
      "TTL": 60,
      "ResourceRecords": [{"Value": "10.0.1.100"}]
    }
  }, {
    "Action": "UPSERT", 
    "ResourceRecordSet": {
      "Name": "voice-agent.company.com",
      "Type": "A",
      "SetIdentifier": "green-version", 
      "Weight": 25,
      "TTL": 60,
      "ResourceRecords": [{"Value": "10.0.2.100"}]
    }
  }]
}'
```

## Implementation in traffic-shift.sh

The script coordinates with your chosen method:

### Nginx Ingress Controller Integration
```bash
shift_traffic_nginx_ingress() {
    local percentage="$1"
    local target_slot="$2"
    
    # Update canary ingress annotation
    kubectl annotate ingress "$INGRESS_NAME-canary" -n "$NAMESPACE" \
        nginx.ingress.kubernetes.io/canary-weight="$percentage" \
        --overwrite
    
    # Monitor ingress controller updates
    kubectl rollout status deployment/nginx-ingress-controller -n ingress-nginx
}
```

### Service Mesh Integration  
```bash
shift_traffic_istio() {
    local percentage="$1"
    local target_slot="$2"
    
    # Update VirtualService with new weights
    kubectl patch virtualservice voice-agent-traffic-split -n "$NAMESPACE" --type='merge' -p="
    {
      \"spec\": {
        \"http\": [{
          \"route\": [
            {\"destination\": {\"host\": \"voice-agent-blue\"}, \"weight\": $((100-percentage))},
            {\"destination\": {\"host\": \"voice-agent-green\"}, \"weight\": $percentage}
          ]
        }]
      }
    }"
}
```

### AWS ALB Integration
```bash
shift_traffic_alb() {
    local percentage="$1"
    
    # Update ALB listener rule weights
    aws elbv2 modify-rule \
        --rule-arn "$ALB_RULE_ARN" \
        --actions Type=forward,ForwardConfig='{
            TargetGroups=[
                {TargetGroupArn="'$BLUE_TG_ARN'",Weight='$((100-percentage))'},
                {TargetGroupArn="'$GREEN_TG_ARN'",Weight='$percentage'}
            ]
        }'
}
```

## Monitoring Integration

Each method integrates with monitoring for automatic rollback:

```bash
monitor_slos() {
    # Query metrics from your monitoring system
    local error_rate=$(curl -s "http://prometheus:9090/api/v1/query?query=rate(http_requests_total{status=~'5..'}[5m])" | jq -r '.data.result[0].value[1]')
    
    local latency_p95=$(curl -s "http://prometheus:9090/api/v1/query?query=histogram_quantile(0.95,rate(http_request_duration_seconds_bucket[5m]))" | jq -r '.data.result[0].value[1]')
    
    # Convert to milliseconds
    local latency_p95_ms=$(echo "$latency_p95 * 1000" | bc -l)
    
    # Check SLO thresholds
    if (( $(echo "$error_rate > 0.05" | bc -l) )); then
        log_error "Error rate breach: $error_rate > 0.05"
        return 1
    fi
    
    if (( $(echo "$latency_p95_ms > 700" | bc -l) )); then
        log_error "Latency breach: ${latency_p95_ms}ms > 700ms"
        return 1
    fi
    
    return 0
}
```

## Healthcare-Specific Considerations

### PHI Protection During Deployments
```bash
# Ensure no PHI leakage during traffic shifting
validate_phi_protection() {
    # Check that logs are properly redacted in new version
    kubectl logs deployment/voice-agent-green -n "$NAMESPACE" | \
        grep -E '\b\d{3}-\d{2}-\d{4}\b|\b\d{3}-\d{3}-\d{4}\b' && {
        log_error "PHI detected in logs - aborting deployment"
        return 1
    }
    
    # Verify audit trail continuity
    local audit_events=$(kubectl exec deployment/voice-agent-green -n "$NAMESPACE" -- \
        curl -s http://localhost:8080/metrics | grep audit_events_total)
    
    log_info "Audit trail validated: $audit_events"
}
```

### Compliance Monitoring
```bash
# Monitor HIPAA-specific metrics during deployment
monitor_compliance_metrics() {
    # Check PHI access patterns
    local phi_access_rate=$(get_phi_access_rate)
    
    # Verify consent collection rates
    local consent_rate=$(get_consent_collection_rate)
    
    # Monitor data minimization compliance
    local data_minimization_score=$(get_data_minimization_score)
    
    log_info "Compliance metrics - PHI access: $phi_access_rate, Consent: $consent_rate, Data minimization: $data_minimization_score"
}
```

The key insight is that `traffic-shift.sh` acts as the **orchestration layer** that coordinates with whatever load balancing technology you're using, while also monitoring business and compliance metrics specific to healthcare AI systems.
