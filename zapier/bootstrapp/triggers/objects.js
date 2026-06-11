const BASE = process.env.BASE_URL;

const perform = async (z, bundle) => {
  const res = await z.request({ url: `${BASE}/zapier/v1/objects` });
  // API returns { key, label, description }; Zapier dropdowns want an `id`.
  return res.data.map((o) => ({
    id: o.key,
    key: o.key,
    label: o.label,
    description: o.description,
  }));
};

module.exports = {
  key: "objects",
  noun: "Object",
  display: {
    label: "List Objects",
    description: "Dropdown source.",
    hidden: true,
  },
  operation: { perform },
};
