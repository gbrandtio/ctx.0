# API Contract

Place the backend's exported OpenAPI document here as `swagger.json`.

When paired with the API template, export it from the running API (`/swagger/v1/swagger.json`) and copy it into this folder whenever endpoints change. Agents read `docs/API/swagger.json` as the authoritative request/response contract — a stale copy causes wrong client code, so refreshing it is part of any API-facing change.
