import { createApiClient } from '@commerceos/frontend-foundation'

export const apiBaseUrl = import.meta.env.VITE_COMMERCEOS_API_BASE_URL ?? ''
export const apiClient = createApiClient(apiBaseUrl)
