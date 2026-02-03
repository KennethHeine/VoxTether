// VoxTether Backend - Azure Container Apps Deployment
// 
// This Bicep template deploys the VoxTether backend to Azure Container Apps
// with serverless GPU support (NVIDIA T4) and scale-to-zero configuration.
//
// Deploy:
//   az deployment group create \
//     --resource-group voxtether-rg \
//     --template-file main.bicep \
//     --parameters containerImage=<your-acr>.azurecr.io/voxtether-backend:latest

@description('The location for all resources')
param location string = resourceGroup().location

@description('Container image to deploy (ACR path)')
param containerImage string

@description('The name of the Container App Environment')
param environmentName string = 'voxtether-env'

@description('The name of the Container App')
param containerAppName string = 'voxtether-backend'

@description('Default Whisper model to load')
@allowed([
  'tiny'
  'base'
  'small'
  'medium'
  'large-v3'
])
param defaultModel string = 'small'

@description('Minimum number of replicas (0 enables scale-to-zero)')
@minValue(0)
@maxValue(10)
param minReplicas int = 0

@description('Maximum number of replicas')
@minValue(1)
@maxValue(10)
param maxReplicas int = 3

@description('Azure Container Registry name (without .azurecr.io)')
param acrName string = ''

// Log Analytics Workspace for monitoring
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${environmentName}-logs'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// Container App Environment with GPU workload profile
resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: environmentName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
      {
        // GPU workload profile using NVIDIA T4
        name: 'gpu-t4'
        workloadProfileType: 'NC4as-T4-v3'
        minimumCount: 0
        maximumCount: maxReplicas
      }
    ]
  }
}

// Container App with GPU support and scale-to-zero
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
        allowInsecure: false
        corsPolicy: {
          allowedOrigins: [
            'http://localhost:*'
            'https://localhost:*'
          ]
          allowedMethods: ['GET', 'POST', 'OPTIONS']
          allowedHeaders: ['*']
        }
      }
      registries: acrName != '' ? [
        {
          server: '${acrName}.azurecr.io'
          identity: 'system'
        }
      ] : []
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
              name: 'VOXTETHER_HOST'
              value: '0.0.0.0'
            }
            {
              name: 'VOXTETHER_PORT'
              value: '5678'
            }
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
              value: defaultModel
            }
            {
              name: 'VOXTETHER_PRELOAD_MODEL'
              value: 'true'
            }
            {
              name: 'VOXTETHER_DEBUG'
              value: 'false'
            }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/api/health'
                port: 5678
              }
              initialDelaySeconds: 120
              periodSeconds: 30
              timeoutSeconds: 10
              failureThreshold: 3
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/api/health'
                port: 5678
              }
              initialDelaySeconds: 60
              periodSeconds: 10
              timeoutSeconds: 5
              failureThreshold: 3
            }
          ]
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
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
  identity: {
    type: 'SystemAssigned'
  }
}

// Outputs
@description('The FQDN of the deployed container app')
output fqdn string = containerApp.properties.configuration.ingress.fqdn

@description('The URL of the deployed container app')
output url string = 'https://${containerApp.properties.configuration.ingress.fqdn}'

@description('The health check endpoint')
output healthEndpoint string = 'https://${containerApp.properties.configuration.ingress.fqdn}/api/health'

@description('The resource ID of the container app')
output containerAppId string = containerApp.id

@description('The Log Analytics workspace ID for monitoring')
output logAnalyticsId string = logAnalytics.id
