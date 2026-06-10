# bootstrapp.cloud schema conventions

These rules are non-optional. The MCP server validates them on every
`add_object_class` call.

## Documentation on every class and property

Every class root and every property in its JSON Schema carries:

- **`title`** — short noun phrase, 5–10 words.
- **`description`** — one to three sentences explaining purpose,
  intent, and any non-obvious constraints.
- **`examples`** — concrete realistic value(s). For a class root, a
  single-element array containing a fully-formed example object
  (valid against the schema, plausible data — not lorem ipsum). For
  a property, a single-element array (or `example` scalar) of the
  right type.

These are part of the contract: the OpenAPI surface uses them
verbatim. Missing values yield an unusable client.

## Top-level vs embedded

A class is either **top-level** (stored in its own collection,
queryable via filter endpoint, eligible as a reference target) or
**embedded** (exists only inside another class, never persisted on
its own).

- Top-level → `type: TopLevel` on `add_object_class`. Carries `_id`.
- Embedded → `type: Embedded`. No `_id`. Filter endpoint not
  available; `indexedFields` must be `[]`.

Action input/output classes are always embedded.

## The `_id` field

Every top-level class declares:

```json
"_id": {
  "type": "string",
  "format": "uuid",
  "title": "<Class> ID",
  "description": "Server-assigned UUID identifying this <class>.",
  "examples": ["6f1d3b2a-4e7f-4c2a-9d8b-1c2e3f4a5b6c"]
}
```

`_id` is in `required`. The backend assigns the value on create.
Embedded classes do not declare `_id`.

## Indexed fields

Every top-level class declares an explicit `indexedFields` list,
passed verbatim to `add_object_class`. **Only indexed fields can be
used to filter results** — any field that appears in any condition
(direct filter call or named view) must be on this list.

Rules:

- Always include `_id` (every reference lookup needs it).
- Always include any `*Id` reference field that joins this class to
  another.
- Include every property used in any view condition or expected
  filter call on this class.
- Keep the list tight — don't index "just in case." Add later when
  a real query appears.

Embedded classes pass `indexedFields: []`.

## References

A field whose value is the `_id` of another top-level object is a
reference. Declared as
`{ "type": "string", "format": "uuid" }` — same shape as the `_id`
it points at.

References are recorded in the References registry in
`BOOTSTRAPP.md` (source class, source field, target class, target
field) and passed to `add_object_class` as `foreignKeys`.

## Inheritance

Single-parent only, with a string discriminator field on the parent.
Child classes set the discriminator to a fixed literal. Avoid deep
chains.

## Naming

- App name: snake_case (lowercase letters, digits, underscores) —
  no hyphens, no dots.
- Every namespace segment satisfies the same rule.
- Class names: PascalCase.
- Property names: camelCase.