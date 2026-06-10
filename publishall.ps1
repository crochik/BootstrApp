$PROJECTS = @(
    "./API",
    "./Dipper",
    "./IDP",
#     "./IME",
    "./O365",
    "./LMS",
    "./PI.CompanyCam",
    "./PI.Convertros",
    "./PI.DocuSeal",
    "./PI.Files",
    "./PI.Google",
#     "./PI.GTM",
    "./PI.LangChain",
    "./PI.Lumin",
    "./PI.Marchex",
    "./PI.OpenAPI",
    "./PI.Openphone",
    "./PI.ProductCatalog",
    "./PI.QuickBooks",
    "./PI.Reports",
    "./PI.Salesforce",
    "./PI.SendGrid",
    "./PI.Singer", 
    "./PI.Slack",
    "./PI.Stripe",
    "./PI.Typeform",
#     "./PI.Verse",
    "./PI.WebPunch",
#     "./PI.Zoom",
    "./Qvinci",
    "./U2",
    "./Zapier"
)

$Today = (Get-Date -Format FileDate)

if ([System.IO.File]::Exists("./tag.version")) { 
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

$CommitMessage = Read-Host -Prompt 'Commit message:'

foreach ( $PROJECT in $PROJECTS )
{
    Push-Location $PROJECT
    & "./kubernetes.ps1" $CommitMessage
    Pop-Location
}

git add .
git commit -m "${CommitMessage}"
git tag -a "${TAG}" -m "${CommitMessage}"
git push --follow-tags

Push-Location ../OPS
git add .
git commit -m "${CommitMessage}"
git tag -a "${TAG}" -m "${CommitMessage}"
git push --follow-tags
Pop-Location