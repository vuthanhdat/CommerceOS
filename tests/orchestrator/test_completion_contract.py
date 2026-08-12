from __future__ import annotations

import sys
import unittest
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[2] / "tools"
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

from commerceos_orchestrator.completion_contract import (
    CompletionContractError,
    CompletionEntryGate,
    CompletionTransaction,
)


class CompletionTransactionTests(unittest.TestCase):
    def test_entry_gate_requires_complete_binding_inventory(self):
        payload = {
            "contractVersion": "CompletionEntryGate/v1", "taskId": "TASK-0100",
            "taskCommitSha": "abc", "builderManifestPath": "builder.json",
            "verificationReportPath": "verification.json", "reviewLedgerPath": "review.json",
            "acceptanceCriterionIds": [], "changedFiles": ["x"],
            "requiredCommandIds": ["task-verification"],
            "allowedEvidenceRefs": ["builder.json", "verification.json"],
        }
        self.assertEqual(CompletionEntryGate.from_dict(payload).changed_files, ("x",))
        payload.pop("changedFiles")
        with self.assertRaises(CompletionContractError):
            CompletionEntryGate.from_dict(payload)

    def test_valid_transaction_round_trips(self):
        transaction = CompletionTransaction.create(
            task_id="TASK-0100",
            catalog="commerceos",
            integrated_sha="integrated",
            bookkeeping_sha="bookkeeping",
            completed_path="tasks/commerceos/completed/TASK-0100-spec.md",
            original_task_path="tasks/commerceos/backlog/TASK-0100-spec.md",
            evidence_artifact_ids=("integration-output", "verification-report"),
            pre_finalization_lifecycle="Backlog",
            canonical_validation="PASS",
            authoritative_verification="PASS",
            rollback_outcome="NOT_REQUIRED",
            push_eligible=True,
        )
        self.assertEqual(
            CompletionTransaction.from_dict(transaction.to_dict()),
            transaction,
        )

    def test_partial_or_stale_transaction_fails_closed(self):
        payload = CompletionTransaction.create(
            task_id="TASK-0100",
            catalog="commerceos",
            integrated_sha="integrated",
            bookkeeping_sha="bookkeeping",
            completed_path="tasks/commerceos/completed/TASK-0100-spec.md",
            original_task_path="tasks/commerceos/backlog/TASK-0100-spec.md",
            evidence_artifact_ids=("integration-output", "verification-report"),
            pre_finalization_lifecycle="Backlog",
            canonical_validation="PASS",
            authoritative_verification="PASS",
            rollback_outcome="NOT_REQUIRED",
            push_eligible=True,
        ).to_dict()
        payload["authoritativeVerification"] = "STALE"
        with self.assertRaises(CompletionContractError):
            CompletionTransaction.from_dict(payload)


if __name__ == "__main__":
    unittest.main()
