# Crochik.NET

nuget sources Add -Name "Crochik" \
     -Source "https://nuget.pkg.github.com/crochik/index.json" \
     -UserName crochik -Password ?????? 


nuget push Crochik.NET.1.0.0.nupkg -Source "Crochik" -ConfigFile ./NuGet.Config
dotnet nuget push Crochik.NET.1.0.0.nupkg -s https://nuget.pkg.github.com/crochik/index.json -k 