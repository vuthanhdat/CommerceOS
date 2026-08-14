import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it } from 'vitest'
import { App } from './App'

describe('Storefront app', () => {
  it('identifies the customer-facing surface', () => {
    expect(renderToStaticMarkup(<App />)).toContain('Giá và tổng tiền')
  })
})

