import { describe, expect, it } from 'vitest'
import { addToCart, estimatedTotal, removeFromCart, setQuantity } from './cart'

describe('tenant cart', () => {
  it('does not silently mix tenant storefronts', () => {
    const cart = addToCart(null, 'one', { productId: 'p1', name: 'One', unitPriceVnd: 1, quantity: 1 })
    expect(addToCart(cart, 'two', { productId: 'p2', name: 'Two', unitPriceVnd: 2, quantity: 1 })).toEqual({ storefrontSlug: 'two', lines: [{ productId: 'p2', name: 'Two', unitPriceVnd: 2, quantity: 1 }] })
  })
  it('supports deterministic quantity and removal', () => {
    const cart = addToCart(null, 'one', { productId: 'p1', name: 'One', unitPriceVnd: 3, quantity: 1 })
    expect(estimatedTotal(setQuantity(cart, 'p1', 2))).toBe(6)
    expect(removeFromCart(cart, 'p1').lines).toHaveLength(0)
    expect(() => setQuantity(cart, 'p1', 1.5)).toThrow()
  })
})
