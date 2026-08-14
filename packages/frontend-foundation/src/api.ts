export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  errors?: Record<string, string[]>
}

export type ApiErrorKind =
  | 'validation'
  | 'unauthorized'
  | 'forbidden'
  | 'not-found'
  | 'conflict'
  | 'service-unavailable'
  | 'network'
  | 'unexpected'

export class ApiError extends Error {
  public readonly kind: ApiErrorKind
  public readonly status?: number
  public readonly problem?: ProblemDetails
  public readonly body?: unknown

  public constructor(kind: ApiErrorKind, message: string, status?: number, problem?: ProblemDetails, body?: unknown) {
    super(message)
    this.name = 'ApiError'
    this.kind = kind
    this.status = status
    this.problem = problem
    this.body = body
  }
}

export interface ApiRequestOptions extends Omit<RequestInit, 'body' | 'headers' | 'method'> {
  method?: 'GET' | 'POST' | 'PATCH' | 'PUT' | 'DELETE'
  body?: unknown
  headers?: HeadersInit
  idempotencyKey?: string
  expectedRevision?: string | number
}

export interface ApiClient {
  request<T>(path: string, options?: ApiRequestOptions): Promise<T>
}

export function createApiClient(baseUrl: string, fetcher: typeof fetch = fetch): ApiClient {
  return {
    async request<T>(path: string, options: ApiRequestOptions = {}): Promise<T> {
      const headers = new Headers(options.headers)
      headers.set('Accept', 'application/json')
      if (options.idempotencyKey) headers.set('Idempotency-Key', options.idempotencyKey)
      if (options.expectedRevision !== undefined) headers.set('If-Match', String(options.expectedRevision))

      let body: BodyInit | undefined
      if (options.body !== undefined) {
        headers.set('Content-Type', 'application/json')
        body = JSON.stringify(options.body)
      }

      let response: Response
      try {
        response = await fetcher(joinUrl(baseUrl, path), {
          ...options,
          method: options.method ?? 'GET',
          headers,
          body,
        })
      } catch (error) {
        if (isAbortError(error)) throw error
        throw new ApiError('network', 'Không thể kết nối đến dịch vụ. Hãy thử lại.', undefined, undefined)
      }

      if (!response.ok) throw await toApiError(response)
      if (response.status === 204) return undefined as T

      const contentType = response.headers.get('content-type') ?? ''
      if (!contentType.includes('application/json')) return undefined as T
      return (await response.json()) as T
    },
  }
}

export function toApiErrorKind(status: number): ApiErrorKind {
  if (status === 400 || status === 422) return 'validation'
  if (status === 401) return 'unauthorized'
  if (status === 403) return 'forbidden'
  if (status === 404) return 'not-found'
  if (status === 409 || status === 412) return 'conflict'
  if (status === 503) return 'service-unavailable'
  return 'unexpected'
}

export function isRevisionConflict(error: unknown): error is ApiError {
  return error instanceof ApiError && error.kind === 'conflict'
}

export function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError'
}

async function toApiError(response: Response): Promise<ApiError> {
  const body = await readJson(response)
  const problem = { status: response.status, ...(isRecord(body) ? body : {}) } as ProblemDetails
  const kind = toApiErrorKind(response.status)
  const fallback = response.status === 409 || response.status === 412
    ? 'Dữ liệu đã thay đổi. Hãy tải lại rồi thử lại.'
    : 'Yêu cầu không thể hoàn tất.'
  return new ApiError(kind, problem.title ?? problem.detail ?? fallback, response.status, problem, body)
}

async function readJson(response: Response): Promise<unknown> {
  const contentType = response.headers.get('content-type') ?? ''
  if (!contentType.includes('json')) return undefined
  try {
    return await response.json()
  } catch {
    return undefined
  }
}

function isRecord(value: unknown): value is Record<string, unknown> { return typeof value === 'object' && value !== null }

function joinUrl(baseUrl: string, path: string): string {
  const normalizedBase = baseUrl.replace(/\/$/, '')
  const normalizedPath = path.startsWith('/') ? path : `/${path}`
  return `${normalizedBase}${normalizedPath}`
}
