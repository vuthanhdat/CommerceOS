import './app.css'

export type BackOfficeRole = 'Owner' | 'Admin' | 'Staff' | 'Viewer'

export function canViewFinancialReports(role: BackOfficeRole) {
  return role === 'Owner' || role === 'Admin'
}

export function App() {
  return (
    <main className="shell">
      <p className="eyebrow">CommerceOS</p>
      <h1>Back Office foundation — Financial overview</h1>
      <p>Read-only views from Accounting. Financial periods use journal effective date.</p>
      <section className="report-card" aria-label="Operational gross sales">
        <span>Operational Gross Sales</span>
        <strong>Not accounting revenue</strong>
        <small>Confirmed-order projection freshness is shown with the report response.</small>
      </section>
      <section className="report-card" aria-label="General ledger">
        <span>General Ledger &amp; Trial Balance</span>
        <strong>Owner and Admin only</strong>
        <small>Posted journals are immutable; this screen has no edit action.</small>
      </section>
    </main>
  )
}

