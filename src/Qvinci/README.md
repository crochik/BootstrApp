# Qvinci

curl -H "X-apiToken: <APITOKEN>" -H "Accept: application/xml" "https://api.qvinci.com/v1/Reporting/Aging/?VerticalAnalysisType=None&AP=true&AR=false&CompanyId=1388387&Locations=[3483480]"

curl -H "X-apiToken: <APITOKEN>"  "https://api.qvinci.com/v1/Reporting/Aging/?VerticalAnalysisType=None&AP=true&AR=false&CompanyId=1388387&Locations=4629625"


curl -H "X-apiToken: <APITOKEN>"  "https://api.qvinci.com/v1/Reporting/ProfitAndLoss?CompanyId=1388387&RelativeDateRange=LastMonth&DateFrequency=Monthly&UseAccountMapping=true&VerticalAnalysisType=None"


curl -H "X-apiToken: <APITOKEN>"  "https://api.qvinci.com/v1/location/search?CompanyId=1388387&take=100" > locations.json
curl -H "X-apiToken: <APITOKEN>"  "https://api.qvinci.com/v1/location/search?CompanyId=1388387&skip=100&take=100" >> locations.json
curl -H "X-apiToken: <APITOKEN>"  "https://api.qvinci.com/v1/location/search?CompanyId=1388387&skip=200&take=100" >> locations.json

# Client id for EternityIt

curl -X POST "https://idp.fci.cloud/connect/token" -d "client_id=EthernityIt&grant_type=client_credentials&client_secret=Lira@1948" -H "Content-Type: application/x-www-form-urlencoded"
