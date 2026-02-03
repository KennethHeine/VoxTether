#!/bin/bash
# VoxTether Backend - Azure Deployment Script
#
# This script deploys the VoxTether backend to Azure Container Apps with GPU support.
#
# Prerequisites:
#   - Azure CLI installed and logged in (az login)
#   - Docker installed
#
# Usage:
#   ./deploy.sh [environment]
#
# Arguments:
#   environment: dev or prod (default: dev)
#
# Examples:
#   ./deploy.sh              # Deploy to dev
#   ./deploy.sh dev          # Deploy to dev
#   ./deploy.sh prod         # Deploy to prod

set -e

# Configuration
ENVIRONMENT="${1:-dev}"
RESOURCE_GROUP="voxtether-${ENVIRONMENT}-rg"
LOCATION="eastus2"  # Change to your preferred region
ACR_NAME="voxtethercr"
IMAGE_NAME="voxtether-backend"
IMAGE_TAG="latest"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}VoxTether Azure Deployment${NC}"
echo -e "${GREEN}Environment: ${ENVIRONMENT}${NC}"
echo -e "${GREEN}========================================${NC}"

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    echo -e "${RED}Error: Azure CLI is not installed.${NC}"
    echo "Please install it from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
fi

# Check if logged in to Azure
if ! az account show &> /dev/null; then
    echo -e "${YELLOW}Not logged in to Azure. Running 'az login'...${NC}"
    az login
fi

# Get subscription info
SUBSCRIPTION=$(az account show --query name -o tsv)
echo -e "${GREEN}Using subscription: ${SUBSCRIPTION}${NC}"

# Step 1: Create Resource Group
echo -e "\n${YELLOW}Step 1: Creating resource group...${NC}"
az group create \
    --name "$RESOURCE_GROUP" \
    --location "$LOCATION" \
    --output none
echo -e "${GREEN}✓ Resource group created: ${RESOURCE_GROUP}${NC}"

# Step 2: Create Azure Container Registry
echo -e "\n${YELLOW}Step 2: Creating Azure Container Registry...${NC}"
if az acr show --name "$ACR_NAME" --resource-group "$RESOURCE_GROUP" &> /dev/null; then
    echo -e "${GREEN}✓ ACR already exists: ${ACR_NAME}${NC}"
else
    az acr create \
        --name "$ACR_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --sku Basic \
        --admin-enabled true \
        --output none
    echo -e "${GREEN}✓ ACR created: ${ACR_NAME}${NC}"
fi

# Step 3: Build and push Docker image
echo -e "\n${YELLOW}Step 3: Building and pushing Docker image...${NC}"
cd "$(dirname "$0")/../src/backend"

# Login to ACR
az acr login --name "$ACR_NAME"

# Build the image
FULL_IMAGE="${ACR_NAME}.azurecr.io/${IMAGE_NAME}:${IMAGE_TAG}"
echo "Building image: ${FULL_IMAGE}"
docker build -t "$FULL_IMAGE" -f Dockerfile.azure .

# Push to ACR
echo "Pushing image to ACR..."
docker push "$FULL_IMAGE"
echo -e "${GREEN}✓ Image pushed: ${FULL_IMAGE}${NC}"

# Step 4: Deploy infrastructure with Bicep
echo -e "\n${YELLOW}Step 4: Deploying infrastructure...${NC}"
cd "$(dirname "$0")"

PARAMS_FILE="parameters.${ENVIRONMENT}.json"
if [ ! -f "$PARAMS_FILE" ]; then
    echo -e "${YELLOW}Warning: ${PARAMS_FILE} not found, using default parameters${NC}"
    PARAMS_FILE=""
fi

if [ -n "$PARAMS_FILE" ]; then
    az deployment group create \
        --resource-group "$RESOURCE_GROUP" \
        --template-file main.bicep \
        --parameters "@${PARAMS_FILE}" \
        --parameters containerImage="$FULL_IMAGE" \
        --output none
else
    az deployment group create \
        --resource-group "$RESOURCE_GROUP" \
        --template-file main.bicep \
        --parameters containerImage="$FULL_IMAGE" \
        --output none
fi

echo -e "${GREEN}✓ Infrastructure deployed${NC}"

# Step 5: Get deployment outputs
echo -e "\n${YELLOW}Step 5: Getting deployment information...${NC}"
FQDN=$(az deployment group show \
    --resource-group "$RESOURCE_GROUP" \
    --name main \
    --query properties.outputs.fqdn.value \
    -o tsv 2>/dev/null || echo "")

if [ -n "$FQDN" ]; then
    echo -e "${GREEN}========================================${NC}"
    echo -e "${GREEN}Deployment Complete!${NC}"
    echo -e "${GREEN}========================================${NC}"
    echo -e "API URL:        https://${FQDN}"
    echo -e "Health Check:   https://${FQDN}/api/health"
    echo -e "API Docs:       https://${FQDN}/docs"
    echo -e "${GREEN}========================================${NC}"
else
    echo -e "${YELLOW}Deployment in progress. Check Azure portal for status.${NC}"
fi

echo -e "\n${GREEN}Done!${NC}"
