import { useMemo, useState } from 'react'
import type { Issue } from '../types'

interface Props {
  issues: Issue[]
  selectedId: string | null
  onSelect: (id: string) => void
}

const SIZE = 260
const PAD = 22

/**
 * AR 坐标俯视图：把标记的 x/z（水平面）投影到二维平面。
 * 注意：坐标是相对每次 AR 会话原点的，不同会话之间不可直接比较，仅作示意。
 */
export function PositionMap({ issues, selectedId, onSelect }: Props) {
  const [hover, setHover] = useState<string | null>(null)

  const { points, range } = useMemo(() => {
    const max = Math.max(1, ...issues.flatMap((i) => [Math.abs(i.position.x), Math.abs(i.position.z)]))
    const range = Math.ceil(max * 1.15 * 2) / 2 // 取 0.5 m 的整数倍
    const scale = (SIZE / 2 - PAD) / range
    return {
      range,
      points: issues.map((i) => ({
        issue: i,
        cx: SIZE / 2 + i.position.x * scale,
        cy: SIZE / 2 - i.position.z * scale, // z 轴向前，屏幕上方
      })),
    }
  }, [issues])

  const active = points.find((p) => p.issue.id === (hover ?? selectedId))

  return (
    <section className="panel map" aria-labelledby="map-title">
      <header className="panel__head">
        <h2 id="map-title">标记俯视图</h2>
        <span className="muted">X / Z 平面 · ±{range} m</span>
      </header>
      <svg viewBox={`0 0 ${SIZE} ${SIZE}`} className="map__svg" role="img" aria-label="AR 标记位置俯视图">
        <defs>
          <radialGradient id="mapGlow" cx="50%" cy="50%" r="50%">
            <stop offset="0%" stopColor="rgba(34,211,238,0.10)" />
            <stop offset="100%" stopColor="rgba(34,211,238,0)" />
          </radialGradient>
        </defs>
        <rect width={SIZE} height={SIZE} fill="url(#mapGlow)" />
        {[0.25, 0.5, 0.75, 1].map((r) => (
          <circle key={r} cx={SIZE / 2} cy={SIZE / 2} r={(SIZE / 2 - PAD) * r} className="map__ring" />
        ))}
        <line x1={PAD} y1={SIZE / 2} x2={SIZE - PAD} y2={SIZE / 2} className="map__axis" />
        <line x1={SIZE / 2} y1={PAD} x2={SIZE / 2} y2={SIZE - PAD} className="map__axis" />
        <text x={SIZE - PAD + 4} y={SIZE / 2 + 3} className="map__label">X</text>
        <text x={SIZE / 2 - 3} y={PAD - 6} className="map__label">Z</text>
        <circle cx={SIZE / 2} cy={SIZE / 2} r={3} className="map__origin" />
        {points.map(({ issue, cx, cy }) => (
          <g
            key={issue.id}
            className={`map__pt map__pt--${issue.priority} ${issue.status === 'resolved' ? 'is-resolved' : ''} ${
              issue.id === selectedId ? 'is-selected' : ''
            }`}
            onMouseEnter={() => setHover(issue.id)}
            onMouseLeave={() => setHover(null)}
            onClick={() => onSelect(issue.id)}
          >
            <circle cx={cx} cy={cy} r={11} className="map__halo" />
            <circle cx={cx} cy={cy} r={5} className="map__dot" />
          </g>
        ))}
      </svg>
      <div className="map__info">
        {active ? (
          <>
            <strong>{active.issue.title}</strong>
            <span className="mono">
              x {active.issue.position.x.toFixed(2)} · y {active.issue.position.y.toFixed(2)} · z{' '}
              {active.issue.position.z.toFixed(2)}
            </span>
          </>
        ) : (
          <span className="muted">悬停或点击圆点查看问题</span>
        )}
      </div>
    </section>
  )
}
