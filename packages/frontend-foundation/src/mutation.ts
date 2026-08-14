import { ApiError } from './api'

export interface CursorPage<T> {
  items: T[]
  nextCursor?: string | null
}

export function appendCursorPage<T>(current: T[], page: CursorPage<T>): T[] {
  return [...current, ...page.items]
}

export function canLoadMore<T>(page: CursorPage<T>): boolean {
  return Boolean(page.nextCursor)
}

export interface MutationAttempt {
  idempotencyKey: string
}

export function createMutationAttempt(): MutationAttempt {
  return { idempotencyKey: createIdempotencyKey() }
}

export function createIdempotencyKey(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') return crypto.randomUUID()
  return `mutation-${Date.now()}-${Math.random().toString(16).slice(2)}`
}

export interface Revisioned {
  revision: string | number
}

export function revisionConflictMessage(error: unknown): string | undefined {
  if (error instanceof ApiError && error.kind === 'conflict') {
    return 'Bản ghi này đã được thay đổi. Hãy tải lại dữ liệu trước khi gửi lại.'
  }
  return undefined
}
