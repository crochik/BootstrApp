const BASE = process.env.BASE_URL;

const perform = async (z, bundle) => {
  const res = await z.request({
    url: `${BASE}/zapier/v1/objects/${bundle.inputData.object}/events`,
  });
  return res.data.map((e) => ({
    id: e.key,
    key: e.key,
    label: e.label,
    description: e.description,
  }));
};

module.exports = {
  key: "events",
  noun: "Event",
  display: {
    label: "List Events",
    description: "Dropdown source.",
    hidden: true,
  },
  operation: {
    perform,
    inputFields: [{ key: "object", required: true }],
  },
};
