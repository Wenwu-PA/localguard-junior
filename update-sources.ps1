$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$projectPath = Join-Path $root 'LocalGuardJunior'
if (-not (Test-Path (Join-Path $projectPath 'LocalGuardJunior.csproj'))) {
    throw 'Run create-project.ps1 first.'
}
Copy-Item -Path (Join-Path $root 'src\*.cs') -Destination $projectPath -Force
& dotnet build (Join-Path $projectPath 'LocalGuardJunior.csproj')
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Host 'Updated. Run: dotnet run --project .\LocalGuardJunior' -ForegroundColor Green
