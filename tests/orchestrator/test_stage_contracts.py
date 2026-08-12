from __future__ import annotations

import json
import sqlite3
import tempfile
import unittest
from pathlib import Path

from commerceos_orchestrator.models import TaskExecutionState
from commerceos_orchestrator.stage_contracts import (
    CONTRACT_VERSION,
    STAGE_CONTRACTS,
    TRANSITION_TABLE,
    StageContractError,
    declared_edges,
    transition_rule,
)
from commerceos_orchestrator.state import InvalidTransitionError, RunStateStore


class StageContractTests(unittest.TestCase):
    def test_every_stage_has_one_distinct_versioned_input_and_output(self):
        self.assertEqual(len({contract.stage for contract in STAGE_CONTRACTS}), len(STAGE_CONTRACTS))
        self.assertEqual(len({contract.input_type for contract in STAGE_CONTRACTS}), len(STAGE_CONTRACTS))
        self.assertEqual(len({contract.output_type for contract in STAGE_CONTRACTS}), len(STAGE_CONTRACTS))

        for contract in STAGE_CONTRACTS:
            input_payload = {
                "contract_version": CONTRACT_VERSION,
                "task_id": "TASK-0100",
                "stage": contract.stage,
                "artifact_id": f"TASK-0100:{contract.stage}:input",
                "commit_sha": "abc123",
                "input_artifact_ids": ["TASK-0100:task-spec"],
            }
            output_payload = {
                "contract_version": CONTRACT_VERSION,
                "task_id": "TASK-0100",
                "stage": contract.stage,
                "artifact_id": f"TASK-0100:{contract.stage}:output",
                "success": True,
                "commit_sha": "abc123",
                "evidence_artifact_ids": ["TASK-0100:evidence"],
                "failure_route": None,
            }
            self.assertEqual(contract.input_type.from_dict(input_payload).stage, contract.stage)
            self.assertEqual(contract.output_type.from_dict(output_payload).stage, contract.stage)

            for field in tuple(input_payload):
                malformed = dict(input_payload)
                malformed.pop(field)
                with self.subTest(stage=contract.stage, record="input", missing=field):
                    with self.assertRaises(StageContractError):
                        contract.input_type.from_dict(malformed)
            for field in tuple(output_payload):
                malformed = dict(output_payload)
                malformed.pop(field)
                with self.subTest(stage=contract.stage, record="output", missing=field):
                    with self.assertRaises(StageContractError):
                        contract.output_type.from_dict(malformed)

    def test_transition_table_has_unique_edges_and_covers_every_executable_state(self):
        edges = [(rule.from_state, rule.to_state) for rule in TRANSITION_TABLE]
        self.assertEqual(len(edges), len(set(edges)))
        self.assertEqual(declared_edges(), frozenset(edges))
        for edge in edges:
            self.assertIsNotNone(transition_rule(*edge))
        represented = {state for edge in declared_edges() for state in edge}
        self.assertEqual(represented, set(TaskExecutionState))
        self.assertNotIn(
            (TaskExecutionState.FIRST_REVIEW, TaskExecutionState.INITIAL_BUILD),
            declared_edges(),
        )
        for rule in TRANSITION_TABLE:
            self.assertTrue(rule.actor)
            self.assertTrue(rule.required_input)
            self.assertTrue(rule.required_output)
            self.assertTrue(rule.success_predicate)
            self.assertTrue(rule.terminal_failure_route)

    def test_invalid_transition_fails_closed_and_records_contract_context(self):
        with tempfile.TemporaryDirectory() as td:
            state = RunStateStore(Path(td) / "state.db")
            state.clear_stop_and_run()
            self.assertTrue(state.claim_task("TASK-0100"))
            state.update_task("TASK-0100", TaskExecutionState.INITIAL_BUILD)
            state.update_task("TASK-0100", TaskExecutionState.PRE_REVIEW_VERIFICATION)
            state.update_task("TASK-0100", TaskExecutionState.FIRST_REVIEW)
            with self.assertRaises(InvalidTransitionError):
                state.update_task("TASK-0100", TaskExecutionState.INITIAL_BUILD)
            run = state.task_run("TASK-0100")
            self.assertEqual(run.execution_state, TaskExecutionState.HUMAN_REQUIRED)
            self.assertEqual(run.blocker_code, "INVALID_TRANSITION")
            rejected = next(
                event for event in state.recent_events(20) if event["kind"] == "TRANSITION_REJECTED"
            )
            detail = json.loads(rejected["detail"])
            self.assertEqual(detail["contract_version"], CONTRACT_VERSION)
            self.assertEqual(detail["from"], "FIRST_REVIEW")
            self.assertEqual(detail["to"], "INITIAL_BUILD")

    def test_additive_migration_preserves_legacy_task_run(self):
        with tempfile.TemporaryDirectory() as td:
            path = Path(td) / "state.db"
            connection = sqlite3.connect(path)
            connection.executescript(
                """
                CREATE TABLE control_state (id INTEGER PRIMARY KEY, state TEXT NOT NULL, updated_at TEXT NOT NULL);
                INSERT INTO control_state VALUES (1, 'RUNNING', 'before');
                CREATE TABLE task_runs (
                    task_id TEXT PRIMARY KEY, execution_state TEXT NOT NULL, branch TEXT,
                    worktree TEXT, attempt INTEGER NOT NULL, fix_attempt INTEGER NOT NULL,
                    blocker_code TEXT, blocker_detail TEXT, activated_at TEXT, updated_at TEXT NOT NULL,
                    drain_at_stop INTEGER NOT NULL
                );
                INSERT INTO task_runs VALUES (
                    'TASK-0100', 'REVIEWING', 'agent/task', 'worktree', 2, 1,
                    NULL, NULL, 'before', 'before', 0
                );
                CREATE TABLE events (
                    id INTEGER PRIMARY KEY AUTOINCREMENT, task_id TEXT, kind TEXT NOT NULL,
                    detail TEXT NOT NULL, created_at TEXT NOT NULL
                );
                """
            )
            connection.commit()
            connection.close()

            state = RunStateStore(path)
            run = state.task_run("TASK-0100")
            self.assertEqual(run.execution_state, TaskExecutionState.FIRST_REVIEW)
            self.assertEqual(run.branch, "agent/task")
            self.assertEqual(run.attempt, 2)
            self.assertEqual(run.fix_attempt, 1)
            self.assertEqual(run.contract_version, CONTRACT_VERSION)

    def test_accepted_transition_persists_artifact_chain(self):
        with tempfile.TemporaryDirectory() as td:
            state = RunStateStore(Path(td) / "state.db")
            state.clear_stop_and_run()
            state.claim_task("TASK-0100")
            queued = state.task_run("TASK-0100")
            state.update_task(
                "TASK-0100",
                TaskExecutionState.INITIAL_BUILD,
                actor="BUILDER",
                output_artifact_id="TASK-0100:builder-output:1",
            )
            run = state.task_run("TASK-0100")
            self.assertEqual(run.input_artifact_id, queued.output_artifact_id)
            self.assertEqual(run.output_artifact_id, "TASK-0100:builder-output:1")
            event = next(event for event in state.recent_events(10) if event["kind"] == "TASK_STATE")
            detail = json.loads(event["detail"])
            self.assertEqual(detail["task_id"], "TASK-0100")
            self.assertIn("input_artifact_id", detail)
            self.assertIn("output_artifact_id", detail)
            detail = json.loads(event["detail"])
            self.assertEqual(detail["actor"], "BUILDER")
            self.assertEqual(detail["contract_version"], CONTRACT_VERSION)

    def test_each_accepted_or_rejected_transition_emits_one_complete_audit_event(self):
        with tempfile.TemporaryDirectory() as td:
            state = RunStateStore(Path(td) / "state.db")
            state.clear_stop_and_run()
            state.claim_task("TASK-0100")
            before = state.recent_events(100)
            state.update_task(
                "TASK-0100", TaskExecutionState.INITIAL_BUILD, actor="BUILDER",
                input_artifact_id="input-1", output_artifact_id="output-1",
            )
            accepted = [
                event for event in state.recent_events(100)
                if event["kind"] == "TASK_STATE" and event not in before
            ]
            self.assertEqual(len(accepted), 1)
            accepted_detail = json.loads(accepted[0]["detail"])
            self.assertEqual(
                set(("task_id", "from", "to", "actor", "contract_version", "input_artifact_id", "output_artifact_id"))
                - set(accepted_detail),
                set(),
            )
            with self.assertRaises(InvalidTransitionError):
                state.update_task(
                    "TASK-0100", TaskExecutionState.PRE_REVIEW_VERIFICATION,
                    actor="BUILDER",
                )
            rejected = [
                event for event in state.recent_events(100)
                if event["kind"] == "TRANSITION_REJECTED"
            ]
            self.assertEqual(len(rejected), 1)
            rejected_detail = json.loads(rejected[0]["detail"])
            for field in (
                "task_id", "from", "to", "actor", "contract_version",
                "input_artifact_id", "output_artifact_id",
            ):
                self.assertIn(field, rejected_detail)

    def test_claim_and_every_declared_transition_have_complete_single_audits(self):
        with tempfile.TemporaryDirectory() as td:
            path = Path(td) / "state.db"
            state = RunStateStore(path)
            state.clear_stop_and_run()
            self.assertTrue(state.claim_task("TASK-0100"))
            claims = [event for event in state.recent_events(20) if event["kind"] == "CLAIMED"]
            self.assertEqual(len(claims), 1)
            self.assertEqual(json.loads(claims[0]["detail"])["from"], "ABSENT")
            required = {
                "task_id", "from", "to", "actor", "contract_version",
                "input_artifact_id", "output_artifact_id",
            }
            self.assertTrue(required.issubset(json.loads(claims[0]["detail"])))

            for index, rule in enumerate(TRANSITION_TABLE):
                with self.subTest(edge=f"{rule.from_state}->{rule.to_state}"):
                    connection = sqlite3.connect(path)
                    connection.execute(
                        "UPDATE task_runs SET execution_state = ?, output_artifact_id = ? WHERE task_id = ?",
                        (rule.from_state.value, f"source-{index}", "TASK-0100"),
                    )
                    connection.commit()
                    connection.close()
                    before = len([
                        event for event in state.recent_events(1000) if event["kind"] == "TASK_STATE"
                    ])
                    state.update_task(
                        "TASK-0100", rule.to_state, actor=rule.actor,
                        input_artifact_id=f"input-{index}", output_artifact_id=f"output-{index}",
                    )
                    accepted = [
                        event for event in state.recent_events(1000) if event["kind"] == "TASK_STATE"
                    ]
                    self.assertEqual(len(accepted), before + 1)
                    self.assertTrue(required.issubset(json.loads(accepted[0]["detail"])))

    def test_every_declared_edge_rejects_the_wrong_actor_once(self):
        with tempfile.TemporaryDirectory() as td:
            path = Path(td) / "state.db"
            state = RunStateStore(path)
            state.clear_stop_and_run()
            state.claim_task("TASK-0100")
            for rule in TRANSITION_TABLE:
                with self.subTest(edge=f"{rule.from_state}->{rule.to_state}"):
                    connection = sqlite3.connect(path)
                    connection.execute(
                        "UPDATE task_runs SET execution_state = ? WHERE task_id = ?",
                        (rule.from_state.value, "TASK-0100"),
                    )
                    connection.commit()
                    connection.close()
                    before = len([
                        event for event in state.recent_events(1000)
                        if event["kind"] == "TRANSITION_REJECTED"
                    ])
                    with self.assertRaises(InvalidTransitionError):
                        state.update_task("TASK-0100", rule.to_state, actor="WRONG_ACTOR")
                    rejected = [
                        event for event in state.recent_events(1000)
                        if event["kind"] == "TRANSITION_REJECTED"
                    ]
                    self.assertEqual(len(rejected), before + 1)
                    self.assertEqual(json.loads(rejected[0]["detail"])["actor"], "WRONG_ACTOR")

    def test_workflow_and_role_docs_reference_the_executable_contract(self):
        root = Path(__file__).resolve().parents[2]
        workflow = (root / "docs/development/16-task-orchestrator.md").read_text(encoding="utf-8")
        roles = (root / "docs/development/17-review-scope-and-finding-ownership.md").read_text(
            encoding="utf-8"
        )
        for document in (workflow, roles):
            self.assertIn(CONTRACT_VERSION, document)
            for contract in STAGE_CONTRACTS:
                self.assertIn(f"`{contract.stage}`", document)


if __name__ == "__main__":
    unittest.main()
