import { useEffect, useRef, type ReactNode } from 'react'
import { ApiError } from './api'
import { AppLink } from './router'

export interface NavigationItem {
  label: string
  href: string
  current?: boolean
}

export function AppShell({ brand, navigation, children }: { brand: string; navigation: NavigationItem[]; children: ReactNode }) {
  return (
    <div className="co-app-shell">
      <a className="co-skip-link" href="#main-content">Bỏ qua điều hướng</a>
      <header className="co-app-header">
        <AppLink className="co-brand" to="/">{brand}</AppLink>
        <nav aria-label="Điều hướng chính">
          {navigation.map((item) => <AppLink key={item.href} to={item.href} aria-current={item.current ? 'page' : undefined}>{item.label}</AppLink>)}
        </nav>
      </header>
      <main id="main-content" className="co-page-content">{children}</main>
    </div>
  )
}

export function AlertPanel({ error, title }: { error: unknown; title?: string }) {
  const message = error instanceof ApiError ? error.message : 'Đã xảy ra lỗi không mong muốn. Hãy thử lại.'
  return <section className="co-alert" role="alert"><strong>{title ?? 'Không thể hoàn tất'}</strong><p>{message}</p></section>
}

export function PageState({ kind, action }: { kind: 'loading' | 'empty' | 'forbidden' | 'not-found' | 'service-unavailable'; action?: ReactNode }) {
  const content = {
    loading: ['Đang tải', 'Nội dung đang được tải.'],
    empty: ['Chưa có dữ liệu', 'Hãy tạo bản ghi đầu tiên để bắt đầu.'],
    forbidden: ['Bạn không có quyền truy cập', 'Tài khoản hiện tại không được phép xem nội dung này.'],
    'not-found': ['Không tìm thấy nội dung', 'Đường dẫn có thể không còn tồn tại.'],
    'service-unavailable': ['Dịch vụ tạm thời không khả dụng', 'Hãy thử lại sau ít phút.'],
  }[kind]
  return <section className={`co-page-state co-page-state-${kind}`} aria-live="polite"><h1>{content[0]}</h1><p>{content[1]}</p>{action}</section>
}

export function StatusBadge({ children, tone = 'neutral' }: { children: ReactNode; tone?: 'neutral' | 'success' | 'warning' | 'danger' }) {
  return <span className={`co-status co-status-${tone}`}>{children}</span>
}

export function FormField({ label, hint, error, children }: { label: string; hint?: string; error?: string; children: ReactNode }) {
  return <label className="co-field"><span>{label}</span>{children}{hint && <small>{hint}</small>}{error && <small className="co-field-error" role="alert">{error}</small>}</label>
}

export function LoadMore({ disabled, onClick }: { disabled?: boolean; onClick: () => void }) {
  return <button className="co-button co-button-secondary" type="button" disabled={disabled} onClick={onClick}>Tải thêm</button>
}

export function Dialog({ title, children, onClose }: { title: string; children: ReactNode; onClose: () => void }) {
  const dialogRef = useRef<HTMLDivElement>(null)
  useEffect(() => {
    dialogRef.current?.focus()
  }, [])
  return (
    <div className="co-dialog-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) onClose() }}>
      <div ref={dialogRef} className="co-dialog" role="dialog" aria-modal="true" aria-labelledby="co-dialog-title" tabIndex={-1} onKeyDown={(event) => { if (event.key === 'Escape') onClose() }}>
        <div className="co-dialog-heading"><h2 id="co-dialog-title">{title}</h2><button className="co-button co-button-secondary" type="button" onClick={onClose}>Đóng</button></div>
        {children}
      </div>
    </div>
  )
}
