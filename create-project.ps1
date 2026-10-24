$ErrorActionPreference = 'Stop'

# ASCII-only strings: works in Windows PowerShell 5.1 and PowerShell 7.
$root = $PSScriptRoot
$projectName = 'LocalGuardJunior'
$projectPath = Join-Path $root $projectName
$sources = Join-Path $root 'src'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet SDK was not found. Install .NET 10 SDK first.'
}

if (Test-Path $projectPath) {
    throw 'Project folder already exists. Use update-sources.ps1 instead.'
}

Write-Host '1/3 Create project using dotnet new winforms' -ForegroundColor Cyan
& dotnet new winforms --name $projectName --output $projectPath --no-restore
if ($LASTEXITCODE -ne 0) { throw 'dotnet new winforms failed.' }

Write-Host '2/3 Copy C# files' -ForegroundColor Cyan
Get-ChildItem -Path $projectPath -File -Filter 'Form1*' | Remove-Item -Force
Copy-Item -Path (Join-Path $sources '*.cs') -Destination $projectPath -Force

Write-Host '3/3 Build' -ForegroundColor Cyan
& dotnet build (Join-Path $projectPath ($projectName + '.csproj'))
if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed. See messages above.' }

Write-Host 'Done. Run: dotnet run --project .\LocalGuardJunior' -ForegroundColor Green
