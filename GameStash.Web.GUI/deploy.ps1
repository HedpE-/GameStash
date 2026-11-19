# Requires -RunAsAdmin
<#
    GameStash - Concurrent Deployment Pipeline Script
    Executa o build, publish e sincronização de rede em paralelo.
#>
$ErrorActionPreference = "Stop"

# --- DEFINIÇÃO DE VARIÁVEIS DE CONFIGURAÇÃO ---
$LocalReleasePath = "C:\GameStash-Release"
$NetworkLatestPath = "\\192.168.1.65\Data Center\GameStash-latest"
$ServiceName = "GameStash"

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host " STARTING CONCURRENT GAMESTASH RELEASE PIPELINE " -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan

$DeploymentTimer = [System.Diagnostics.Stopwatch]::StartNew()

# 1. [PASSO DE SEGURANÇA] Para o serviço remotamente no servidor via WinRM
Write-Host "[1/6] Stopping GameStash Windows Service on Production Server..." -ForegroundColor Yellow

# Dispara o comando remoto usando as credenciais do utilizador Admin do servidor
# Se ambas as máquinas usarem o mesmo utilizador/password, podes omitir o parâmetro -Credential
Invoke-Command -ComputerName "192.168.1.65" -ScriptBlock { 
    Stop-Service -Name "GameStashService" -Force -ErrorAction SilentlyContinue 
}

# 2. DISPARAR TAREFA ASSÍNCRONA DE REDE (CONCORRÊNCIA COM VALIDAÇÃO DE ERROS REMOTOS)
Write-Host "[2/6] Spawning async network cleanup background job..." -ForegroundColor Yellow
$NetworkJob = Start-Job -ScriptBlock {
    param($Path)
    $ErrorActionPreference = "Stop"
    try {
        if (Test-Path $Path) {
            # Tenta apagar recursivamente. Se houver um .exe ou .dll trancado pelo servidor, o Windows lança um erro de IO
            Get-ChildItem -Path $Path -Recurse | Remove-Item -Recurse -Force
        }
        return "Success"
    }
    catch {
        # Envia a mensagem de erro detalhada de volta para a thread principal do PC de Dev
        throw "CLEANUP_FAILED: $($_.Exception.Message)"
    }
} -ArgumentList $NetworkLatestPath

# 3. LIMPEZA LOCAL E COMPILAÇÃO (Operações de Disco Locais)
Write-Host "[3/6] Cleaning old local release artifacts..." -ForegroundColor Yellow
if (Test-Path $LocalReleasePath) {
    Get-ChildItem -Path $LocalReleasePath -Recurse | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "[4/6] Executing dotnet publish (Self-Contained x64)..." -ForegroundColor Magenta
# Compila e gera o release limpo na pasta local
dotnet publish -c Release -r win-x64 -o $LocalReleasePath -p:WarningLevel=0

# 4. SINCRO DE CONCORRÊNCIA: Aguarda que o Job de rede termine
Write-Host "Waiting for background network cleanup job to finalize..." -ForegroundColor Yellow

# Caracteres de animação do spinner
$spinner = @('|', '/', '-', '\')
$counter = 0

# Fica em loop enquanto o Job estiver a correr (Running)
while ($NetworkJob.State -eq "Running") {
    # Desenha o spinner na mesma linha
    Write-Host "`r[PROCESSING] Remote server file purge ongoing... $($spinner[$counter % 4])" -NoNewline -ForegroundColor Cyan
    Start-Sleep -Milliseconds 250
    $counter++
}
Write-Host "`r[COMPLETE] Background network cleanup job finished.          " -ForegroundColor Green

# Captura o output e erros da thread assíncrona
$JobOutput = Receive-Job $NetworkJob -ErrorAction SilentlyContinue
$JobError = $NetworkJob.ChildJobs[0].JobData.Error

# Se o Job falhou ou o output reportou falha de limpeza por ficheiro trancado
if ($NetworkJob.State -eq "Failed" -or $JobError -or $JobOutput -match "CLEANUP_FAILED") {
    Write-Host ""
    Write-Host "!!!!!!!!!!!!!!!!================================================================================" -ForegroundColor Red
    Write-Host " CRITICAL ERROR ENCOUNTERED DURING DESTINATION CLEANUP TRANSIT SEQUENCE " -ForegroundColor Red
    Write-Host "!!!!!!!!!!!!!!!!================================================================================" -ForegroundColor Red
    Write-Host "Notice: Remote file locks could not be released on the production server host." -ForegroundColor White
    Write-Host "Details: The target directory or its dependencies are currently locked by an active remote process." -ForegroundColor White
    Write-Host "Action: Please manually stop the GameStash Windows Service on the target production server machine." -ForegroundColor White
    Write-Host "Result: Deployment pipeline cannot proceed safely and will now terminate immediately to prevent corruption." -ForegroundColor Red
    Write-Host "================================================================================================" -ForegroundColor Red
    Write-Host ""
    
    # Termina o Job em segundo plano e força o encerramento do pipeline (Exit Code 1 para o Jenkins)
    Remove-Job $NetworkJob
    Exit 1
}

Remove-Job $NetworkJob

# 5. TRANSFERÊNCIA ULTRA-RÁPIDA VIA ROBOCOPY
Write-Host "[5/6] Deploying build artifacts to Network Share..." -ForegroundColor Green
# O Robocopy é nativo, mostra progresso e é infinitamente mais rápido que o Copy-Item para rede
robocopy $LocalReleasePath $NetworkLatestPath /E /Z /R:3 /W:5 /NDL /NJH /NJS

# 6. REINICIAR O SERVIÇO LOCAL
Write-Host "[6/6]Restarting Local Windows Service..." -ForegroundColor Green
Invoke-Command -ComputerName "192.168.1.65" -ScriptBlock { 
    Start-Service -Name "GameStashService" -ErrorAction SilentlyContinue 
}

$DeploymentTimer.Stop()
$ElapsedTime = "{0:mm} min {0:ss} sec" -f $DeploymentTimer.Elapsed

Write-Host "=========================================================" -ForegroundColor Green
Write-Host " DEPLOYMENT PIPELINE SUCCESSFUL!                         " -ForegroundColor Green
Write-Host " TOTAL EXECUTION TIME: $ElapsedTime                      " -ForegroundColor Green
Write-Host "=========================================================" -ForegroundColor Green