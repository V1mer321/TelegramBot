# Telegram Bot Service

This is an automated Telegram bot service that runs on Azure App Service.

## Setup Instructions

1. Install Prerequisites:
   - Install [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
   - Install [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)

2. Run the Azure setup script:
   ```powershell
   .\setup-azure.ps1 `
       -resourceGroupName "your-resource-group" `
       -location "westeurope" `
       -appServicePlanName "your-service-plan" `
       -webAppName "your-web-app-name" `
       -telegramBotToken "your-telegram-bot-token" `
       -weatherApiKey "your-weather-api-key"
   ```

3. Configure GitHub:
   - Copy the contents of the generated `publish-profile.xml`
   - Go to your GitHub repository settings
   - Navigate to "Settings" > "Secrets and variables" > "Actions"
   - Add a new secret named `AZURE_WEBAPP_PUBLISH_PROFILE`
   - Paste the publish profile content as the secret value

4. Update the workflow file:
   - Open `.github/workflows/azure-deploy.yml`
   - Update the `AZURE_WEBAPP_NAME` to match your Azure web app name

5. Deploy:
   - Push your changes to the main branch
   - The GitHub Actions workflow will automatically deploy your bot
   - The bot will run automatically as a Windows service

## Configuration

The bot uses the following configuration:
- Telegram Bot Token: Stored in Azure App Service configuration
- OpenWeatherMap API Key: Stored in Azure App Service configuration

## Automatic Deployment

The bot will automatically deploy when:
- You push to the main branch
- You manually trigger the workflow

## Monitoring

You can monitor your bot through:
- Azure App Service Logs
- Application Insights (if configured)
- Azure App Service monitoring tools
