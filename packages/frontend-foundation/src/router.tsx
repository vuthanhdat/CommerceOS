import { useEffect, useState, type AnchorHTMLAttributes, type MouseEvent, type ReactNode } from 'react'

export interface RouteMatch {
  path: string
  params: Record<string, string>
}

export interface RouteDefinition {
  path: string
  render: (match: RouteMatch) => ReactNode
}

export function matchRoute(pathname: string, pattern: string): RouteMatch | undefined {
  const actual = trimPath(pathname).split('/')
  const expected = trimPath(pattern).split('/')
  if (actual.length !== expected.length) return undefined

  const params: Record<string, string> = {}
  for (let index = 0; index < expected.length; index += 1) {
    const part = expected[index]
    const value = actual[index]
    if (part.startsWith(':')) {
      params[part.slice(1)] = decodeURIComponent(value)
      continue
    }
    if (part !== value) return undefined
  }
  return { path: pathname, params }
}

export function Router({ routes, notFound }: { routes: RouteDefinition[]; notFound: ReactNode }) {
  const [pathname, setPathname] = useState(() => getPathname())

  useEffect(() => {
    const onPopState = () => setPathname(window.location.pathname)
    window.addEventListener('popstate', onPopState)
    return () => window.removeEventListener('popstate', onPopState)
  }, [])

  const found = routes.find((route) => matchRoute(pathname, route.path))
  if (!found) return <>{notFound}</>
  return <>{found.render(matchRoute(pathname, found.path)!)}</>
}

export function navigate(to: string) {
  if (typeof window === 'undefined') return
  window.history.pushState({}, '', to)
  window.dispatchEvent(new PopStateEvent('popstate'))
}

export function AppLink({ to, children, ...props }: { to: string; children: ReactNode } & AnchorHTMLAttributes<HTMLAnchorElement>) {
  const onClick = (event: MouseEvent<HTMLAnchorElement>) => {
    if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return
    event.preventDefault()
    navigate(to)
  }
  return <a href={to} onClick={onClick} {...props}>{children}</a>
}

function trimPath(path: string): string {
  const value = path.replace(/^\/+|\/+$/g, '')
  return value === '' ? '' : value
}

function getPathname(): string {
  return typeof window === 'undefined' ? '/' : window.location.pathname
}
