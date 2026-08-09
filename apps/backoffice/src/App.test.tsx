import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it } from 'vitest'
import { App } from './App'

describe('Back Office app', () => {
  it('identifies the merchant-facing surface', () => {
    expect(renderToStaticMarkup(<App />)).toContain('Back Office foundation')
  })
})

