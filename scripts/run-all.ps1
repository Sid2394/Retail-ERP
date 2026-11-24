# Starts backend and frontend in two windows. Run from the repository root.
Start-Process powershell -ArgumentList '-NoExit','-Command',"cd '$PSScriptRoot\..\backend\MiniERP.Api'; dotnet run"
Start-Process powershell -ArgumentList '-NoExit','-Command',"cd '$PSScriptRoot\..\frontend'; npm run dev"
Write-Host 'API: http://localhost:5080   Web: http://localhost:5173'
