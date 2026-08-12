# Task catalogs

CommerceOS keeps two physically and operationally separate task catalogs:

- `commerceos/` — product, domain, application, infrastructure/runtime, CI, and delivery work.
- `orchestrator/` — the local Task Orchestrator, agent workflow, review harness, and operator UI.

`BACKLOG.v2.yaml` is a shared registry so dependencies can reference completed foundation work
across catalogs. Its shards and detailed task artifacts are physically separated. One
Orchestrator process operates on exactly one catalog; `commerceos` is the default.

```text
python tools/orchestrator.py --catalog commerceos validate
python tools/orchestrator.py --catalog orchestrator validate
```

Each catalog receives separate SQLite state and logs under
`.commerceos/orchestrator/<catalog>/` unless `--state` overrides the path.
