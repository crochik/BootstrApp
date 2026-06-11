const BASE = process.env.BASE_URL;

const subscribeHook = async (z, bundle) => {
  const res = await z.request({
    url: `${BASE}/zapier/v1/subscriptions`,
    method: "POST",
    body: {
      object: bundle.inputData.object,
      event: bundle.inputData.event,
      targetUrl: bundle.targetUrl,
    },
  });
  return res.data; // { id, object, event, targetUrl } -> stored as subscribeData
};

const unsubscribeHook = async (z, bundle) => {
  const id = bundle.subscribeData.id;
  await z.request({
    url: `${BASE}/zapier/v1/subscriptions/${id}`,
    method: "DELETE",
  });
  return {};
};

// Incoming hook -> emit the delivered envelope as a single item.
const perform = (z, bundle) => [bundle.cleanedRequest];

// "Test trigger" sample data.
const performList = async (z, bundle) => {
  const res = await z.request({
    url: `${BASE}/zapier/v1/objects/${bundle.inputData.object}/events/${bundle.inputData.event}/samples`,
  });
  return res.data; // already an array
};

module.exports = {
  key: "object_event",
  noun: "Event",
  display: {
    label: "Object Event",
    description: "Fires on Create/Update/Delete of an object.",
  },
  operation: {
    type: "hook",
    inputFields: [
      {
        key: "object",
        label: "Object",
        required: true,
        dynamic: "objects.key.label",
        altersDynamicFields: true,
      },
      {
        key: "event",
        label: "Event",
        required: true,
        dynamic: "events.key.label",
      },
    ],
    performSubscribe: subscribeHook,
    performUnsubscribe: unsubscribeHook,
    perform,
    performList,
    sample: {
      eventId: "evt_123",
      tenantId: "acct_123",
      eventName: "contact.create",
      occurredAt: "2026-01-15T09:30:00.0000000Z",
      schemaVersion: "1",
      data: {},
    },
  },
};
