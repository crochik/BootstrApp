const authentication = require("./authentication");
const objects = require("./triggers/objects");
const events = require("./triggers/events");
const objectEvent = require("./triggers/object_event");

const addBearerHeader = (request, z, bundle) => {
  if (bundle.authData && bundle.authData.access_token) {
    request.headers.Authorization = `Bearer ${bundle.authData.access_token}`;
  }
  return request;
};

const refreshOn401 = (response, z) => {
  if (response.status === 401) {
    throw new z.errors.RefreshAuthError();
  }
  return response;
};

module.exports = {
  version: require("./package.json").version,
  platformVersion: require("zapier-platform-core").version,
  authentication,
  beforeRequest: [addBearerHeader],
  afterResponse: [refreshOn401],
  triggers: {
    [objects.key]: objects,
    [events.key]: events,
    [objectEvent.key]: objectEvent,
  },
};
