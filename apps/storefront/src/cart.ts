export type CartLine = { productId: string; name: string; unitPriceVnd: number; quantity: number }
export type TenantCart = { storefrontSlug: string; lines: CartLine[] }

const validQuantity = (quantity: number) => Number.isSafeInteger(quantity) && quantity > 0

export function addToCart(cart: TenantCart | null, storefrontSlug: string, line: CartLine): TenantCart {
  if (!validQuantity(line.quantity)) throw new Error('Quantity must be a positive whole unit')
  if (cart && cart.storefrontSlug !== storefrontSlug) return { storefrontSlug, lines: [line] }
  const current = cart ?? { storefrontSlug, lines: [] }
  const old = current.lines.find((item) => item.productId === line.productId)
  return old
    ? { ...current, lines: current.lines.map((item) => item.productId === line.productId ? { ...item, quantity: item.quantity + line.quantity } : item) }
    : { ...current, lines: [...current.lines, line] }
}

export function setQuantity(cart: TenantCart, productId: string, quantity: number): TenantCart {
  if (!validQuantity(quantity)) throw new Error('Quantity must be a positive whole unit')
  return { ...cart, lines: cart.lines.map((line) => line.productId === productId ? { ...line, quantity } : line) }
}

export function removeFromCart(cart: TenantCart, productId: string): TenantCart {
  return { ...cart, lines: cart.lines.filter((line) => line.productId !== productId) }
}

export function estimatedTotal(cart: TenantCart): number {
  return cart.lines.reduce((total, line) => total + line.quantity * line.unitPriceVnd, 0)
}
