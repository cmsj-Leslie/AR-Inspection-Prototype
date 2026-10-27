import { useCallback, useEffect, useRef, useState } from 'react'
import { api, ApiError } from '../api'
import type { Issue, Status } from '../types'

export type LiveState = 'connecting' | 'live' | 'offline'

interface ChangeEvent {
  type: 'issue.created' | 'issue.updated' | 'issue.deleted'
  data: Issue | { id: string }
}

/**
 * 问题列表的数据管理：
 * - 首次加载 + 手动刷新
 * - SSE 实时更新（断线后浏览器自动重连，并在重连成功时全量刷新一次）
 * - 状态修改采用乐观更新，失败时回滚
 */
export function useIssues() {
  const [issues, setIssues] = useState<Issue[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<ApiError | null>(null)
  const [live, setLive] = useState<LiveState>('connecting')
  const [lastSync, setLastSync] = useState<Date | null>(null)
  const [recentIds, setRecentIds] = useState<Set<string>>(new Set())
  const issuesRef = useRef(issues)
  issuesRef.current = issues

  const flash = useCallback((id: string) => {
    setRecentIds((s) => new Set(s).add(id))
    window.setTimeout(() => {
      setRecentIds((s) => {
        const n = new Set(s)
        n.delete(id)
        return n
      })
    }, 2500)
  }, [])

  const refresh = useCallback(async () => {
    setLoading(true)
    try {
      const res = await api.listIssues()
      setIssues(res.items)
      setError(null)
      setLastSync(new Date())
    } catch (e) {
      setError(e instanceof ApiError ? e : new ApiError('network', String(e)))
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void refresh()
  }, [refresh])

  // SSE 实时订阅
  useEffect(() => {
    const es = new EventSource(api.eventsUrl())
    let wasOffline = false

    es.addEventListener('ready', () => {
      setLive('live')
      if (wasOffline) {
        wasOffline = false
        void refresh() // 断线期间可能漏掉事件，重连后全量同步
      }
    })
    es.addEventListener('change', (ev) => {
      const msg = JSON.parse((ev as MessageEvent).data) as ChangeEvent
      setLastSync(new Date())
      setError(null)
      if (msg.type === 'issue.deleted') {
        setIssues((list) => list.filter((i) => i.id !== msg.data.id))
        return
      }
      const it = msg.data as Issue
      setIssues((list) => {
        const idx = list.findIndex((i) => i.id === it.id)
        if (idx === -1) return [it, ...list]
        const next = list.slice()
        next[idx] = it
        return next
      })
      flash(it.id)
    })
    es.onerror = () => {
      wasOffline = true
      setLive('offline')
    }
    return () => es.close()
  }, [refresh, flash])

  const updateStatus = useCallback(async (id: string, status: Status) => {
    const prev = issuesRef.current.find((i) => i.id === id)
    if (!prev || prev.status === status) return
    setIssues((list) => list.map((i) => (i.id === id ? { ...i, status } : i)))
    try {
      const updated = await api.updateStatus(id, status)
      setIssues((list) => list.map((i) => (i.id === id ? updated : i)))
    } catch (e) {
      setIssues((list) => list.map((i) => (i.id === id ? prev : i))) // 回滚
      throw e
    }
  }, [])

  const remove = useCallback(async (id: string) => {
    await api.deleteIssue(id)
    setIssues((list) => list.filter((i) => i.id !== id))
  }, [])

  return { issues, loading, error, live, lastSync, recentIds, refresh, updateStatus, remove }
}

/** 定时检查后端健康状态 */
export function useHealth(intervalMs = 10000) {
  const [ok, setOk] = useState<boolean | null>(null)
  useEffect(() => {
    let alive = true
    const check = () =>
      api
        .health()
        .then((h) => alive && setOk(h.status === 'ok'))
        .catch(() => alive && setOk(false))
    void check()
    const t = window.setInterval(check, intervalMs)
    return () => {
      alive = false
      window.clearInterval(t)
    }
  }, [intervalMs])
  return ok
}
