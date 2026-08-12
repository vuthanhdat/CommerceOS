import unittest

from commerceos_orchestrator.repair_contract import RepairContractError, RepairManifest, RepairPacket
from commerceos_orchestrator.review_contract import FindingOwner, FindingRoute, ReviewFinding, ReviewLedger


class RepairContractTests(unittest.TestCase):
    def _packet(self):
        ledger = ReviewLedger(
            "TASK-0100", "base", "INITIAL", (("AC01", "FAIL"),), (("src/x.py", "IN_SCOPE"),),
            (
                ReviewFinding("F-001", "OPEN", FindingOwner.BUILDER, FindingRoute.BUILDER_FIX, "fix x", "MEDIUM", ("evidence",), ("src/**",), "tests pass"),
                ReviewFinding("F-002", "FOLLOW_UP", FindingOwner.BUILDER, FindingRoute.BUILDER_FIX, "later", "LOW", ("evidence",), ("docs/**",), "document later"),
            ), "FIX_REQUIRED",
        )
        return RepairPacket.from_ledger(ledger, "ledger.json")

    def _manifest(self):
        return {
            "contractVersion": "RepairManifest/v1", "taskId": "TASK-0100",
            "baselineSha": "base", "repairedSha": "head",
            "findingDispositions": [{"findingId": "F-001", "disposition": "ADDRESSED"}],
            "changedFiles": [{"path": "src/x.py", "findingIds": ["F-001"]}],
        }

    def test_packet_contains_only_open_builder_findings(self):
        self.assertEqual([item.finding_id for item in self._packet().findings], ["F-001"])

    def test_manifest_exactly_maps_delta_to_packet_findings(self):
        manifest = RepairManifest.from_dict(self._manifest(), packet=self._packet(), repaired_sha="head", repair_delta=("src/x.py",))
        self.assertEqual(manifest.changed_files[0][0], "src/x.py")
        self.assertTrue(RepairManifest._matches("tests/unit/test_x.py", "tests/**"))
        self.assertTrue(RepairManifest._matches("src/x.py", "src/*.py"))
        self.assertFalse(RepairManifest._matches("src/nested/x.py", "src/*.py"))
        self.assertFalse(RepairManifest._matches("src/x.py", "../src/**"))
        self.assertFalse(RepairManifest._matches("src/x.py", "/src/**"))

    def test_invalid_scope_ids_coverage_and_disposition_fail_closed(self):
        mutations = []
        unknown = self._manifest(); unknown["changedFiles"][0]["findingIds"] = ["F-999"]; mutations.append(unknown)
        unmatched = self._manifest(); unmatched["changedFiles"][0]["path"] = "docs/x.md"; mutations.append(unmatched)
        traversal = self._manifest(); traversal["changedFiles"][0]["path"] = "../x"; mutations.append(traversal)
        duplicate = self._manifest(); duplicate["findingDispositions"] *= 2; mutations.append(duplicate)
        blocked = self._manifest(); blocked["findingDispositions"][0]["disposition"] = "BLOCKED"; mutations.append(blocked)
        repeated = self._manifest(); repeated["changedFiles"][0]["findingIds"] *= 2; mutations.append(repeated)
        unmapped = self._manifest(); unmapped["changedFiles"] = []; mutations.append(unmapped)
        for value in mutations:
            with self.subTest(value=value), self.assertRaises(RepairContractError):
                delta = tuple(row["path"] for row in value["changedFiles"])
                RepairManifest.from_dict(value, packet=self._packet(), repaired_sha="head", repair_delta=delta)


if __name__ == "__main__":
    unittest.main()
