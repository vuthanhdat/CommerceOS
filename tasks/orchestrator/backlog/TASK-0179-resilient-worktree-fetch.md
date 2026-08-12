# TASK-0179 - Make worktree creation resilient to transient origin fetch failures

Status: Backlog
Specification maturity: Ready
Execution permission: YES
Owner: Builder - Engineering / Harness
Recommended implementation model: gpt-5.6-terra, medium reasoning, standard service tier
Created: 2026-08-12
Depends on: TASK-0178
Cloud verification: No

## Goal

Prevent a temporary GitHub connectivity failure from incorrectly escalating a planning candidate to
`HUMAN_REQUIRED` when a safe, synchronized local `main` snapshot is available.

## Business context

TASK-0095 planning was never started because worktree preparation treated a failed `git fetch origin
main` as a planning decision. Network availability is an operational condition, not a human product
or architecture decision.

## Planning readiness

- Owning domain/bounded context: Engineering / Harness.
- State/error semantics: transient fetch failures may use a verified synchronized local snapshot;
  unsafe or non-network failures remain blocking.
- Module ownership: Git worktree preparation and its regression tests.
- Material ADRs: none.
- Remaining planning blockers: none.

## In scope

- Recognize transient remote-network fetch failures during task worktree preparation.
- Retry the fetch once.
- Fall back only when local `main` and cached `origin/main` both exist and resolve to the same commit.
- Preserve the original failure when no safe synchronized snapshot exists.
- Add regression tests for safe fallback and fail-closed behavior.

## Out of scope

- Offline integration, merge, or push.
- Treating authentication, authorization, repository, or ref errors as transient.
- Changing canonical task maturity or dependency rules.

## Acceptance criteria

### AC01 - Safe transient fallback

Given a transient fetch failure and equal local `main`/cached `origin/main`, worktree creation proceeds
from that cached commit after one retry.

### AC02 - Fail closed

Given unequal or missing refs, or a non-transient Git failure, worktree creation raises the original
workspace error and creates no task worktree.

### AC03 - Regression coverage

Automated tests cover retry success, safe cached fallback, and rejection of unsafe fallback.

## Architecture and security impact

No product/domain boundary changes. The fallback validates immutable commit IDs and never trusts a
working-tree file comparison or an arbitrary task-supplied ref.

## Test plan

- Targeted Git workspace tests.
- Full orchestrator test suite.
- Repository harness.

## Completion summary

Pending implementation.
