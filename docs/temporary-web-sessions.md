# Temporary parallel web sessions

During the Angular V1/V2 side-by-side migration, the V2 API's Development configuration sets:

```json
"Authentication": {
  "EnforceSingleWebSession": false
}
```

`ConfigUuidFilter` skips only the comparison against the latest web login. JWT authentication, company UUID validation and refresh-token expiration/revocation checks remain in place. Existing headers and login records do not need to change.

## Reactivate after side-by-side testing

Set `Authentication:EnforceSingleWebSession` to `true` in `appsettings.Development.json`, or remove the temporary `Authentication` section. Restart the API. The default when the setting is absent is **true**, preserving the original restriction. The switch can also be overridden with `Authentication__EnforceSingleWebSession=true` in the process environment.

The checked-in development override does not apply to other environments, and local appsettings are excluded from published artifacts. For local runs, use the Development environment and restart the running V2 API to ensure the new code/configuration is loaded.

This change affects this V2 API repository only. If the V1 client still targets a separate V1 API, that API can still reject its older login when both APIs share the login database. Point both local clients at V2 for this switch to cover both, or apply an equivalent temporary switch to V1 separately.

Verification extends the isolated MySQL session test: default enforcement rejects an older login; disabling it accepts the same login while invalid company UUIDs and missing authentication remain rejected.
