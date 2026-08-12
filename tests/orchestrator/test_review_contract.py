import unittest

from commerceos_orchestrator.review_contract import (
    FindingOwner,
    FindingRoute,
    next_hop,
    parse_review_findings,
)


class ReviewContractTests(unittest.TestCase):
    def test_domain_and_technical_findings_route_through_backlog_planner(self):
        findings = parse_review_findings(
            """FINDING F-001 STATUS: OPEN OWNER: DOMAIN_ARCHITECT ROUTE: PLANNING_REQUIRED TITLE: invariant conflict
FINDING F-002 STATUS: OPEN OWNER: TECHNICAL_ARCHITECT ROUTE: PLANNING_REQUIRED TITLE: contract conflict
FINDING F-003 STATUS: FOLLOW_UP OWNER: BUILDER ROUTE: BUILDER_FIX TITLE: optional cleanup"""
        )
        self.assertEqual(findings[0].owner, FindingOwner.DOMAIN_ARCHITECT)
        self.assertEqual(findings[1].route, FindingRoute.PLANNING_REQUIRED)
        self.assertEqual(next_hop(findings[0]), "Backlog Planner")
        self.assertEqual(next_hop(findings[1]), "Backlog Planner")
        self.assertEqual(len(findings), 3)

    def test_builder_finding_routes_to_builder(self):
        finding = parse_review_findings(
            "FINDING F-004 STATUS: OPEN OWNER: BUILDER ROUTE: BUILDER_FIX TITLE: missing regression test"
        )[0]
        self.assertEqual(next_hop(finding), "Builder")


if __name__ == "__main__":
    unittest.main()
