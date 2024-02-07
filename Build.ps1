# Build.ps1

# Define absolute paths
$projectPath = $PSScriptRoot
$buildOutput = Join-Path -Path $projectPath -ChildPath "BizDevAgent\bin\Release" # Adjust depending on your build configuration
$zipDir = Join-Path -Path $projectPath -ChildPath "Build"
$zipFilePath = Join-Path -Path $zipDir -ChildPath "BizDevAgent.zip"
$networkFolderPath = "\\indiansummer\Users\gregs\Downloads" # Update with your network folder path
$networkFilePath = Join-Path -Path $networkFolderPath -ChildPath "BizDevAgent.zip" # Full path including filename

# Ensure build output and zip directories exist
if (-Not (Test-Path -Path $buildOutput)) {
    New-Item -Path $buildOutput -ItemType Directory
}
if (-Not (Test-Path -Path $zipDir)) {
    New-Item -Path $zipDir -ItemType Directory
}

# Build the project
dotnet build "$projectPath\BizDevAgent.sln" --configuration Release

# Zip the contents
Compress-Archive -Path "$buildOutput\*" -DestinationPath $zipFilePath -Force

# Copy the zip to the network folder
Copy-Item -Path $zipFilePath -Destination $networkFilePath -Force
