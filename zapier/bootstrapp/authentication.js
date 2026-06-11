const BASE = process.env.BASE_URL; // https://rproxy-fci.fci.cloud
const IDP = process.env.IDP_URL; // IdentityServer base, e.g. https://identity.fci.cloud

module.exports = {
  type: "oauth2",
  oauth2Config: {
    authorizeUrl: {
      url: `${IDP}/connect/authorize`,
      params: {
        client_id: "{{process.env.CLIENT_ID}}",
        state: "{{bundle.inputData.state}}",
        redirect_uri: "{{bundle.inputData.redirect_uri}}",
        response_type: "code",
        scope: "openid profile zapier offline_access",
      },
    },
    getAccessToken: {
      url: `${IDP}/connect/token`,
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: {
        code: "{{bundle.inputData.code}}",
        client_id: "{{process.env.CLIENT_ID}}",
        client_secret: "{{process.env.CLIENT_SECRET}}",
        grant_type: "authorization_code",
        redirect_uri: "{{bundle.inputData.redirect_uri}}",
      },
    },
    refreshAccessToken: {
      url: `${IDP}/connect/token`,
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: {
        refresh_token: "{{bundle.authData.refresh_token}}",
        client_id: "{{process.env.CLIENT_ID}}",
        client_secret: "{{process.env.CLIENT_SECRET}}",
        grant_type: "refresh_token",
      },
    },
    scope: "openid profile zapier offline_access",
    autoRefresh: true,
  },
  test: { url: `${BASE}/zapier/v1/user`, method: "GET" },
  connectionLabel: (z, bundle) => bundle.inputData.name,
};
