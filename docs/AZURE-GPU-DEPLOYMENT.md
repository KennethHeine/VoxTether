# Azure GPU Deployment Guide for VoxTether Backend

This document provides comprehensive guidance on deploying the VoxTether backend to Azure with GPU support, focusing on **lowest cost** options and **easy on/off** capabilities to save money.

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Deployment Options Comparison](#deployment-options-comparison)
3. [Recommended Approach: Azure Container Apps](#recommended-approach-azure-container-apps)
4. [Alternative: Azure Spot VMs](#alternative-azure-spot-vms)
5. [Docker Configuration](#docker-configuration)
6. [Infrastructure as Code](#infrastructure-as-code)
7. [Cost Optimization Strategies](#cost-optimization-strategies)
8. [Regional Pricing Considerations](#regional-pricing-considerations)

---

## Executive Summary

For the VoxTether backend (faster-whisper speech-to-text), we recommend **Azure Container Apps with Serverless GPUs** as the primary deployment option because:

- ✅ **Pay-per-second billing** - Only pay when transcribing
- ✅ **Scale-to-zero** - No costs when idle
- ✅ **No infrastructure management** - Fully managed
- ✅ **NVIDIA T4 GPU** - Perfect for faster-whisper inference at ~$0.45/hour
- ✅ **Automatic scaling** - Handles burst requests

**Estimated costs:**
| Usage Pattern | Monthly Cost Estimate |
|--------------|----------------------|
| 1 hour/day (30 hours/month) | ~$13.50 |
| 4 hours/day (120 hours/month) | ~$54.00 |
| 8 hours/day (240 hours/month) | ~$108.00 |

---

## Deployment Options Comparison

| Option | GPU Type | Pricing Model | Min Cost/Hour | Scale-to-Zero | Ease of Setup |
|--------|----------|---------------|---------------|---------------|---------------|
| **Azure Container Apps** | T4, A100 | Per-second | ~$0.45 (T4) | ✅ Yes | ⭐⭐⭐⭐⭐ |
| **Azure Spot VMs** | T4, V100, A100 | Per-minute | ~$0.16 (T4) | ❌ No (manual) | ⭐⭐⭐ |
| **Azure VMs (on-demand)** | T4, V100, A100 | Per-minute | ~$0.53 (T4) | ❌ No | ⭐⭐⭐ |
| **Azure ML Endpoints** | T4, A100 | Per-second | ~$0.60 (T4) | ✅ Yes | ⭐⭐⭐⭐ |

### GPU Options for faster-whisper

| Model Size | Recommended GPU | VRAM Required | Transcription Speed |
|------------|-----------------|---------------|---------------------|
| tiny/base | T4 (16GB) | ~2GB | ~10x real-time |
| small | T4 (16GB) | ~4GB | ~8x real-time |
| medium | T4 (16GB) | ~8GB | ~5x real-time |
| large-v3 | T4 (16GB) | ~10GB | ~3x real-time |

**Recommendation:** NVIDIA T4 is the best price/performance choice for faster-whisper inference.

---

## Recommended Approach: Azure Container Apps

Azure Container Apps with serverless GPUs is the **optimal choice** for VoxTether because:

1. **True pay-per-use**: Billed per GPU-second (~$0.000125/second for T4)
2. **Automatic scale-to-zero**: No costs when no transcription requests
3. **No VM management**: Fully managed infrastructure
4. **Fast cold starts**: Container ready within seconds

### Pricing Breakdown

| Resource | Unit Price | Notes |
|----------|------------|-------|
| T4 GPU | $0.45/hour | ~$0.000125/second |
| A100 GPU | $4.00/hour | For larger models |
| vCPU | $0.000020/second | After 180k free seconds/month |
| Memory | $0.000002/GiB-second | After 360k free GiB-seconds/month |

### Setup Instructions

#### Step 1: Create Dockerfile

Create `Dockerfile.azure` in your backend directory:

```dockerfile
# See the Docker Configuration section for the complete Dockerfile
```

#### Step 2: Deploy to Azure Container Apps

```bash
# Login to Azure
az login

# Create resource group
az group create --name voxtether-rg --location eastus

# Create Container App Environment
az containerapp env create \
  --name voxtether-env \
  --resource-group voxtether-rg \
  --location eastus

# Build and push image to Azure Container Registry
az acr create --name voxtethercr --resource-group voxtether-rg --sku Basic
az acr login --name voxtethercr
docker build -t voxtethercr.azurecr.io/voxtether-backend:latest -f Dockerfile.azure .
docker push voxtethercr.azurecr.io/voxtether-backend:latest

# Deploy with GPU support
az containerapp create \
  --name voxtether-backend \
  --resource-group voxtether-rg \
  --environment voxtether-env \
  --image voxtethercr.azurecr.io/voxtether-backend:latest \
  --target-port 5678 \
  --ingress external \
  --min-replicas 0 \
  --max-replicas 3 \
  --cpu 4.0 \
  --memory 16.0Gi \
  --workload-profile-name gpu-t4 \
  --scale-rule-name http-rule \
  --scale-rule-type http \
  --scale-rule-http-concurrency 5
```

### Scale-to-Zero Configuration

The `--min-replicas 0` flag enables automatic scale-to-zero:

```yaml
# containerapp.yaml
properties:
  template:
    scale:
      minReplicas: 0
      maxReplicas: 3
      rules:
        - name: http-scale-rule
          http:
            metadata:
              concurrentRequests: '5'
```

When no requests arrive for the cooldown period (default 5 minutes), the container scales to zero and you pay nothing.

---

## Alternative: Azure Spot VMs

If you need more control or predictable usage patterns, Spot VMs offer **up to 90% savings**:

### Spot VM Pricing (NC-series with T4)

| VM Size | vCPUs | GPU | VRAM | On-Demand/hr | Spot/hr | Savings |
|---------|-------|-----|------|--------------|---------|---------|
| NC4as_T4_v3 | 4 | 1x T4 | 16GB | $0.53 | ~$0.16 | 70% |
| NC8as_T4_v3 | 8 | 1x T4 | 16GB | $0.76 | ~$0.23 | 70% |
| NC16as_T4_v3 | 16 | 1x T4 | 16GB | $1.20 | ~$0.36 | 70% |

### Auto Start/Stop with Azure Automation

#### Option 1: Scheduled Start/Stop (Azure Functions)

```bash
# Create Azure Function App
az functionapp create \
  --name voxtether-scheduler \
  --resource-group voxtether-rg \
  --consumption-plan-location eastus \
  --runtime python \
  --runtime-version 3.11 \
  --functions-version 4
```

**Function to Start VM:**

```python
import azure.functions as func
from azure.identity import DefaultAzureCredential
from azure.mgmt.compute import ComputeManagementClient
import os

def main(timer: func.TimerRequest) -> None:
    credential = DefaultAzureCredential()
    subscription_id = os.environ['AZURE_SUBSCRIPTION_ID']
    
    compute_client = ComputeManagementClient(credential, subscription_id)
    
    # Start the VM
    compute_client.virtual_machines.begin_start(
        resource_group_name='voxtether-rg',
        vm_name='voxtether-gpu-vm'
    )
```

**Function to Stop VM:**

```python
import azure.functions as func
from azure.identity import DefaultAzureCredential
from azure.mgmt.compute import ComputeManagementClient
import os

def main(timer: func.TimerRequest) -> None:
    credential = DefaultAzureCredential()
    subscription_id = os.environ['AZURE_SUBSCRIPTION_ID']
    
    compute_client = ComputeManagementClient(credential, subscription_id)
    
    # Deallocate (not just stop) to avoid compute charges
    compute_client.virtual_machines.begin_deallocate(
        resource_group_name='voxtether-rg',
        vm_name='voxtether-gpu-vm'
    )
```

**Schedule Configuration (function.json):**

```json
{
  "bindings": [
    {
      "name": "timer",
      "type": "timerTrigger",
      "direction": "in",
      "schedule": "0 0 8 * * 1-5"  // Start at 8 AM weekdays
    }
  ]
}
```

#### Option 2: On-Demand Start via HTTP Trigger

Create an HTTP-triggered function that starts/stops the VM on demand:

```python
import azure.functions as func
from azure.identity import DefaultAzureCredential
from azure.mgmt.compute import ComputeManagementClient
import json
import os

def main(req: func.HttpRequest) -> func.HttpResponse:
    action = req.params.get('action', 'status')
    
    credential = DefaultAzureCredential()
    subscription_id = os.environ['AZURE_SUBSCRIPTION_ID']
    compute_client = ComputeManagementClient(credential, subscription_id)
    
    rg = 'voxtether-rg'
    vm_name = 'voxtether-gpu-vm'
    
    if action == 'start':
        compute_client.virtual_machines.begin_start(rg, vm_name)
        return func.HttpResponse(json.dumps({"status": "starting"}))
    
    elif action == 'stop':
        compute_client.virtual_machines.begin_deallocate(rg, vm_name)
        return func.HttpResponse(json.dumps({"status": "stopping"}))
    
    else:
        vm = compute_client.virtual_machines.get(rg, vm_name, expand='instanceView')
        status = vm.instance_view.statuses[-1].display_status
        return func.HttpResponse(json.dumps({"status": status}))
```

### Creating a Spot VM

```bash
# Create Spot VM with T4 GPU
az vm create \
  --resource-group voxtether-rg \
  --name voxtether-gpu-vm \
  --image Canonical:0001-com-ubuntu-server-jammy:22_04-lts-gen2:latest \
  --size Standard_NC4as_T4_v3 \
  --priority Spot \
  --max-price 0.20 \
  --eviction-policy Deallocate \
  --admin-username azureuser \
  --generate-ssh-keys \
  --public-ip-sku Standard

# Install NVIDIA drivers and Docker
az vm extension set \
  --resource-group voxtether-rg \
  --vm-name voxtether-gpu-vm \
  --name NvidiaGpuDriverLinux \
  --publisher Microsoft.HpcCompute \
  --version 1.6
```

---

## Docker Configuration

### Production Dockerfile (Dockerfile.azure)

Create this file at `src/backend/Dockerfile.azure`:

```dockerfile
# VoxTether Backend - Azure GPU Deployment
# Optimized for NVIDIA T4 GPU with CUDA 12.x

FROM nvidia/cuda:12.4.1-cudnn-runtime-ubuntu22.04

LABEL maintainer="VoxTether"
LABEL description="VoxTether speech-to-text backend with GPU support"

# Prevent interactive prompts during package installation
ENV DEBIAN_FRONTEND=noninteractive

# Install Python 3.11 and system dependencies
RUN apt-get update && apt-get install -y \
    python3.11 \
    python3.11-venv \
    python3-pip \
    ffmpeg \
    libsndfile1 \
    && rm -rf /var/lib/apt/lists/*

# Create app user for security
RUN useradd -m -u 1000 appuser

# Set working directory
WORKDIR /app

# Copy requirements first for better caching
COPY requirements.txt .

# Install Python dependencies
RUN pip3 install --no-cache-dir -r requirements.txt

# Copy application code
COPY . .

# Create directories for models and logs
RUN mkdir -p /app/models /app/logs && \
    chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Environment variables
ENV VOXTETHER_HOST=0.0.0.0
ENV VOXTETHER_PORT=5678
ENV VOXTETHER_DEVICE=cuda
ENV VOXTETHER_COMPUTE_TYPE=float16
ENV VOXTETHER_MODELS_PATH=/app/models
ENV VOXTETHER_LOGS_PATH=/app/logs
ENV VOXTETHER_PRELOAD_MODEL=true
ENV VOXTETHER_DEFAULT_MODEL=small

# Expose the API port
EXPOSE 5678

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD python3 -c "import urllib.request; urllib.request.urlopen('http://localhost:5678/api/health')" || exit 1

# Start the server
CMD ["python3", "-m", "uvicorn", "main:app", "--host", "0.0.0.0", "--port", "5678"]
```

### Docker Compose for Local Testing

Create `docker-compose.azure.yml`:

```yaml
version: '3.8'

services:
  voxtether-backend:
    build:
      context: ./src/backend
      dockerfile: Dockerfile.azure
    container_name: voxtether-gpu
    ports:
      - "5678:5678"
    environment:
      - VOXTETHER_DEVICE=cuda
      - VOXTETHER_COMPUTE_TYPE=float16
      - VOXTETHER_DEFAULT_MODEL=small
      - VOXTETHER_PRELOAD_MODEL=true
    volumes:
      - voxtether-models:/app/models
      - voxtether-logs:/app/logs
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: 1
              capabilities: [gpu]
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "python3", "-c", "import urllib.request; urllib.request.urlopen('http://localhost:5678/api/health')"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 60s

volumes:
  voxtether-models:
  voxtether-logs:
```

---

## Infrastructure as Code

### Bicep Template for Azure Container Apps

Create `infra/main.bicep`:

```bicep
@description('The location for all resources')
param location string = resourceGroup().location

@description('Container image to deploy')
param containerImage string = 'voxtethercr.azurecr.io/voxtether-backend:latest'

@description('The name of the Container App Environment')
param environmentName string = 'voxtether-env'

@description('The name of the Container App')
param containerAppName string = 'voxtether-backend'

// Container App Environment
resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: environmentName
  location: location
  properties: {
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
      {
        name: 'gpu-t4'
        workloadProfileType: 'NC4as-T4-v3'
        minimumCount: 0
        maximumCount: 3
      }
    ]
  }
}

// Container App
resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  location: location
  properties: {
    managedEnvironmentId: environment.id
    workloadProfileName: 'gpu-t4'
    configuration: {
      ingress: {
        external: true
        targetPort: 5678
        transport: 'http'
      }
    }
    template: {
      containers: [
        {
          name: 'voxtether-backend'
          image: containerImage
          resources: {
            cpu: json('4.0')
            memory: '16Gi'
          }
          env: [
            {
              name: 'VOXTETHER_DEVICE'
              value: 'cuda'
            }
            {
              name: 'VOXTETHER_COMPUTE_TYPE'
              value: 'float16'
            }
            {
              name: 'VOXTETHER_DEFAULT_MODEL'
              value: 'small'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 3
        rules: [
          {
            name: 'http-scaling'
            http: {
              metadata: {
                concurrentRequests: '5'
              }
            }
          }
        ]
      }
    }
  }
}

output fqdn string = containerApp.properties.configuration.ingress.fqdn
```

### Deploy with Bicep

```bash
# Create resource group
az group create --name voxtether-rg --location eastus

# Deploy infrastructure
az deployment group create \
  --resource-group voxtether-rg \
  --template-file infra/main.bicep \
  --parameters containerImage=voxtethercr.azurecr.io/voxtether-backend:latest
```

---

## Cost Optimization Strategies

### 1. Use Scale-to-Zero (Container Apps)

```yaml
scale:
  minReplicas: 0  # Scale to zero when idle
  maxReplicas: 3
```

**Savings:** 100% when not in use

### 2. Choose the Right Model Size

| Model | VRAM | Speed | Use Case |
|-------|------|-------|----------|
| tiny | 2GB | 10x RT | Quick drafts, simple speech |
| small | 4GB | 8x RT | **Best balance** for most use |
| medium | 8GB | 5x RT | High accuracy needs |
| large-v3 | 10GB | 3x RT | Maximum accuracy |

**Recommendation:** Start with `small` model for best cost/accuracy balance.

### 3. Use Spot VMs for Batch Processing

For batch transcription jobs:
- Use Spot VMs at 70-90% discount
- Set max price slightly above spot price
- Implement checkpointing for eviction handling

### 4. Regional Pricing Arbitrage

GPU prices vary by region. Cheapest regions often include:
- East US 2
- South Central US
- West Europe
- Southeast Asia

Check current prices: [Azure Pricing Calculator](https://azure.microsoft.com/pricing/calculator/)

### 5. Reserved Instances (For Consistent Usage)

If you consistently use >8 hours/day:
- 1-year reservation: ~40% savings
- 3-year reservation: ~60% savings

---

## Regional Pricing Considerations

GPU availability and pricing varies by Azure region. Here's a general guide:

### Regions with Best GPU Availability

| Region | T4 Availability | Notes |
|--------|-----------------|-------|
| East US | High | Good for US users |
| East US 2 | High | Often cheaper |
| West US 2 | Medium | Good alternative |
| North Europe | High | Good for EU users |
| West Europe | High | Good for EU users |
| Southeast Asia | Medium | Good for APAC |

### Cost Comparison by Region (Approximate)

| Region | T4 Spot/hr | T4 On-Demand/hr |
|--------|------------|-----------------|
| East US 2 | ~$0.15 | ~$0.53 |
| South Central US | ~$0.16 | ~$0.53 |
| West Europe | ~$0.17 | ~$0.58 |
| Southeast Asia | ~$0.14 | ~$0.50 |

**Note:** Prices change frequently. Always verify with Azure Pricing Calculator.

---

## Quick Start Summary

### For Lowest Cost with Automatic On/Off:

```bash
# 1. Build Docker image
cd src/backend
docker build -t voxtether-backend:gpu -f Dockerfile.azure .

# 2. Create Azure resources
az group create --name voxtether-rg --location eastus2
az containerapp env create --name voxtether-env --resource-group voxtether-rg

# 3. Deploy with scale-to-zero
az containerapp create \
  --name voxtether-backend \
  --resource-group voxtether-rg \
  --environment voxtether-env \
  --image voxtether-backend:gpu \
  --min-replicas 0 \
  --workload-profile-name gpu-t4
```

**Result:** Pay only when transcribing, ~$0.45/hour during active use, $0 when idle.

---

## Additional Resources

- [Azure Container Apps GPU Documentation](https://learn.microsoft.com/en-us/azure/container-apps/gpu-serverless-overview)
- [Azure Spot VMs Guide](https://learn.microsoft.com/en-us/azure/virtual-machines/spot-vms)
- [Azure Pricing Calculator](https://azure.microsoft.com/pricing/calculator/)
- [faster-whisper GitHub](https://github.com/SYSTRAN/faster-whisper)
- [NVIDIA CUDA Docker Hub](https://hub.docker.com/r/nvidia/cuda)

---

## Changelog

| Date | Version | Changes |
|------|---------|---------|
| 2024-02-03 | 1.0 | Initial research and documentation |
