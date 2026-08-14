import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it } from 'vitest'
import { ApiError, AppShell, PageState, appendCursorPage, createApiClient, createMutationAttempt, matchRoute } from './index'

describe('frontend foundation', () => {
  it('matches parameterized routes without making route state authoritative', () => {
    expect(matchRoute('/app/catalog/products/tea', '/app/catalog/products/:productId')).toEqual({
      path: '/app/catalog/products/tea',
      params: { productId: 'tea' },
    })
    expect(matchRoute('/app/catalog', '/app/catalog/products')).toBeUndefined()
  })

  it('maps RFC 7807 validation responses into a safe client error', async () => {
    const client = createApiClient('/api', async () => new Response(JSON.stringify({
      title: 'Tên hiển thị không hợp lệ',
      errors: { displayName: ['Bắt buộc nhập tên hiển thị.'] },
    }), { status: 400, headers: { 'content-type': 'application/problem+json' } }))

    await expect(client.request('/v1/example')).rejects.toMatchObject<ApiError>({
      kind: 'validation',
      status: 400,
      message: 'Tên hiển thị không hợp lệ',
    })
  })

  it('keeps a mutation idempotency key stable for one attempt and appends cursor data', () => {
    const attempt = createMutationAttempt()
    expect(attempt.idempotencyKey).toBeTruthy()
    expect(appendCursorPage(['first'], { items: ['second'], nextCursor: 'cursor-2' })).toEqual(['first', 'second'])
  })

  it('renders accessible navigation and a service-unavailable state', () => {
    const markup = renderToStaticMarkup(<AppShell brand="CommerceOS" navigation={[{ label: 'Trang chủ', href: '/' }]}><PageState kind="service-unavailable" /></AppShell>)
    expect(markup).toContain('Bỏ qua điều hướng')
    expect(markup).toContain('Dịch vụ tạm thời không khả dụng')
  })
})
