# Calculated SHA256 for shared secret

```
using System.Security.Cryptography;

var input = "shared_secret";
using (var sha = SHA256.Create())
{
    var bytes = Encoding.UTF8.GetBytes(input);
    var hash = sha.ComputeHash(bytes);

    return Convert.ToBase64String(hash);
}
```

# Invitation

No need to have autoprovision enabled for client ... 

User has to exist with
- IsActive = false
- Identities = null
- AppProfiles.{profileKey} != null

