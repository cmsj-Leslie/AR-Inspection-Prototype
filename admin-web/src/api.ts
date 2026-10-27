import type { HealthResponse, Issue, IssueListResponse, Status } from './types'

/**
 * 后端地址：
 * 1. 优先使用构建时环境变量 VITE_API_BASE（例如 http://192.168.1.10:8080）
 * 2. 否则默认同主机的 8080 端口，这样局域网内其他电脑打开页面也能连上后端
 */
export const API_BASE: string =
  (import.meta.env.VITE_API_BASE as string | undefined)?.replace(/\/$/, '') ||
  `${window.location.protocol}//${window.location.hostname}:8080`

/** 统一的接口错误，kind 区分网络不可达 / 后端返回错误 / 超时 */
export class ApiError extends Error {
  readonly kind: 'network' | 'http' | 'timeout'
  readonly status?: number
  readonly code?: string
  readonly field?: string

  constructor(
    kind: ApiError['kind'],
    message: string,
    extra: { status?: number; code?: string; field?: string } = {},
  ) {
    super(message)
    this.name = 'ApiError'
    this.kind = kind
    this.status = extra.status
    this.code = extra.code
    this.field = extra.field
  }
}

async function request<T>(path: string, init: RequestInit = {}, timeoutMs = 8000): Promise<T> {
  const ctrl = new AbortController()
  const timer = window.setTimeout(() => ctrl.abort(), timeoutMs)
  let res: Response
  try {
    res = await fetch(API_BASE + path, {
      ...init,
      headers: { 'Content-Type': 'application/json', ...init.headers },
      signal: ctrl.signal,
    })
  } catch (e) {
    if ((e as Error).name === 'AbortError') {
      throw new ApiError('timeout', `请求超时（${timeoutMs / 1000}s），后端可能无响应`)
    }
    throw new ApiError('network', `无法连接后端 ${API_BASE}，请确认 Go 服务已启动`)
  } finally {
    window.clearTimeout(timer)
  }

  if (res.status === 204) return undefined as T

  // 后端错误格式：{ error: { code, message, field? } }
  const body = await res.json().catch(() => null)
  if (!res.ok) {
    const err = body?.error
    throw new ApiError('http', err?.message ?? `请求失败（HTTP ${res.status}）`, {
      status: res.status,
      code: err?.code,
      field: err?.field,
    })
  }
  return body as T
}

export const api = {
  health: () => request<HealthResponse>('/api/health', {}, 3000),
  listIssues: () => request<IssueListResponse>('/api/issues'),
  updateStatus: (id: string, status: Status) =>
    request<Issue>(`/api/issues/${encodeURIComponent(id)}/status`, {
      method: 'PATCH',
      body: JSON.stringify({ status }),
    }),
  deleteIssue: (id: string) =>
    request<void>(`/api/issues/${encodeURIComponent(id)}`, { method: 'DELETE' }),
  eventsUrl: () => `${API_BASE}/api/events`,
}
