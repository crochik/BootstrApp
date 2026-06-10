param ($message)

# --------------------------------------------------------------
$DEPLOYMENT="staging/apps/reports/deployment.yml"
$JOBTEMPLATES = @(
    "staging/apps/reports/job_export_to_postgres.yml"
    )
$APP="reports"
$DOCKERIMAGE = "crochik/${APP}"
$IMAGE = "crochik/${APP}"
# $IMAGE="ghcr.io/crochik/${APP}"
# --------------------------------------------------------------

if ($message) {
    Write-Output "----------------------------------------------"
    Write-Output "Running as part of a build: $message"
    Write-Output "----------------------------------------------"
}

$Today = (Get-Date -Format FileDate)

if (Test-Path -Path "./tag.version") { 
    Write-Output "Config file exists"
    $Version = Get-Content -Raw -Path "./tag.version"  -ErrorAction Stop | ConvertFrom-Json

    if ( $Version.PSObject.Properties["Major"] -and $Version.Major -eq $Today ) {
        $Version.Minor++        
    } else {
        $Version = @{
            Major = $Today
            Minor = 1
        }
    }
    $TAG = $Version.Major + "." + $Version.Minor
}
else {
    $Version = @{
        Major = $Today
        Minor = 0
    }
    $TAG = $Version.Major
}

$Version | ConvertTo-Json | Out-File "./tag.version"

Remove-Item ./out -Recurse

if (!$message) {
    $CommitMessage = Read-Host -Prompt 'Commit message:'
    git add ..
    git commit -m "${CommitMessage}"
    git tag -a "${APP}.${TAG}" -m "${CommitMessage}"
    git push --follow-tags
}

Write-Output "----------------------------------------------"
Write-Output "Publishing: ${DOCKERIMAGE} - ${TAG}"
dotnet publish -c Release -o out -r linux-x64 --self-contained
docker buildx build --platform linux/amd64 --rm -f "Dockerfile" -t ${DOCKERIMAGE}:${TAG} .
docker push ${DOCKERIMAGE}:${TAG}

Write-Output "----------------------------------------------"
Write-Output "${IMAGE}:${TAG}"
$Env:IMAGE = "${IMAGE}:${TAG}"
$Env:TAG = "${APP}:${TAG}"

yq '.reports.image = env(TAG)' -i ../../OPS/staging/config/releases.yaml

# yq '.spec.template.spec.containers[0].image = env(IMAGE)' -i ../../OPS/$DEPLOYMENT 
# yq '.spec.template.spec.containers[0].env[0].name=\"PI_CONTAINER\"' -i ../../OPS/$DEPLOYMENT 
# yq '.spec.template.spec.containers[0].env[0].value = env(TAG)' -i ../../OPS/$DEPLOYMENT 
# 
# foreach ( $JOBTEMPLATE in $JOBTEMPLATES )
# {
#     Write-Output "----------------------------------------------"
#     Write-Output "${IMAGE}:${TAG}"
# 
#     yq '.spec.jobTemplate.spec.template.spec.containers[0].image = env(IMAGE)' -i ../../OPS/$JOBTEMPLATE 
#     yq '.spec.jobTemplate.spec.template.spec.containers[0].env[0].name=\"PI_CONTAINER\"' -i ../../OPS/$JOBTEMPLATE 
#     yq '.spec.jobTemplate.spec.template.spec.containers[0].env[0].value = env(TAG)' -i ../../OPS/$JOBTEMPLATE 
# }

if (!$message) {
    Set-Location ../../OPS
    
    pwsh ./update.ps1
    
    git add .
    git commit -m "${CommitMessage}"
    git tag -a "${APP}.${TAG}" -m "${CommitMessage}"
    git push --follow-tags
}