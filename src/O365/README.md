# Login using admin
clientSecret (password): ggsIJ87?_%{xeukYNMUD241

# Tenant Id 
programinterface: 66e4ad38-89b1-42da-9252-cfef787fa221

# Users:
Felipe: 638ddd20-bf08-4671-9d5e-8af444a858bc
Other: 76654a48-9123-43eb-bfdf-9cf19b0b313c


## links:
https://apps.dev.microsoft.com/#/application/527fa1ce-35c5-4c73-8a68-60febf0b2398

GET https://login.microsoftonline.com/{tenant}/adminconsent?client_id=527fa1ce-35c5-4c73-8a68-60febf0b2398&state=css&redirect_uri=https://o365.fci.cloud/Login/Redirect

use common as tenantId to allow the user to choose the account.

https://login.microsoftonline.com/common/adminconsent?client_id=527fa1ce-35c5-4c73-8a68-60febf0b2398&state=css&redirect_uri=https://o365.fci.cloud/Login/Redirect

## result of login (redirected to)
https://o365.fci.cloud/Login/Redirect?admin_consent=True&tenant=66e4ad38-89b1-42da-9252-cfef787fa221&state=css