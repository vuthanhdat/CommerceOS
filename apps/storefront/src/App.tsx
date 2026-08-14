import { useEffect, useState, type ReactNode } from 'react'
import { addToCart, estimatedTotal, removeFromCart, setQuantity, type TenantCart } from './cart'
import { AlertPanel, ApiError, AppLink, AppShell, FormField, LoadMore, PageState, Router, StatusBadge, createMutationAttempt, navigate, type RouteDefinition } from '@commerceos/frontend-foundation'
import { apiClient } from './config'
import './app.css'

interface ProductSpecification { name: string; value: string; unit?: string; displayOrder: number }
interface ProductMedia { assetId: string; altText: string; displayOrder: number }
interface Product { productId: string; slug: string; name: string; sku?: string; basePrice: { amount: number; currency: string }; effectivePrice: { amount: number; currency: string }; promotionId?: string; promotionEffectiveUntil?: string; availableQuantity: number; categoryName?: string; brandName?: string; specifications?: ProductSpecification[]; media?: ProductMedia[] }
interface ProductPage { items: Product[]; nextCursor?: string }
interface CheckoutLine { productId: string; sku: string; name: string; quantity: number; unitPriceVnd: number; currency: string }
interface CheckoutResult { code: string; lines: CheckoutLine[]; totalVnd: number; currency: string }

function formatVnd(value: number) { return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value) }

function Storefront({ slug }: { slug: string }) {
  const [cart, setCart] = useState<TenantCart | null>(null)
  const routes: RouteDefinition[] = [
    { path: '/:storefrontSlug', render: () => <ProductList slug={slug} cart={cart} setCart={setCart} /> },
    { path: '/:storefrontSlug/products/:productSlug', render: (match) => <ProductDetail slug={slug} productSlug={match.params.productSlug} cart={cart} setCart={setCart} /> },
    { path: '/:storefrontSlug/cart', render: () => <CartPage slug={slug} cart={cart} setCart={setCart} /> },
    { path: '/:storefrontSlug/checkout', render: () => <CheckoutPage slug={slug} cart={cart} /> },
    { path: '/:storefrontSlug/order-confirmation/:orderId', render: (match) => <Confirmation orderId={match.params.orderId} /> },
  ]
  return <Router routes={routes} notFound={<PageState kind="not-found" />} />
}

function Frame({ slug, children }: { slug: string; children: ReactNode }) {
  return <AppShell brand="CommerceOS" navigation={[{ label: 'Cửa hàng', href: `/${slug}`, current: true }, { label: 'Giỏ hàng', href: `/${slug}/cart` }]}>{children}</AppShell>
}

function ProductList({ slug, cart, setCart }: { slug: string; cart: TenantCart | null; setCart: (cart: TenantCart | null) => void }) {
  const [page, setPage] = useState<ProductPage>()
  const [search, setSearch] = useState('')
  const [error, setError] = useState<unknown>()
  const load = async (cursor?: string, reset = false) => { try { const next = await apiClient.request<ProductPage>(`/api/v1/storefronts/${encodeURIComponent(slug)}/products?search=${encodeURIComponent(search)}${cursor ? `&cursor=${encodeURIComponent(cursor)}` : ''}`); setPage((current) => reset || !current ? next : { items: [...current.items, ...next.items], nextCursor: next.nextCursor }); setError(undefined) } catch (loadError) { setError(loadError) } }
  useEffect(() => { void load(undefined, true) }, [slug])
  if (error instanceof ApiError && error.status === 404) return <PageState kind="not-found" />
  return <Frame slug={slug}><div className="storefront-heading"><div><p className="eyebrow">Storefront</p><h1>Sản phẩm đang có sẵn</h1></div><StatusBadge tone="neutral">Giá sẽ xác thực lúc checkout</StatusBadge></div><form className="search-form" onSubmit={(event) => { event.preventDefault(); void load(undefined, true) }}><FormField label="Tìm sản phẩm"><input value={search} onChange={(event) => setSearch(event.target.value)} /></FormField><button className="co-button co-button-primary" type="submit">Tìm</button></form>{error !== undefined && <AlertPanel error={error} />}{!page ? <PageState kind="loading" /> : page.items.length === 0 ? <PageState kind="empty" /> : <><div className="product-grid">{page.items.map((product) => <article className="product-card" key={product.productId}><h2><AppLink to={`/${slug}/products/${product.slug}`}>{product.name}</AppLink></h2><p>{product.sku ?? 'Chưa có SKU công khai'}</p>{(product.categoryName || product.brandName) && <small>{[product.categoryName, product.brandName].filter(Boolean).join(' · ')}</small>}<strong>{formatVnd(product.effectivePrice.amount)}</strong>{product.promotionId && <small>Giá khuyến mãi đang áp dụng</small>}<small>{product.availableQuantity > 0 ? `Còn ${product.availableQuantity}` : 'Tạm hết hàng'}</small><button className="co-button co-button-primary" type="button" disabled={product.availableQuantity <= 0} onClick={() => setCart(addToCart(cart, slug, { productId: product.productId, name: product.name, unitPriceVnd: product.effectivePrice.amount, quantity: 1 }))}>Thêm vào giỏ</button></article>)}</div>{page.nextCursor && <LoadMore onClick={() => void load(page.nextCursor)} />}</>}</Frame>
}

function ProductDetail({ slug, productSlug, cart, setCart }: { slug: string; productSlug: string; cart: TenantCart | null; setCart: (cart: TenantCart | null) => void }) {
  const [product, setProduct] = useState<Product>()
  const [quantity, setQuantity] = useState(1)
  const [error, setError] = useState<unknown>()
  useEffect(() => { void apiClient.request<Product>(`/api/v1/storefronts/${encodeURIComponent(slug)}/products/${encodeURIComponent(productSlug)}`).then(setProduct).catch(setError) }, [slug, productSlug])
  if (error instanceof ApiError && error.status === 404) return <PageState kind="not-found" />
  return <Frame slug={slug}>{error !== undefined && <AlertPanel error={error} />}{!product ? <PageState kind="loading" /> : <article className="product-detail"><AppLink to={`/${slug}`}>Quay lại sản phẩm</AppLink>{product.media && product.media.length > 0 && <div className="product-gallery" aria-label="Thư viện sản phẩm">{product.media.map((media) => <div className="product-image-placeholder" key={media.assetId} role="img" aria-label={media.altText || product.name}>{media.altText || 'Ảnh sản phẩm'}</div>)}</div>}<h1>{product.name}</h1><p>SKU: {product.sku ?? 'Không có'}</p>{(product.categoryName || product.brandName) && <p>{[product.categoryName, product.brandName].filter(Boolean).join(' · ')}</p>}<strong>{formatVnd(product.effectivePrice.amount)}</strong>{product.promotionId && <p>Giá khuyến mãi được máy chủ xác định.</p>}<p>{product.availableQuantity > 0 ? `Còn ${product.availableQuantity} sản phẩm` : 'Sản phẩm hiện không có sẵn'}</p>{product.specifications && product.specifications.length > 0 && <dl className="product-specifications">{product.specifications.map((specification) => <div key={`${specification.name}-${specification.displayOrder}`}><dt>{specification.name}</dt><dd>{specification.value}{specification.unit ? ` ${specification.unit}` : ''}</dd></div>)}</dl>}<FormField label="Số lượng"><input type="number" min="1" max={Math.max(1, product.availableQuantity)} value={quantity} onChange={(event) => setQuantity(Math.max(1, Number(event.target.value) || 1))} /></FormField><button className="co-button co-button-primary" type="button" disabled={product.availableQuantity <= 0 || quantity > product.availableQuantity} onClick={() => setCart(addToCart(cart, slug, { productId: product.productId, name: product.name, unitPriceVnd: product.effectivePrice.amount, quantity }))}>Thêm vào giỏ</button></article>}</Frame>
}

function CartPage({ slug, cart, setCart }: { slug: string; cart: TenantCart | null; setCart: (cart: TenantCart | null) => void }) {
  if (!cart || cart.lines.length === 0) return <Frame slug={slug}><PageState kind="empty" action={<AppLink to={`/${slug}`}>Xem sản phẩm</AppLink>} /></Frame>
  return <Frame slug={slug}><h1>Giỏ hàng</h1><p className="lead">Tổng tiền là ước tính. Giá, sản phẩm và tồn kho được xác nhận trước khi đặt hàng.</p><section className="cart-panel" aria-label="Giỏ hàng">{cart.lines.map((line) => <div className="cart-line" key={line.productId}><span>{line.name}</span><FormField label={`Số lượng ${line.name}`}><input type="number" min="1" value={line.quantity} onChange={(event) => setCart(setQuantity(cart, line.productId, Math.max(1, Number(event.target.value) || 1)))} /></FormField><strong>{formatVnd(line.unitPriceVnd * line.quantity)}</strong><button className="co-button co-button-secondary" type="button" onClick={() => setCart(removeFromCart(cart, line.productId))}>Xóa</button></div>)}<p>Tạm tính: {formatVnd(estimatedTotal(cart))}</p><div className="cart-actions"><button className="co-button co-button-secondary" type="button" onClick={() => setCart(null)}>Xóa toàn bộ</button><AppLink className="co-button co-button-primary" to={`/${slug}/checkout`}>Checkout</AppLink></div></section></Frame>
}

function CheckoutPage({ slug, cart }: { slug: string; cart: TenantCart | null }) {
  const [name, setName] = useState(''); const [email, setEmail] = useState(''); const [phone, setPhone] = useState(''); const [address, setAddress] = useState(''); const [result, setResult] = useState<CheckoutResult>(); const [error, setError] = useState<unknown>(); const [placing, setPlacing] = useState(false); const [attempt] = useState(createMutationAttempt)
  if (!cart || cart.lines.length === 0) return <Frame slug={slug}><PageState kind="empty" action={<AppLink to={`/${slug}`}>Xem sản phẩm</AppLink>} /></Frame>
  const request = { lines: cart.lines.map((line) => ({ productId: line.productId, quantity: line.quantity, estimatedUnitPriceVnd: line.unitPriceVnd })), estimatedTotalVnd: estimatedTotal(cart), reconfirmed: Boolean(result), guest: { name, email, phone: phone || null, address: address || null } }
  const validate = async () => { setPlacing(true); try { const response = await apiClient.request<CheckoutResult>(`/api/v1/storefronts/${encodeURIComponent(slug)}/checkout/validate`, { method: 'POST', body: request, idempotencyKey: attempt.idempotencyKey }); setResult(response); setError(undefined) } catch (checkoutError) { if (checkoutError instanceof ApiError && checkoutError.status === 409 && isCheckoutResult(checkoutError.body) && checkoutError.body.code === 'CHECKOUT_RECONFIRMATION_REQUIRED') { setResult(checkoutError.body); setError(undefined) } else setError(checkoutError) } finally { setPlacing(false) } }
  const place = async () => { setPlacing(true); try { const confirmation = await apiClient.request<OrderConfirmation>(`/api/v1/storefronts/${encodeURIComponent(slug)}/orders`, { method: 'POST', body: request, idempotencyKey: attempt.idempotencyKey }); sessionStorage.setItem(orderConfirmationKey(confirmation.orderId), JSON.stringify(confirmation)); navigate(`/${slug}/order-confirmation/${confirmation.orderId}`) } catch (placeError) { setError(placeError) } finally { setPlacing(false) } }
  return <Frame slug={slug}><h1>Checkout</h1><form className="checkout-form" onSubmit={(event) => { event.preventDefault(); void (result ? place() : validate()) }}><FormField label="Họ tên"><input required value={name} onChange={(event) => setName(event.target.value)} /></FormField><FormField label="Email"><input required type="email" value={email} onChange={(event) => setEmail(event.target.value)} /></FormField><FormField label="Số điện thoại"><input value={phone} onChange={(event) => setPhone(event.target.value)} /></FormField><FormField label="Địa chỉ"><textarea value={address} onChange={(event) => setAddress(event.target.value)} /></FormField>{result && <section className="reconfirmation" aria-live="polite"><strong>Giá đã được xác nhận: {formatVnd(result.totalVnd)}</strong><p>Hãy xác nhận lại để đặt hàng.</p></section>}{error !== undefined && <AlertPanel error={error} />}<button className="co-button co-button-primary" type="submit" disabled={placing}>{result ? 'Xác nhận đặt hàng' : 'Xác thực checkout'}</button></form></Frame>
}

interface OrderConfirmation { orderId: string; status: string; lines: CheckoutLine[]; totalVnd: number; currency: string }
function orderConfirmationKey(orderId: string) { return `commerceos.order-confirmation.${orderId}` }
function Confirmation({ orderId }: { orderId: string }) {
  const raw = typeof sessionStorage === 'undefined' ? null : sessionStorage.getItem(orderConfirmationKey(orderId))
  let confirmation: OrderConfirmation | undefined
  try { confirmation = raw ? JSON.parse(raw) as OrderConfirmation : undefined } catch { confirmation = undefined }
  return <main className="confirmation"><h1>Đã tiếp nhận đơn hàng</h1><p>Mã đơn: {orderId}</p>{confirmation ? <><StatusBadge tone="success">{confirmation.status}</StatusBadge><ul>{confirmation.lines.map((line) => <li key={line.productId}>{line.name} × {line.quantity} — {formatVnd(line.unitPriceVnd * line.quantity)}</li>)}</ul><p>Tổng cộng: {formatVnd(confirmation.totalVnd)}</p></> : <p>Thông tin xác nhận này chỉ có trên thiết bị của lần đặt hàng vừa hoàn tất. Vui lòng lưu mã đơn của bạn.</p>}</main>
}
function isCheckoutResult(value: unknown): value is CheckoutResult { return typeof value === 'object' && value !== null && 'code' in value && 'totalVnd' in value && 'lines' in value }

const routes: RouteDefinition[] = [
  { path: '/', render: () => <PageState kind="not-found" /> },
  { path: '/:storefrontSlug', render: (match) => <Storefront slug={match.params.storefrontSlug} /> },
  { path: '/:storefrontSlug/products/:productSlug', render: (match) => <Storefront slug={match.params.storefrontSlug} /> },
  { path: '/:storefrontSlug/cart', render: (match) => <Storefront slug={match.params.storefrontSlug} /> },
  { path: '/:storefrontSlug/checkout', render: (match) => <Storefront slug={match.params.storefrontSlug} /> },
  { path: '/:storefrontSlug/order-confirmation/:orderId', render: (match) => <Storefront slug={match.params.storefrontSlug} /> },
]
export function App() { return <Router routes={routes} notFound={<PageState kind="not-found" />} /> }
