$serviceName = "TelegramBotService"
$displayName = "Telegram Bot Service"
$description = "Telegram Bot Service with weather and search capabilities"
$exePath = Join-Path $PSScriptRoot "bin\Release\net6.0\win-x64\publish\TelegramBot.exe"

# Stop and remove the service if it exists
if (Get-Service $serviceName -ErrorAction SilentlyContinue) {
    Stop-Service $serviceName
    sc.exe delete $serviceName
    Start-Sleep -Seconds 2
}

# Create the service
sc.exe create $serviceName binPath= "$exePath" DisplayName= "$displayName" start= auto
sc.exe description $serviceName "$description"

# Start the service
Start-Service $serviceName

Write-Host "Service installed and started successfully!"
Write-Host "You can manage the service in Windows Services (services.msc)"
