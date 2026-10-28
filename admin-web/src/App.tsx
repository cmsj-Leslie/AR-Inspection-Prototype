import { useMemo, useState } from 'react'
import { API_BASE } from './api'
import { IssueTable } from './components/IssueTable'
import { PositionMap } from './components/PositionMap'
import { useHealth, useIssues, type LiveState } from './hooks/useIssues'
import { PRIORITIES, PRIORITY_LABEL, STATUSES, STATUS_LABEL, type Priority, type Status } from './types'
import { formatDateTime, timeAgo } from './utils'

type Filter<T> = T | 'all'

export default function App() {
  const { issues, loading, error, live, lastSync, recentIds, refresh, updateStatus, remove } = useIssues()
  const healthy = useHealth()
  const [status, setStatus] = useState<Filter<Status>>('all')
  const [priority, setPriority] = useState<Filter<Priority>>('all')
  const [keyword, setKeyword] = useState('')
  const [selectedId, setSelectedId] = useState<string | null>(null)

  const stats = useMemo(
    () => ({
      total: issues.length,
      open: issues.filter((i) => i.status === 'open').length,
      in_progress: issues.filter((i) => i.status === 'in_progress').length,
      resolved: issues.filter((i) => i.status === 'resolved').length,
      highOpen: issues.filter((i) => i.priority === 'high' && i.status !== 'resolved').length,
    }),
    [issues],
  )

  const filtered = useMemo(() => {
    const kw = keyword.trim().toLowerCase()
    return issues.filter(
      (i) =>
        (status === 'all' || i.status === status) &&
        (priority === 'all' || i.priority === priority) &&
        (!kw || i.title.toLowerCase().includes(kw) || i.description.toLowerCase().includes(kw)),
    )
  }, [issues, status, priority, keyword])

  const selected = issues.find((i) => i.id === selectedId) ?? null
  const showEmptyFirstLoad = loading && issues.length === 0 && !error

  return (
    <div className="app">
      <div className="bg-orbs" aria-hidden />
      <header className="topbar">
        <div className="brand">
          <div className="brand__logo" aria-hidden>
            <svg viewBox="0 0 24 24" width="20" height="20">
              <path d="M12 2l9 5v10l-9 5-9-5V7z" fill="none" stroke="currentColor" strokeWidth="1.8" />
              <circle cx="12" cy="12" r="3" fill="currentColor" />
            </svg>
          </div>
          <div>
            <h1 className="brand__title">巡检问题管理台</h1>
            <p className="brand__sub">AR 现场标注 · 实时同步</p>
          </div>
        </div>
        <div className="topbar__right">
          <ConnectionPill healthy={healthy} live={live} />
          <button id="btn-refresh" className="btn" onClick={() => void refresh()} disabled={loading}>
            <svg viewBox="0 0 24 24" width="15" height="15" className={loading ? 'spin' : ''} aria-hidden>
              <path d="M20 12a8 8 0 11-2.3-5.7M20 4v5h-5" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
            </svg>
            刷新
          </button>
        </div>
      </header>

      {/* 对应任务书 3.2.4：后端不可用时，页面应显示错误提示，绝不能直接白屏 */}
      {error && (
        <div className="alert" role="alert">
          <div className="alert__icon">!</div>
          <div className="alert__body">
            <strong>{error.kind === 'http' ? '后端返回错误' : '无法连接后端服务'}</strong>
            <p>{error.message}</p>
            <p className="muted small">
              后端地址：<code>{API_BASE}</code>
              {lastSync && ` · 当前显示的是 ${timeAgo(lastSync.toISOString())} 的数据`}
            </p>
          </div>
          <button id="btn-retry" className="btn btn--primary" onClick={() => void refresh()}>
            重试
          </button>
        </div>
      )}

      <section className="stats" aria-label="统计">
        <StatCard label="全部问题" value={stats.total} tone="neutral" />
        <StatCard label="待处理" value={stats.open} tone="open" onClick={() => setStatus('open')} active={status === 'open'} />
        <StatCard
          label="处理中"
          value={stats.in_progress}
          tone="progress"
          onClick={() => setStatus('in_progress')}
          active={status === 'in_progress'}
        />
        <StatCard
          label="已解决"
          value={stats.resolved}
          tone="resolved"
          onClick={() => setStatus('resolved')}
          active={status === 'resolved'}
        />
        <StatCard label="高优先级未解决" value={stats.highOpen} tone="high" />
      </section>

      <main className="layout">
        <section className="panel list" aria-labelledby="list-title">
          <header className="panel__head">
            <h2 id="list-title">
              问题列表 <span className="count">{filtered.length}</span>
            </h2>
            <span className="muted small">{lastSync ? `同步于 ${lastSync.toLocaleTimeString('zh-CN', { hour12: false })}` : ''}</span>
          </header>

          <div className="toolbar">
            <div className="search">
              <svg viewBox="0 0 24 24" width="15" height="15" aria-hidden>
                <circle cx="11" cy="11" r="7" fill="none" stroke="currentColor" strokeWidth="2" />
                <path d="M20 20l-4-4" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
              </svg>
              <input
                id="search-input"
                placeholder="搜索标题或描述"
                value={keyword}
                onChange={(e) => setKeyword(e.target.value)}
              />
            </div>
            <Segmented
              id="filter-status"
              value={status}
              onChange={setStatus}
              options={[{ v: 'all', l: '全部状态' }, ...STATUSES.map((s) => ({ v: s, l: STATUS_LABEL[s] }))]}
            />
            <Segmented
              id="filter-priority"
              value={priority}
              onChange={setPriority}
              options={[{ v: 'all', l: '全部' }, ...PRIORITIES.map((p) => ({ v: p, l: PRIORITY_LABEL[p] }))]}
            />
          </div>

          {showEmptyFirstLoad ? (
            <div className="skeletons">
              {[0, 1, 2, 3].map((i) => (
                <div key={i} className="skeleton" style={{ animationDelay: `${i * 120}ms` }} />
              ))}
            </div>
          ) : filtered.length === 0 ? (
            <div className="empty">
              <div className="empty__icon" aria-hidden>
                ◎
              </div>
              <p>{issues.length === 0 ? (error ? '暂时无法获取数据' : '还没有上报的问题') : '没有符合筛选条件的问题'}</p>
              <p className="muted small">
                {issues.length === 0 && !error ? '在手机端识别平面、放置标记并提交后，这里会实时出现。' : ''}
              </p>
            </div>
          ) : (
            <IssueTable
              issues={filtered}
              recentIds={recentIds}
              selectedId={selectedId}
              onSelect={setSelectedId}
              onStatusChange={updateStatus}
              onDelete={remove}
            />
          )}
        </section>

        <aside className="side">
          <PositionMap issues={filtered} selectedId={selectedId} onSelect={setSelectedId} />
          <section className="panel detail" aria-labelledby="detail-title">
            <header className="panel__head">
              <h2 id="detail-title">问题详情</h2>
            </header>
            {selected ? (
              <dl className="detail__list">
                <dt>标题</dt>
                <dd>{selected.title}</dd>
                <dt>描述</dt>
                <dd>{selected.description || <span className="muted">（无）</span>}</dd>
                <dt>优先级</dt>
                <dd>
                  <span className={`badge badge--${selected.priority}`}>{PRIORITY_LABEL[selected.priority]}</span>
                </dd>
                <dt>状态</dt>
                <dd>{STATUS_LABEL[selected.status]}</dd>
                <dt>坐标</dt>
                <dd className="mono">
                  ({selected.position.x.toFixed(3)}, {selected.position.y.toFixed(3)}, {selected.position.z.toFixed(3)})
                </dd>
                <dt>上报时间</dt>
                <dd>{formatDateTime(selected.createdAt)}</dd>
                <dt>更新时间</dt>
                <dd>{formatDateTime(selected.updatedAt)}</dd>
                <dt>ID</dt>
                <dd className="mono small muted">{selected.id}</dd>
              </dl>
            ) : (
              <p className="muted small detail__empty">点击列表中的一行查看详情</p>
            )}
          </section>
        </aside>
      </main>

      <footer className="footer muted small">
        后端：<code>{API_BASE}</code> · 坐标为 AR 会话内相对坐标（单位：米）
      </footer>
    </div>
  )
}

function ConnectionPill({ healthy, live }: { healthy: boolean | null; live: LiveState }) {
  const state = healthy === false ? 'down' : healthy === null ? 'pending' : live === 'live' ? 'live' : 'up'
  const text = { down: '后端离线', pending: '检测中', live: '实时连接', up: '已连接' }[state]
  return (
    <div className={`pill pill--${state}`} title={`健康检查：${healthy ? '正常' : '异常'}；实时推送：${live}`}>
      <span className="pill__dot" />
      {text}
    </div>
  )
}

function StatCard(props: { label: string; value: number; tone: string; onClick?: () => void; active?: boolean }) {
  const Tag = props.onClick ? 'button' : 'div'
  return (
    <Tag className={`stat stat--${props.tone} ${props.active ? 'is-active' : ''}`} onClick={props.onClick}>
      <span className="stat__label">{props.label}</span>
      <span className="stat__value">{props.value}</span>
    </Tag>
  )
}

function Segmented<T extends string>(props: {
  id: string
  value: T
  onChange: (v: T) => void
  options: { v: T; l: string }[]
}) {
  return (
    <div className="segmented" role="radiogroup" id={props.id}>
      {props.options.map((o) => (
        <button
          key={o.v}
          id={`${props.id}-${o.v}`}
          role="radio"
          aria-checked={props.value === o.v}
          className={props.value === o.v ? 'is-active' : ''}
          onClick={() => props.onChange(o.v)}
        >
          {o.l}
        </button>
      ))}
    </div>
  )
}
