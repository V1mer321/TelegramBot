# Azure configuration script
param(
    [Parameter(Mandatory=$true)]
    [string]$resourceGroupName,
    
    [Parameter(Mandatory=$true)]
    [string]$location,
    
    [Parameter(Mandatory=$true)]
    [string]$appServicePlanName,
    
    [Parameter(Mandatory=$true)]
    [string]$webAppName,
    
    [Parameter(Mandatory=$true)]
    [string]$telegramBotToken,
    
    [Parameter(Mandatory=$true)]
    [string]$weatherApiKey
)

# Login to Azure
Write-Host "Please login to Azure..."
az login

# Create resource group
Write-Host "Creating resource group..."
az group create --name $resourceGroupName --location $location

# Create app service plan
Write-Host "Creating app service plan..."
az appservice plan create `
    --name $appServicePlanName `
    --resource-group $resourceGroupName `
    --sku B1 `
    --is-linux false

# Create web app
Write-Host "Creating web app..."
az webapp create `
    --name $webAppName `
    --resource-group $resourceGroupName `
    --plan $appServicePlanName `
    --runtime "dotnet:6"

# Configure app settings
Write-Host "Configuring app settings..."
az webapp config appsettings set `
    --name $webAppName `
    --resource-group $resourceGroupName `
    --settings TelegramBot:Token=$telegramBotToken OpenWeatherMap:ApiKey=$weatherApiKey

# Enable always on
Write-Host "Enabling always on..."
az webapp config set `
    --name $webAppName `
    --resource-group $resourceGroupName `
    --always-on true

# Get publish profile
Write-Host "Getting publish profile..."
$publishProfile = az webapp deployment list-publishing-profiles `
    --name $webAppName `
    --resource-group $resourceGroupName `
    --xml

# Save publish profile
$publishProfile | Out-File -FilePath "publish-profile.xml"

Write-Host "Setup complete! Please follow these steps:"
Write-Host "1. Copy the contents of publish-profile.xml"
Write-Host "2. Go to your GitHub repository settings"
Write-Host "3. Add a new secret named AZURE_WEBAPP_PUBLISH_PROFILE"
Write-Host "4. Paste the publish profile content as the secret value"
