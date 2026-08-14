import { useState } from 'react'
import { addToCart, estimatedTotal, removeFromCart, type TenantCart } from './cart'
import './app.css'

export function App() {
  const [cart, setCart] = useState<TenantCart | null>(null)
  const sample = { productId: 'sample-tea', name: 'Trà mẫu', unitPriceVnd: 45000, quantity: 1 }
  return (
    <main className="shell">
      <p className="eyebrow">CommerceOS</p>
      <h1>Storefront</h1>
      <p>
        Giá và tổng tiền trong giỏ chỉ là ước tính. Checkout sẽ xác thực lại sản phẩm, giá và tồn kho.
      </p>
      <button onClick={() => setCart(addToCart(cart, 'demo-store', sample))}>Thêm trà mẫu vào giỏ</button>
      {cart && <section aria-label="Giỏ hàng"><h2>Giỏ hàng</h2>{cart.lines.map((line) => <p key={line.productId}>{line.name} × {line.quantity} — {line.unitPriceVnd * line.quantity} VND <button onClick={() => setCart(removeFromCart(cart, line.productId))}>Xóa</button></p>)}<p>Tạm tính: {estimatedTotal(cart)} VND</p><button disabled={cart.lines.length === 0}>Tiếp tục checkout</button></section>}
    </main>
  )
}

