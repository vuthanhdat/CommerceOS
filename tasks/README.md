# CommerceOS task catalog

`commerceos/` contains product, domain, application, infrastructure, CI, and delivery planning
records. These files are optional context for direct human–AI work; they do not activate an
automated Builder/Reviewer workflow.

`BACKLOG.v2.yaml` and the shards under `commerceos/backlog-v2/` preserve the existing product plan.
Detailed specifications live under `commerceos/backlog/`, active manual work may be recorded under
`commerceos/active/`, and completed records live under `commerceos/completed/`.

The standalone TaskOrchestrator source and its own development history now live in the sibling
`TaskOrchestrator` repository and are not part of the CommerceOS runtime or verification harness.
