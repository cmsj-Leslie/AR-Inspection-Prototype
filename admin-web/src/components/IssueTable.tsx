import { useState } from 'react'
import { ApiError } from '../api'
import { PRIORITY_LABEL, STATUSES, STATUS_LABEL, type Issue, type Status } from '../types'
import { formatDateTime, timeAgo } from '../utils'
import { useToast } from './Toast'

interface Props {
  issues: Issue[]
  recentIds: Set<string>
  selectedId: string | null
  onSelect: (id: string | null) => void
  onStatusChange: (id: string, s: Status) => Promise<void>
  onDelete: (id: string) => Promise<void>
}

export function IssueTable({ issues, recentIds, selectedId, onSelect, onStatusChange, onDelete }: Props) {
  const toast = useToast()
  const [pending, setPending] = useState<string | null>(null)

  const change = async (it: Issue, s: Status) => {
    setPending(it.id)
    try {
      await onStatusChange(it.id, s)
      toast('success', `「${it.title}」已改为 ${STATUS_LABEL[s]}`)
    } catch (e) {
      toast('error', `修改失败：${e instanceof ApiError ? e.message : String(e)}`)
    } finally {
      setPending(null)
    }
  }

  const remove = async (it: Issue) => {
    if (!window.confirm(`确定删除「${it.title}」吗？此操作不可恢复。`)) return
    try {
      await onDelete(it.id)
      toast('success', '已删除')
      if (selectedId === it.id) onSelect(null)
    } catch (e) {
      toast('error', `删除失败：${e instanceof ApiError ? e.message : String(e)}`)
    }
  }

  return (
    <div className="table-wrap">
      <table className="table">
        <thead>
          <tr>
            <th>问题</th>
            <th>优先级</th>
            <th>状态</th>
            <th className="hide-sm">坐标 (m)</th>
            <th className="hide-sm">上报时间</th>
            <th aria-label="操作" />
          </tr>
        </thead>
        <tbody>
          {issues.map((it) => (
            <tr
              key={it.id}
              className={`row ${recentIds.has(it.id) ? 'row--flash' : ''} ${selectedId === it.id ? 'row--selected' : ''}`}
              onClick={() => onSelect(selectedId === it.id ? null : it.id)}
            >
              <td>
                <div className="cell-title">
                  <span className={`dot dot--${it.priority}`} />
                  <div>
                    <div className="cell-title__text">{it.title}</div>
                    {it.description && <div className="cell-title__desc">{it.description}</div>}
                  </div>
                </div>
              </td>
              <td>
                <span className={`badge badge--${it.priority}`}>{PRIORITY_LABEL[it.priority]}</span>
              </td>
              <td onClick={(e) => e.stopPropagation()}>
                <div className={`status-select status-select--${it.status} ${pending === it.id ? 'is-pending' : ''}`}>
                  <select
                    id={`status-${it.id}`}
                    aria-label={`修改「${it.title}」的状态`}
                    value={it.status}
                    disabled={pending === it.id}
                    onChange={(e) => void change(it, e.target.value as Status)}
                  >
                    {STATUSES.map((s) => (
                      <option key={s} value={s}>
                        {STATUS_LABEL[s]}
                      </option>
                    ))}
                  </select>
                </div>
              </td>
              <td className="hide-sm mono muted">
                {it.position.x.toFixed(2)}, {it.position.y.toFixed(2)}, {it.position.z.toFixed(2)}
              </td>
              <td className="hide-sm muted" title={formatDateTime(it.createdAt)}>
                {timeAgo(it.createdAt)}
              </td>
              <td onClick={(e) => e.stopPropagation()}>
                <button
                  id={`delete-${it.id}`}
                  className="icon-btn"
                  title="删除"
                  aria-label={`删除「${it.title}」`}
                  onClick={() => void remove(it)}
                >
                  <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden>
                    <path
                      d="M4 7h16M10 11v6M14 11v6M5 7l1 12a2 2 0 002 2h8a2 2 0 002-2l1-12M9 7V4h6v3"
                      fill="none"
                      stroke="currentColor"
                      strokeWidth="1.8"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                    />
                  </svg>
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
