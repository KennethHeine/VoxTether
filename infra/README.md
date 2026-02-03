# VoxTether Azure Infrastructure

This directory contains Infrastructure as Code (IaC) templates and scripts for deploying VoxTether to Azure with GPU support.

## Overview

The infrastructure deploys the VoxTether backend to **Azure Container Apps** with:
- **NVIDIA T4 GPU** for fast transcription
- **Scale-to-zero** capability (no costs when idle)
- **Per-second billing** for GPU usage
- **Automatic scaling** based on HTTP requests

## Files

| File | Description |
|------|-------------|
| `main.bicep` | Main Bicep template for Azure Container Apps deployment |
| `parameters.dev.json` | Parameters for development environment |
| `parameters.prod.json` | Parameters for production environment |
| `deploy.sh` | Automated deployment script |

## Quick Start

### Prerequisites

1. [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) installed
2. [Docker](https://www.docker.com/get-started) installed
3. Azure subscription with GPU quota

### Deploy

```bash
# Login to Azure
az login

# Deploy to development
./deploy.sh dev

# Deploy to production
./deploy.sh prod
```

### Manual Deployment

```bash
# 1. Create resource group
az group create --name voxtether-rg --location eastus2

# 2. Create container registry
az acr create --name voxtethercr --resource-group voxtether-rg --sku Basic

# 3. Build and push image
cd ../src/backend
az acr login --name voxtethercr
docker build -t voxtethercr.azurecr.io/voxtether-backend:latest -f Dockerfile.azure .
docker push voxtethercr.azurecr.io/voxtether-backend:latest

# 4. Deploy infrastructure
cd ../infra
az deployment group create \
  --resource-group voxtether-rg \
  --template-file main.bicep \
  --parameters @parameters.dev.json
```

## Cost Estimates

| Usage Pattern | GPU Hours/Month | Estimated Cost |
|--------------|-----------------|----------------|
| Light (1 hr/day) | 30 | ~$13.50 |
| Medium (4 hrs/day) | 120 | ~$54.00 |
| Heavy (8 hrs/day) | 240 | ~$108.00 |

**Note:** With scale-to-zero, you only pay when actively transcribing. Idle time costs $0.

## Configuration

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `VOXTETHER_DEVICE` | cuda | Device to use (cuda/cpu) |
| `VOXTETHER_COMPUTE_TYPE` | float16 | Compute precision |
| `VOXTETHER_DEFAULT_MODEL` | small | Whisper model size |
| `VOXTETHER_PRELOAD_MODEL` | true | Preload model on startup |

### Scaling

The deployment is configured with:
- **Min replicas:** 0 (scale-to-zero)
- **Max replicas:** 3 (configurable)
- **Scale trigger:** 5 concurrent HTTP requests

## Troubleshooting

### GPU Quota

If you get quota errors, request GPU quota increase:
```bash
az quota update --resource-name "standardNCAST4v3Family" \
  --scope "/subscriptions/{subscription-id}/providers/Microsoft.Compute/locations/eastus2" \
  --limit-object value=4
```

### Check Logs

```bash
az containerapp logs show \
  --name voxtether-backend \
  --resource-group voxtether-rg \
  --follow
```

### Check Status

```bash
az containerapp show \
  --name voxtether-backend \
  --resource-group voxtether-rg \
  --query properties.runningStatus
```

## See Also

- [Azure GPU Deployment Guide](../docs/AZURE-GPU-DEPLOYMENT.md) - Comprehensive deployment documentation
- [Backend Setup](../docs/BACKEND-SETUP.md) - Local backend setup
- [Azure Container Apps GPU Docs](https://learn.microsoft.com/en-us/azure/container-apps/gpu-serverless-overview)
