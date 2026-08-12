from __future__ import annotations

import sys
import unittest
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[2] / "tools"
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

from commerceos_orchestrator.completion_contract import (
    CompletionContractError,
    CompletionTransaction,
)


class CompletionTransactionTests(unittest.TestCase):
    def test_valid_transaction_round_trips(self):
        transaction = CompletionTransaction.create(
            task_id="TASK-0100",
            catalog="commerceos",
            integrated_sha="integrated",
            bookkeeping_sha="bookkeeping",
            completed_path="tasks/commerceos/completed/TASK-0100-spec.md",
            original_task_path="tasks/commerceos/backlog/TASK-0100-spec.md",
            evidence_artifact_ids=("integration-output", "verification-report"),
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
        ).to_dict()
        payload["authoritativeVerification"] = "STALE"
        with self.assertRaises(CompletionContractError):
            CompletionTransaction.from_dict(payload)


if __name__ == "__main__":
    unittest.main()
