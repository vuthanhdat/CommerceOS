from __future__ import annotations

from dataclasses import asdict, dataclass
from typing import Any, ClassVar

from .models import TaskExecutionState


CONTRACT_VERSION = "commerceos.orchestrator.stage/v1"


class StageContractError(ValueError):
    pass


@dataclass(frozen=True)
class StageInput:
    contract_version: str
    task_id: str
    stage: str
    artifact_id: str
    commit_sha: str
    input_artifact_ids: tuple[str, ...]

    stage_name: ClassVar[str]

    def validate(self) -> None:
        _validate_common(self, self.stage_name)
        _require_non_empty("commit_sha", self.commit_sha)
        _validate_artifact_ids("input_artifact_ids", self.input_artifact_ids)

    def to_dict(self) -> dict[str, Any]:
        self.validate()
        return asdict(self)

    @classmethod
    def from_dict(cls, payload: dict[str, Any]) -> StageInput:
        values = _required_payload(
            payload,
            ("contract_version", "task_id", "stage", "artifact_id", "commit_sha", "input_artifact_ids"),
        )
        values["input_artifact_ids"] = _as_tuple(values["input_artifact_ids"], "input_artifact_ids")
        record = cls(**values)
        record.validate()
        return record


@dataclass(frozen=True)
class StageOutput:
    contract_version: str
    task_id: str
    stage: str
    artifact_id: str
    success: bool
    commit_sha: str
    evidence_artifact_ids: tuple[str, ...]
    failure_route: str | None

    stage_name: ClassVar[str]

    def validate(self) -> None:
        _validate_common(self, self.stage_name)
        if not isinstance(self.success, bool):
            raise StageContractError("success must be a boolean")
        _require_non_empty("commit_sha", self.commit_sha)
        _validate_artifact_ids("evidence_artifact_ids", self.evidence_artifact_ids)
        if self.success and self.failure_route is not None:
            raise StageContractError("successful stage output cannot declare a failure route")
        if not self.success and self.failure_route not in {
            TaskExecutionState.REPAIR_REQUIRED.value,
            TaskExecutionState.PLANNING_REQUIRED.value,
            TaskExecutionState.ORCHESTRATOR_ACTION_REQUIRED.value,
            TaskExecutionState.HUMAN_REQUIRED.value,
            TaskExecutionState.BLOCKED.value,
        }:
            raise StageContractError("failed stage output requires a named failure route")

    def to_dict(self) -> dict[str, Any]:
        self.validate()
        return asdict(self)

    @classmethod
    def from_dict(cls, payload: dict[str, Any]) -> StageOutput:
        values = _required_payload(
            payload,
            (
                "contract_version",
                "task_id",
                "stage",
                "artifact_id",
                "success",
                "commit_sha",
                "evidence_artifact_ids",
                "failure_route",
            ),
        )
        values["evidence_artifact_ids"] = _as_tuple(
            values["evidence_artifact_ids"], "evidence_artifact_ids"
        )
        record = cls(**values)
        record.validate()
        return record


@dataclass(frozen=True)
class BuilderInput(StageInput):
    stage_name: ClassVar[str] = "builder"


@dataclass(frozen=True)
class BuilderOutput(StageOutput):
    stage_name: ClassVar[str] = "builder"


@dataclass(frozen=True)
class PlanningInput(StageInput):
    stage_name: ClassVar[str] = "planning"


@dataclass(frozen=True)
class PlanningOutput(StageOutput):
    stage_name: ClassVar[str] = "planning"


@dataclass(frozen=True)
class VerificationInput(StageInput):
    stage_name: ClassVar[str] = "verification"


@dataclass(frozen=True)
class VerificationOutput(StageOutput):
    stage_name: ClassVar[str] = "verification"


@dataclass(frozen=True)
class ReviewerInput(StageInput):
    stage_name: ClassVar[str] = "reviewer"


@dataclass(frozen=True)
class ReviewerOutput(StageOutput):
    stage_name: ClassVar[str] = "reviewer"


@dataclass(frozen=True)
class RepairBuilderInput(StageInput):
    stage_name: ClassVar[str] = "repair_builder"


@dataclass(frozen=True)
class RepairBuilderOutput(StageOutput):
    stage_name: ClassVar[str] = "repair_builder"


@dataclass(frozen=True)
class IntegrationInput(StageInput):
    stage_name: ClassVar[str] = "integration"


@dataclass(frozen=True)
class IntegrationOutput(StageOutput):
    stage_name: ClassVar[str] = "integration"


@dataclass(frozen=True)
class FinalizationInput(StageInput):
    stage_name: ClassVar[str] = "finalization"


@dataclass(frozen=True)
class FinalizationOutput(StageOutput):
    stage_name: ClassVar[str] = "finalization"


@dataclass(frozen=True)
class StageContract:
    stage: str
    actor: str
    input_type: type[StageInput]
    output_type: type[StageOutput]


STAGE_CONTRACTS: tuple[StageContract, ...] = (
    StageContract("planning", "BACKLOG_PLANNER", PlanningInput, PlanningOutput),
    StageContract("builder", "BUILDER", BuilderInput, BuilderOutput),
    StageContract("verification", "VERIFICATION_RUNNER", VerificationInput, VerificationOutput),
    StageContract("reviewer", "REVIEWER", ReviewerInput, ReviewerOutput),
    StageContract("repair_builder", "REPAIR_BUILDER", RepairBuilderInput, RepairBuilderOutput),
    StageContract("integration", "ORCHESTRATOR", IntegrationInput, IntegrationOutput),
    StageContract("finalization", "ORCHESTRATOR", FinalizationInput, FinalizationOutput),
)

_CONTRACT_BY_STAGE = {contract.stage: contract for contract in STAGE_CONTRACTS}


def stage_contract(stage: str) -> StageContract:
    try:
        return _CONTRACT_BY_STAGE[stage]
    except KeyError as exc:
        raise StageContractError(f"unknown stage: {stage!r}") from exc


@dataclass(frozen=True)
class TransitionRule:
    from_state: TaskExecutionState
    to_state: TaskExecutionState
    actor: str
    required_input: str
    required_output: str
    success_predicate: str
    retry_route: str
    terminal_failure_route: str


def _rule(
    source: TaskExecutionState,
    target: TaskExecutionState,
    actor: str,
    input_kind: str,
    output_kind: str,
    predicate: str,
    retry: str = "none",
    failure: str = "HUMAN_REQUIRED",
) -> TransitionRule:
    return TransitionRule(source, target, actor, input_kind, output_kind, predicate, retry, failure)


_SUCCESS_TRANSITIONS: tuple[TransitionRule, ...] = (
    _rule(TaskExecutionState.QUEUED, TaskExecutionState.PLANNING, "BACKLOG_PLANNER", "planning_input", "planning_output", "dependency-satisfied planning candidate selected"),
    _rule(TaskExecutionState.PLANNING, TaskExecutionState.PLANNING_COMPLETED, "ORCHESTRATOR", "planning_output", "canonical_ready_snapshot", "verified planning artifacts integrated"),
    _rule(TaskExecutionState.QUEUED, TaskExecutionState.INITIAL_BUILD, "BUILDER", "builder_input", "builder_output", "valid builder input"),
    _rule(TaskExecutionState.INITIAL_BUILD, TaskExecutionState.PRE_REVIEW_VERIFICATION, "VERIFICATION_RUNNER", "builder_output", "verification_output", "builder output valid and successful"),
    _rule(TaskExecutionState.PRE_REVIEW_VERIFICATION, TaskExecutionState.FIRST_REVIEW, "REVIEWER", "verification_output", "reviewer_output", "verification successful"),
    _rule(TaskExecutionState.PRE_REVIEW_VERIFICATION, TaskExecutionState.REPAIR_REQUIRED, "ORCHESTRATOR", "verification_output", "repair_route", "verification failed within retry budget", "REPAIR_BUILD"),
    _rule(TaskExecutionState.FIRST_REVIEW, TaskExecutionState.MERGE_QUEUED, "ORCHESTRATOR", "reviewer_output", "merge_input", "review passed with no open blocking findings"),
    _rule(TaskExecutionState.FIRST_REVIEW, TaskExecutionState.REPAIR_REQUIRED, "ORCHESTRATOR", "reviewer_output", "repair_route", "open Builder-owned findings", "REPAIR_BUILD"),
    _rule(TaskExecutionState.REPAIR_REQUIRED, TaskExecutionState.REPAIR_BUILD, "REPAIR_BUILDER", "repair_builder_input", "repair_builder_output", "valid repair packet"),
    _rule(TaskExecutionState.REPAIR_BUILD, TaskExecutionState.REPAIR_VERIFICATION, "VERIFICATION_RUNNER", "repair_builder_output", "verification_output", "repair output valid and successful"),
    _rule(TaskExecutionState.REPAIR_VERIFICATION, TaskExecutionState.RE_REVIEW, "REVIEWER", "verification_output", "reviewer_output", "repair verification successful"),
    _rule(TaskExecutionState.REPAIR_VERIFICATION, TaskExecutionState.REPAIR_REQUIRED, "ORCHESTRATOR", "verification_output", "repair_route", "verification failed within retry budget", "REPAIR_BUILD"),
    _rule(TaskExecutionState.RE_REVIEW, TaskExecutionState.MERGE_QUEUED, "ORCHESTRATOR", "reviewer_output", "merge_input", "re-review passed with no open blocking findings"),
    _rule(TaskExecutionState.RE_REVIEW, TaskExecutionState.REPAIR_REQUIRED, "ORCHESTRATOR", "reviewer_output", "repair_route", "open Builder-owned findings within retry budget", "REPAIR_BUILD"),
    _rule(TaskExecutionState.MERGE_QUEUED, TaskExecutionState.INTEGRATING, "ORCHESTRATOR", "integration_input", "integration_output", "serialized merge lane acquired"),
    _rule(TaskExecutionState.INTEGRATING, TaskExecutionState.FINALIZING, "ORCHESTRATOR", "integration_output", "finalization_input", "merge and post-integration verification successful"),
    _rule(TaskExecutionState.FINALIZING, TaskExecutionState.COMPLETED, "ORCHESTRATOR", "finalization_input", "finalization_output", "canonical finalization verified and pushed"),
)


ROUTED_STATES = (
    TaskExecutionState.PLANNING_REQUIRED,
    TaskExecutionState.ORCHESTRATOR_ACTION_REQUIRED,
    TaskExecutionState.HUMAN_REQUIRED,
    TaskExecutionState.BLOCKED,
)

_FAILURE_SOURCES = tuple(
    state
    for state in TaskExecutionState
    if state
    not in {
        TaskExecutionState.PLANNING_COMPLETED,
        TaskExecutionState.COMPLETED,
        *ROUTED_STATES,
    }
)

_FAILURE_TRANSITIONS: tuple[TransitionRule, ...] = tuple(
    _rule(
        source,
        target,
        "ORCHESTRATOR",
        "stage_failure",
        "route_decision",
        f"validated route to {target.value}",
        failure=target.value,
    )
    for source in _FAILURE_SOURCES
    for target in ROUTED_STATES
    if (source, target)
    not in {(rule.from_state, rule.to_state) for rule in _SUCCESS_TRANSITIONS}
)

# This is the sole production transition inventory. Generated failure rows are materialized in
# the tuple so runtime lookup and structural coverage inspect the same canonical table.
TRANSITION_TABLE: tuple[TransitionRule, ...] = _SUCCESS_TRANSITIONS + _FAILURE_TRANSITIONS

_RULE_BY_EDGE = {(rule.from_state, rule.to_state): rule for rule in TRANSITION_TABLE}


def transition_rule(
    source: TaskExecutionState, target: TaskExecutionState
) -> TransitionRule | None:
    if source == target:
        return None
    rule = _RULE_BY_EDGE.get((source, target))
    if rule is not None:
        return rule
    return None


def declared_edges() -> frozenset[tuple[TaskExecutionState, TaskExecutionState]]:
    return frozenset(_RULE_BY_EDGE)


def _validate_common(record: StageInput | StageOutput, expected_stage: str) -> None:
    if record.contract_version != CONTRACT_VERSION:
        raise StageContractError(f"unsupported contract version: {record.contract_version!r}")
    _require_non_empty("task_id", record.task_id)
    if not record.task_id.startswith("TASK-"):
        raise StageContractError("task_id must use TASK- prefix")
    if record.stage != expected_stage:
        raise StageContractError(f"stage must be {expected_stage!r}")
    _require_non_empty("artifact_id", record.artifact_id)


def _required_payload(payload: dict[str, Any], fields: tuple[str, ...]) -> dict[str, Any]:
    if not isinstance(payload, dict):
        raise StageContractError("stage payload must be an object")
    missing = [field for field in fields if field not in payload]
    if missing:
        raise StageContractError(f"missing required fields: {', '.join(missing)}")
    unknown = sorted(set(payload) - set(fields))
    if unknown:
        raise StageContractError(f"unknown fields: {', '.join(unknown)}")
    return {field: payload[field] for field in fields}


def _as_tuple(value: Any, field: str) -> tuple[str, ...]:
    if not isinstance(value, (list, tuple)):
        raise StageContractError(f"{field} must be a list")
    return tuple(value)


def _validate_artifact_ids(field: str, values: tuple[str, ...]) -> None:
    if not values:
        raise StageContractError(f"{field} must contain at least one artifact id")
    if any(not isinstance(value, str) or not value.strip() for value in values):
        raise StageContractError(f"{field} contains an invalid artifact id")
    if len(values) != len(set(values)):
        raise StageContractError(f"{field} contains duplicate artifact ids")


def _require_non_empty(field: str, value: Any) -> None:
    if not isinstance(value, str) or not value.strip():
        raise StageContractError(f"{field} must be a non-empty string")
