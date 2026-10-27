// 与 Go 后端 / Unity 保持一致的数据结构

export type Priority = 'low' | 'medium' | 'high'
export type Status = 'open' | 'in_progress' | 'resolved'

export interface Position {
  x: number
  y: number
  z: number
}

export interface Issue {
  id: string
  title: string
  description: string
  priority: Priority
  status: Status
  position: Position
  createdAt: string
  updatedAt: string
}

export interface IssueListResponse {
  items: Issue[]
  total: number
}

export interface HealthResponse {
  status: 'ok' | 'degraded'
  database: string
  issueCount: number
  uptimeSec: number
  time: string
}

export const PRIORITIES: Priority[] = ['high', 'medium', 'low']
export const STATUSES: Status[] = ['open', 'in_progress', 'resolved']

export const PRIORITY_LABEL: Record<Priority, string> = {
  high: '高',
  medium: '中',
  low: '低',
}

export const STATUS_LABEL: Record<Status, string> = {
  open: '待处理',
  in_progress: '处理中',
  resolved: '已解决',
}
