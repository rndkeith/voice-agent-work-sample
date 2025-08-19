# Voice AI Assistant - Production Deployment Configuration
# Demonstrates cloud-native deployment strategy for healthcare AI systems

# Stage 1: Build Environment
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# Copy csproj and restore dependencies
COPY src/*.csproj ./
RUN dotnet restore

# Copy source code and build application
COPY src/ ./
RUN dotnet publish -c Release -o out --no-restore

# Stage 2: Runtime Environment  
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Create non-root user for security
RUN groupadd -r voiceagent && useradd -r -g voiceagent voiceagent

# Install required packages for healthcare compliance
RUN apt-get update && apt-get install -y \
    curl \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Set working directory and copy application
WORKDIR /app
COPY --from=build-env /app/out .

# Create directories for logs and data with proper permissions
RUN mkdir -p /app/logs /app/data \
    && chown -R voiceagent:voiceagent /app

# Switch to non-root user
USER voiceagent

# Health check configuration
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8080/healthz || exit 1

# Environment configuration
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_EnableDiagnostics=0
ENV DOTNET_USE_POLLING_FILE_WATCHER=true

# Security hardening
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
ENV ASPNETCORE_PATHBASE=""

# Observability configuration
ENV OTEL_SERVICE_NAME=voice-agent
ENV OTEL_SERVICE_VERSION=1.0.0
ENV OTEL_RESOURCE_ATTRIBUTES="service.name=voice-agent,service.environment=production"

# Expose port
EXPOSE 8080

# Entry point
ENTRYPOINT ["dotnet", "VoiceAgent.dll"]

# Metadata labels for container registry
LABEL org.opencontainers.image.title="Voice AI Assistant"
LABEL org.opencontainers.image.description="Healthcare voice AI assistant for appointment booking"
LABEL org.opencontainers.image.vendor="Healthcare AI Solutions"
LABEL org.opencontainers.image.licenses="MIT"
LABEL org.opencontainers.image.documentation="https://github.com/company/voice-agent-work-sample"
LABEL org.opencontainers.image.source="https://github.com/company/voice-agent-work-sample"

# Security scanning metadata
LABEL security.scan.enabled="true"
LABEL security.compliance.hipaa="true"
LABEL security.vulnerability.scanner="trivy"

# Build information (populated by CI/CD)
ARG BUILD_VERSION=dev
ARG BUILD_COMMIT=unknown
ARG BUILD_DATE=unknown

LABEL org.opencontainers.image.version="${BUILD_VERSION}"
LABEL org.opencontainers.image.revision="${BUILD_COMMIT}"
LABEL org.opencontainers.image.created="${BUILD_DATE}"

# Multi-stage build optimization
# - Reduces final image size by excluding build tools
# - Improves security by minimizing attack surface
# - Optimizes for container registry layer caching