#!/bin/bash

# Traffic Shifting Script for Blue-Green Deployments
# Coordinates with nginx ingress controller for gradual traffic migration
# 
# Usage: ./traffic-shift.sh --target-version v1.2.0 --percentage 25 --environment production
# 
# Note: This script demonstrates production deployment patterns for healthcare AI systems.
# Actual implementation would integrate with your specific infrastructure and monitoring systems.

set -euo pipefail

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
NAMESPACE="voice-agent"
SERVICE_NAME="voice-agent-service"
INGRESS_NAME="voice-agent-ingress"

# Default values
TARGET_VERSION=""
TRAFFIC_PERCENTAGE=""
ENVIRONMENT="staging"
DRY_RUN=false
ROLLBACK=false
MONITOR_DURATION=300  # 5 minutes
SLO_THRESHOLD_ERROR_RATE=0.05
SLO_THRESHOLD_LATENCY_P95=700

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Logging functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Help function
show_help() {
    cat << EOF
Traffic Shifting Script for Blue-Green Deployments

USAGE:
    $0 [OPTIONS]

OPTIONS:
    -v, --target-version VERSION    Target version to deploy (required)
    -p, --percentage PERCENT        Traffic percentage to shift (0-100)
    -e, --environment ENV           Environment (staging, production)
    -d, --dry-run                   Show what would be done without executing
    -r, --rollback                  Rollback to previous version
    -m, --monitor-duration SEC      Monitoring duration in seconds (default: 300)
    -h, --help                      Show this help message

EXAMPLES:
    # Shift 25% of traffic to new version
    $0 --target-version v1.2.0 --percentage 25 --environment production
    
    # Dry run to see what would happen
    $0 --target-version v1.2.0 --percentage 50 --dry-run
    
    # Rollback current deployment
    $0 --rollback --environment production
    
    # Complete cutover (100% traffic)
    $0 --target-version v1.2.0 --percentage 100 --environment production

TRAFFIC SHIFTING METHODS:
    1. Nginx Ingress Controller (annotations-based)
    2. Kubernetes Service Mesh (Istio/Linkerd)
    3. External Load Balancer (AWS ALB, GCP LB)
    4. DNS-based shifting (Route53, CloudFlare)

MONITORING:
    - Real-time SLO monitoring during traffic shift
    - Automatic rollback on SLO breach
    - Business metrics validation (containment rate, etc.)
    - Error rate and latency tracking

For more information, see: docs/deployment-strategies.md
EOF
}

# Parse command line arguments
parse_arguments() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            -v|--target-version)
                TARGET_VERSION="$2"
                shift 2
                ;;
            -p|--percentage)
                TRAFFIC_PERCENTAGE="$2"
                shift 2
                ;;
            -e|--environment)
                ENVIRONMENT="$2"
                shift 2
                ;;
            -d|--dry-run)
                DRY_RUN=true
                shift
                ;;
            -r|--rollback)
                ROLLBACK=true
                shift
                ;;
            -m|--monitor-duration)
                MONITOR_DURATION="$2"
                shift 2
                ;;
            -h|--help)
                show_help
                exit 0
                ;;
            *)
                log_error "Unknown option: $1"
                show_help
                exit 1
                ;;
        esac
    done
}

# Validate inputs
validate_inputs() {
    if [[ "$ROLLBACK" == "false" ]]; then
        if [[ -z "$TARGET_VERSION" ]]; then
            log_error "Target version is required for deployment"
            exit 1
        fi
        
        if [[ -z "$TRAFFIC_PERCENTAGE" ]]; then
            log_error "Traffic percentage is required"
            exit 1
        fi
        
        if [[ "$TRAFFIC_PERCENTAGE" -lt 0 || "$TRAFFIC_PERCENTAGE" -gt 100 ]]; then
            log_error "Traffic percentage must be between 0 and 100"
            exit 1
        fi
    fi
    
    # Validate environment
    if [[ ! "$ENVIRONMENT" =~ ^(staging|production)$ ]]; then
        log_error "Environment must be 'staging' or 'production'"
        exit 1
    fi
    
    # Validate kubectl access
    if ! kubectl get namespace "$NAMESPACE" &>/dev/null; then
        log_error "Cannot access Kubernetes namespace: $NAMESPACE"
        exit 1
    fi
}

# Get current deployment status
get_current_status() {
    log_info "Getting current deployment status..."
    
    # Get current service endpoints
    local current_endpoints=$(kubectl get endpoints "$SERVICE_NAME" -n "$NAMESPACE" -o json)
    local current_ingress=$(kubectl get ingress "$INGRESS_NAME" -n "$NAMESPACE" -o json)
    
    # Extract current version information
    local current_version=$(kubectl get deployment voice-agent -n "$NAMESPACE" -o jsonpath='{.metadata.labels.version}' 2>/dev/null || echo "unknown")
    
    log_info "Current active version: $current_version"
    
    # Check if blue-green deployments are already in progress
    local blue_deployment=$(kubectl get deployment voice-agent-blue -n "$NAMESPACE" 2>/dev/null || echo "not found")
    local green_deployment=$(kubectl get deployment voice-agent-green -n "$NAMESPACE" 2>/dev/null || echo "not found")
    
    if [[ "$blue_deployment" != "not found" && "$green_deployment" != "not found" ]]; then
        log_warning "Blue-green deployments detected. Current traffic split in progress."
    fi
}

# Deploy new version to inactive slot
deploy_new_version() {
    local version="$1"
    log_info "Deploying version $version to inactive slot..."
    
    # Determine active/inactive slots
    local active_slot=$(get_active_slot)
    local inactive_slot=$(get_inactive_slot "$active_slot")
    
    log_info "Active slot: $active_slot, Deploying to: $inactive_slot"
    
    # Update deployment manifest with new version
    local deployment_manifest="deployment/kubernetes.yaml"
    local temp_manifest="/tmp/voice-agent-${inactive_slot}.yaml"
    
    # Create deployment for inactive slot
    sed "s/name: voice-agent$/name: voice-agent-${inactive_slot}/" "$deployment_manifest" | \
    sed "s/app.kubernetes.io\/name: voice-agent$/app.kubernetes.io\/name: voice-agent-${inactive_slot}/" | \
    sed "s/image: voice-agent:.*$/image: voice-agent:${version}/" > "$temp_manifest"
    
    if [[ "$DRY_RUN" == "true" ]]; then
        log_info "DRY RUN: Would deploy to $inactive_slot slot with version $version"
        return
    fi
    
    # Apply the deployment
    kubectl apply -f "$temp_manifest"
    
    # Wait for deployment to be ready
    log_info "Waiting for $inactive_slot deployment to be ready..."
    kubectl rollout status deployment/voice-agent-${inactive_slot} -n "$NAMESPACE" --timeout=600s
    
    # Health check the new deployment
    if ! health_check_deployment "voice-agent-${inactive_slot}"; then
        log_error "Health check failed for new deployment"
        exit 1
    fi
    
    log_success "Version $version deployed successfully to $inactive_slot slot"
}

# Get active deployment slot (blue or green)
get_active_slot() {
    # Check ingress annotations or service selectors to determine active slot
    local current_selector=$(kubectl get service "$SERVICE_NAME" -n "$NAMESPACE" -o jsonpath='{.spec.selector.app\.kubernetes\.io/name}' 2>/dev/null || echo "voice-agent")
    
    if [[ "$current_selector" == "voice-agent-blue" ]]; then
        echo "blue"
    elif [[ "$current_selector" == "voice-agent-green" ]]; then
        echo "green"
    else
        echo "blue"  # Default to blue if no blue-green setup exists
    fi
}

# Get inactive deployment slot
get_inactive_slot() {
    local active_slot="$1"
    if [[ "$active_slot" == "blue" ]]; then
        echo "green"
    else
        echo "blue"
    fi
}

# Health check deployment
health_check_deployment() {
    local deployment_name="$1"
    log_info "Performing health check on $deployment_name..."
    
    # Check deployment status
    local ready_replicas=$(kubectl get deployment "$deployment_name" -n "$NAMESPACE" -o jsonpath='{.status.readyReplicas}' 2>/dev/null || echo "0")
    local desired_replicas=$(kubectl get deployment "$deployment_name" -n "$NAMESPACE" -o jsonpath='{.spec.replicas}' 2>/dev/null || echo "0")
    
    if [[ "$ready_replicas" != "$desired_replicas" ]]; then
        log_error "Deployment not ready: $ready_replicas/$desired_replicas replicas"
        return 1
    fi
    
    # HTTP health check
    local service_ip=$(kubectl get service "${deployment_name}" -n "$NAMESPACE" -o jsonpath='{.spec.clusterIP}' 2>/dev/null || echo "")
    if [[ -n "$service_ip" ]]; then
        if ! kubectl run health-check-$$-$(date +%s) --rm -i --restart=Never --image=curlimages/curl -- \
            curl -f "http://${service_ip}/healthz" --max-time 10; then
            log_error "HTTP health check failed"
            return 1
        fi
    fi
    
    log_success "Health check passed for $deployment_name"
    return 0
}

# Shift traffic using nginx ingress annotations
shift_traffic_nginx_ingress() {
    local percentage="$1"
    local target_slot="$2"
    
    log_info "Shifting $percentage% traffic to $target_slot slot using nginx ingress..."
    
    if [[ "$DRY_RUN" == "true" ]]; then
        log_info "DRY RUN: Would shift $percentage% traffic to $target_slot"
        return
    fi
    
    # Update ingress with canary annotations
    kubectl annotate ingress "$INGRESS_NAME" -n "$NAMESPACE" \
        nginx.ingress.kubernetes.io/canary="true" \
        nginx.ingress.kubernetes.io/canary-weight="$percentage" \
        nginx.ingress.kubernetes.io/canary-by-header="X-Canary" \
        nginx.ingress.kubernetes.io/canary-by-header-value="$target_slot" \
        --overwrite
    
    # Create canary ingress for target slot
    kubectl get ingress "$INGRESS_NAME" -n "$NAMESPACE" -o yaml | \
    sed "s/name: $INGRESS_NAME$/name: ${INGRESS_NAME}-canary/" | \
    sed "s/serviceName: $SERVICE_NAME$/serviceName: voice-agent-${target_slot}/" | \
    kubectl apply -f -
    
    log_success "Traffic shifted: $percentage% → $target_slot, $((100-percentage))% → $(get_active_slot)"
}

# Shift traffic using service selector (simpler method)
shift_traffic_service_selector() {
    local percentage="$1"
    local target_slot="$2"
    
    log_info "Shifting traffic using service selector method..."
    
    if [[ "$percentage" -eq 100 ]]; then
        # Complete cutover
        if [[ "$DRY_RUN" == "true" ]]; then
            log_info "DRY RUN: Would perform complete cutover to $target_slot"
            return
        fi
        
        kubectl patch service "$SERVICE_NAME" -n "$NAMESPACE" -p \
            "{\"spec\":{\"selector\":{\"app.kubernetes.io/name\":\"voice-agent-${target_slot}\"}}}"
        
        log_success "Complete cutover to $target_slot completed"
    else
        # Partial traffic shift (requires multiple services or mesh)
        log_warning "Partial traffic shifting with service selector requires service mesh or multiple services"
        log_info "Consider using nginx ingress method for percentage-based routing"
    fi
}

# Monitor SLOs during traffic shift
monitor_slos() {
    local duration="$1"
    log_info "Monitoring SLOs for $duration seconds..."
    
    local start_time=$(date +%s)
    local end_time=$((start_time + duration))
    
    while [[ $(date +%s) -lt $end_time ]]; do
        # Check error rate
        local error_rate=$(get_error_rate)
        if (( $(echo "$error_rate > $SLO_THRESHOLD_ERROR_RATE" | bc -l) )); then
            log_error "Error rate ($error_rate) exceeds threshold ($SLO_THRESHOLD_ERROR_RATE)"
            return 1
        fi
        
        # Check latency
        local latency_p95=$(get_latency_p95)
        if [[ "$latency_p95" -gt "$SLO_THRESHOLD_LATENCY_P95" ]]; then
            log_error "P95 latency (${latency_p95}ms) exceeds threshold (${SLO_THRESHOLD_LATENCY_P95}ms)"
            return 1
        fi
        
        # Check business metrics (containment rate, etc.)
        local containment_rate=$(get_containment_rate)
        if (( $(echo "$containment_rate < 0.80" | bc -l) )); then
            log_error "Containment rate ($containment_rate) below minimum threshold (0.80)"
            return 1
        fi
        
        log_info "SLOs healthy - Error rate: $error_rate, P95 latency: ${latency_p95}ms, Containment: $containment_rate"
        sleep 30
    done
    
    log_success "SLO monitoring completed successfully"
    return 0
}

# Get current error rate (placeholder - would integrate with actual monitoring)
get_error_rate() {
    # Implementation would query Prometheus/Grafana/CloudWatch
    # For demonstration, return a mock value
    echo "0.02"
}

# Get current P95 latency (placeholder)
get_latency_p95() {
    # Implementation would query monitoring system
    echo "450"
}

# Get current containment rate (placeholder)
get_containment_rate() {
    # Implementation would query business metrics
    echo "0.87"
}

# Rollback deployment
rollback_deployment() {
    log_warning "Initiating rollback procedure..."
    
    local active_slot=$(get_active_slot)
    local previous_slot=$(get_inactive_slot "$active_slot")
    
    if [[ "$DRY_RUN" == "true" ]]; then
        log_info "DRY RUN: Would rollback from $active_slot to $previous_slot"
        return
    fi
    
    # Immediate traffic cutover to previous version
    shift_traffic_service_selector 100 "$previous_slot"
    
    # Remove canary ingress if exists
    kubectl delete ingress "${INGRESS_NAME}-canary" -n "$NAMESPACE" 2>/dev/null || true
    
    # Remove canary annotations
    kubectl annotate ingress "$INGRESS_NAME" -n "$NAMESPACE" \
        nginx.ingress.kubernetes.io/canary- \
        nginx.ingress.kubernetes.io/canary-weight- \
        nginx.ingress.kubernetes.io/canary-by-header- \
        nginx.ingress.kubernetes.io/canary-by-header-value- 2>/dev/null || true
    
    log_success "Rollback completed - traffic restored to $previous_slot"
}

# Main execution function
main() {
    local start_time=$(date +%s)
    
    echo "Voice AI Assistant - Traffic Shifting"
    echo "   Production-grade blue-green deployment management"
    echo
    
    # Parse arguments and validate
    parse_arguments "$@"
    validate_inputs
    
    # Show current status
    get_current_status
    
    if [[ "$ROLLBACK" == "true" ]]; then
        rollback_deployment
        exit 0
    fi
    
    echo
    log_info "Starting traffic shift to version $TARGET_VERSION"
    log_info "Target: $TRAFFIC_PERCENTAGE% traffic shift"
    log_info "Environment: $ENVIRONMENT"
    
    # Deploy new version if not already deployed
    deploy_new_version "$TARGET_VERSION"
    
    # Shift traffic
    local target_slot=$(get_inactive_slot "$(get_active_slot)")
    
    # Choose traffic shifting method based on percentage
    if [[ "$TRAFFIC_PERCENTAGE" -eq 100 ]]; then
        shift_traffic_service_selector "$TRAFFIC_PERCENTAGE" "$target_slot"
    else
        shift_traffic_nginx_ingress "$TRAFFIC_PERCENTAGE" "$target_slot"
    fi
    
    # Monitor SLOs
    if ! monitor_slos "$MONITOR_DURATION"; then
        log_error "SLO breach detected - initiating automatic rollback"
        rollback_deployment
        exit 1
    fi
    
    local end_time=$(date +%s)
    local duration=$((end_time - start_time))
    
    echo
    log_success "Traffic shift completed successfully!"
    log_info "   Version: $TARGET_VERSION"
    log_info "   Traffic: $TRAFFIC_PERCENTAGE% shifted"
    log_info "   Duration: ${duration}s"
    log_info "   SLOs: All within acceptable ranges"
    
    # Display next steps
    echo
    log_info "NEXT STEPS"
    if [[ "$TRAFFIC_PERCENTAGE" -lt 100 ]]; then
    echo "   - Monitor business metrics for extended period"
    echo "   - Consider increasing traffic percentage if metrics look good"
    echo "   - Run: $0 --target-version $TARGET_VERSION --percentage 100 --environment $ENVIRONMENT"
    else
    echo "   - Complete cutover achieved - monitor for 24 hours"
    echo "   - Clean up old deployment when confident"
    echo "   - Update monitoring dashboards and alerts"
    fi
    echo
}

# Execute main function with all arguments
main "$@"
