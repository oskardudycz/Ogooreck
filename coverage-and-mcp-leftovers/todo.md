# Todo: Leftovers after step 8

Plan: `plan.md` (r1). Spec: `spec.md` (r1).

## Steps

- [x] 1. Keep array attributes (1.1-1.2)
- [x] 2. Test runs that don't rebuild an unchanged project (2.1-2.2; needs the connected ogooreck-mcp, 2.2 needs Q2)
- [ ] 3. TUnit coverage counts for Ogooreck (3.1; needs Q1). Left out of this release by Oskar on 2026-11-27; see progress.md
- [x] 4. Cover the configuration API of Ogooreck (4.1)
- [x] 5. Cover the steps of Ogooreck (5.1)
- [x] 6. Cover the rest of Ogooreck (6.1)
- [x] 7. Cover Ogooreck.Kafka (7.1; needs Q3)
- [ ] 8. Close out: .NET 11 build, qa.md, cleanup

## Parked (spec "Out of Scope")

- [ ] One test project instead of a dozen; `TestWebApi` naming
- [ ] `.mcp/server.json` in the MCP package
- [ ] Receiver span per received metrics export
- [ ] Aspire `OTEL_METRIC_EXPORT_INTERVAL=500`
- [ ] `IDerivedPlugin`, unused usings, `MIGRATION.md`
- [ ] Prove `test-skill`'s failure checks once by hand
- [ ] Follow-up: a supervisor in front of ogooreck-mcp that rebuilds and restarts the server on change and replays `initialize`, so a rebuild needs no reconnect (`../qa.md` 116)
