param([string]$ArtifactDirectory="publish",[switch]$ForceFailure)
$ErrorActionPreference='Stop'
function Run([string]$exe,[string[]]$arguments){& $exe @arguments;if($LASTEXITCODE -ne 0){throw "Etapa $exe falhou com codigo $LASTEXITCODE"}}
Run dotnet @('restore')
Run dotnet @('build','--no-restore','-c','Release')
if($ForceFailure){$env:FORCE_TEST_FAILURE='1'}
try{Run dotnet @('run','--project','PortalTarefas.Tests','-c','Release','--no-build')}finally{Remove-Item Env:FORCE_TEST_FAILURE -ErrorAction SilentlyContinue}
Push-Location PortalTarefas.Frontend
try{Run npm @('ci');Run npm @('test');Run npm @('run','build')}finally{Pop-Location}
Run dotnet @('publish','PortalTarefas.Web','-c','Release','-o',$ArtifactDirectory)
Copy-Item PortalTarefas.Frontend/dist/* "$ArtifactDirectory/wwwroot/app" -Recurse -Force
git rev-parse HEAD | Set-Content "$ArtifactDirectory/commit.txt"
Write-Output 'PUBLICACAO_LOCAL_CONCLUIDA'
