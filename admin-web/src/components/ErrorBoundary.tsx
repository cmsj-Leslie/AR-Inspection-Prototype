import { Component, type ErrorInfo, type ReactNode } from 'react'

/** 兜底：任何渲染异常都显示错误页，而不是白屏 */
export class ErrorBoundary extends Component<{ children: ReactNode }, { error: Error | null }> {
  state = { error: null as Error | null }

  static getDerivedStateFromError(error: Error) {
    return { error }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('页面渲染异常', error, info.componentStack)
  }

  render() {
    if (!this.state.error) return this.props.children
    return (
      <div className="fatal">
        <div className="fatal__card">
          <h1>页面出现异常</h1>
          <p>{this.state.error.message}</p>
          <button className="btn btn--primary" onClick={() => window.location.reload()}>
            重新加载
          </button>
        </div>
      </div>
    )
  }
}
