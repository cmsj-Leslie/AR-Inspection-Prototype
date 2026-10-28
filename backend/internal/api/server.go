// Package api 提供 HTTP 接口，供 Unity 移动端和 React 管理端共同使用。
package api

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"log"
	"net/http"
	"strings"
	"time"

	"github.com/holocode/ar-inspection/backend/internal/issue"
	"github.com/holocode/ar-inspection/backend/internal/store"
)

const maxBodyBytes = 64 << 10 // 64KB，足够一条问题

// Server HTTP 服务
type Server struct {
	store       *store.Store
	broker      *Broker
	startedAt   time.Time
	allowOrigin string
}

// NewServer 创建服务。allowOrigin 为 CORS 允许的来源，"*" 表示全部。
func NewServer(s *store.Store, allowOrigin string) *Server {
	return &Server{store: s, broker: NewBroker(), startedAt: time.Now(), allowOrigin: allowOrigin}
}

// Handler 返回注册好路由和中间件的 http.Handler
func (s *Server) Handler() http.Handler {
	mux := http.NewServeMux()
	// -------------------------------------------------------------
	// 对应任务书 3.3 Go 后端能力规范：
	//   - 3.3.1: 简单的服务运行状态检查 (GET /api/health)
	//   - 3.3.2: 接收并保存 Unity 上报的问题 (POST /api/issues)
	//   - 3.3.3: 返回已经保存的问题列表 (GET /api/issues)
	//   - 3.3.4: 根据问题 ID 修改处理状态 (PATCH /api/issues/{id}/status)
	//   - 3.3.8 / 3.3.9: 浏览器跨域与三端同一套数据 (CORS 中间件包装)
	//   - 加分项: Server-Sent Events (SSE) 实时数据流 (GET /api/events)
	// -------------------------------------------------------------
	mux.HandleFunc("GET /api/health", s.health)
	mux.HandleFunc("GET /api/issues", s.listIssues)
	mux.HandleFunc("POST /api/issues", s.createIssue)
	mux.HandleFunc("GET /api/issues/{id}", s.getIssue)
	mux.HandleFunc("PATCH /api/issues/{id}/status", s.updateStatus)
	mux.HandleFunc("DELETE /api/issues/{id}", s.deleteIssue)
	mux.HandleFunc("GET /api/events", s.events)
	mux.HandleFunc("/", func(w http.ResponseWriter, r *http.Request) {
		writeError(w, http.StatusNotFound, "NOT_FOUND", "接口不存在: "+r.Method+" "+r.URL.Path, "")
	})
	return s.withCORS(withLogging(mux))
}

// ---------- 处理函数 ----------

func (s *Server) health(w http.ResponseWriter, r *http.Request) {
	ctx, cancel := context.WithTimeout(r.Context(), 2*time.Second)
	defer cancel()
	dbOK := s.store.Ping(ctx) == nil
	count, _ := s.store.Count(ctx)
	status, code := "ok", http.StatusOK
	if !dbOK {
		status, code = "degraded", http.StatusServiceUnavailable
	}
	writeJSON(w, code, map[string]any{
		"status":     status,
		"database":   map[bool]string{true: "ok", false: "unavailable"}[dbOK],
		"issueCount": count,
		"uptimeSec":  int(time.Since(s.startedAt).Seconds()),
		"time":       time.Now().UTC().Format(time.RFC3339),
	})
}

func (s *Server) listIssues(w http.ResponseWriter, r *http.Request) {
	f := store.ListFilter{
		Status:   strings.TrimSpace(r.URL.Query().Get("status")),
		Priority: strings.TrimSpace(r.URL.Query().Get("priority")),
	}
	if f.Status != "" && !issue.IsValidStatus(f.Status) {
		writeError(w, http.StatusBadRequest, "VALIDATION_ERROR", "status 筛选值无效", "status")
		return
	}
	if f.Priority != "" && !issue.IsValidPriority(f.Priority) {
		writeError(w, http.StatusBadRequest, "VALIDATION_ERROR", "priority 筛选值无效", "priority")
		return
	}
	items, err := s.store.List(r.Context(), f)
	if err != nil {
		internalError(w, err)
		return
	}
	// 用对象包一层：Unity 的 JsonUtility 不支持顶层数组
	writeJSON(w, http.StatusOK, map[string]any{"items": items, "total": len(items)})
}

// createIssue 对应任务书 3.3.2 接收保存上报问题
func (s *Server) createIssue(w http.ResponseWriter, r *http.Request) {
	var in issue.CreateInput
	if !decodeJSON(w, r, &in) {
		return
	}
	// 对应任务书 3.3.6: 标题为空或枚举值错误时，返回明确的参数错误提示与 400 状态码
	if err := in.Normalize(); err != nil {
		writeValidation(w, err)
		return
	}
	// 对应任务书 3.3.5: 后端生成唯一 UUID，默认初始状态为 open，并持久化到 SQLite
	it, err := s.store.Create(r.Context(), in)
	if err != nil {
		internalError(w, err)
		return
	}
	// 广播实时推送事件给所有已连接的管理端（加分项：SSE 实时同步）
	s.broker.Publish(Event{Type: "issue.created", Data: it})
	writeJSON(w, http.StatusCreated, it)
}

func (s *Server) getIssue(w http.ResponseWriter, r *http.Request) {
	it, err := s.store.Get(r.Context(), r.PathValue("id"))
	if s.handleStoreErr(w, err) {
		return
	}
	writeJSON(w, http.StatusOK, it)
}

func (s *Server) updateStatus(w http.ResponseWriter, r *http.Request) {
	var in issue.UpdateStatusInput
	if !decodeJSON(w, r, &in) {
		return
	}
	if err := in.Validate(); err != nil {
		writeValidation(w, err)
		return
	}
	it, err := s.store.UpdateStatus(r.Context(), r.PathValue("id"), in.Status)
	if s.handleStoreErr(w, err) {
		return
	}
	s.broker.Publish(Event{Type: "issue.updated", Data: it})
	writeJSON(w, http.StatusOK, it)
}

func (s *Server) deleteIssue(w http.ResponseWriter, r *http.Request) {
	id := r.PathValue("id")
	if s.handleStoreErr(w, s.store.Delete(r.Context(), id)) {
		return
	}
	s.broker.Publish(Event{Type: "issue.deleted", Data: map[string]string{"id": id}})
	w.WriteHeader(http.StatusNoContent)
}

// events 以 Server-Sent Events 推送实时变更
func (s *Server) events(w http.ResponseWriter, r *http.Request) {
	flusher, ok := w.(http.Flusher)
	if !ok {
		writeError(w, http.StatusInternalServerError, "SSE_UNSUPPORTED", "服务器不支持流式响应", "")
		return
	}
	w.Header().Set("Content-Type", "text/event-stream")
	w.Header().Set("Cache-Control", "no-cache")
	w.Header().Set("Connection", "keep-alive")
	w.Header().Set("X-Accel-Buffering", "no")

	ch, unsubscribe := s.broker.Subscribe()
	defer unsubscribe()

	fmt.Fprint(w, "retry: 3000\nevent: ready\ndata: {}\n\n")
	flusher.Flush()

	heartbeat := time.NewTicker(25 * time.Second) // 防止代理/浏览器断开空闲连接
	defer heartbeat.Stop()
	for {
		select {
		case <-r.Context().Done():
			return
		case msg, ok := <-ch:
			if !ok {
				return
			}
			fmt.Fprintf(w, "event: change\ndata: %s\n\n", msg)
			flusher.Flush()
		case <-heartbeat.C:
			fmt.Fprint(w, ": ping\n\n")
			flusher.Flush()
		}
	}
}

// ---------- 工具函数 ----------

type errorBody struct {
	Error struct {
		Code    string `json:"code"`
		Message string `json:"message"`
		Field   string `json:"field,omitempty"`
	} `json:"error"`
}

func writeJSON(w http.ResponseWriter, code int, v any) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(code)
	_ = json.NewEncoder(w).Encode(v)
}

func writeError(w http.ResponseWriter, code int, errCode, msg, field string) {
	var b errorBody
	b.Error.Code, b.Error.Message, b.Error.Field = errCode, msg, field
	writeJSON(w, code, b)
}

func writeValidation(w http.ResponseWriter, err error) {
	var ve *issue.ValidationError
	if errors.As(err, &ve) {
		writeError(w, http.StatusBadRequest, "VALIDATION_ERROR", ve.Message, ve.Field)
		return
	}
	writeError(w, http.StatusBadRequest, "VALIDATION_ERROR", err.Error(), "")
}

func internalError(w http.ResponseWriter, err error) {
	log.Printf("internal error: %v", err)
	writeError(w, http.StatusInternalServerError, "INTERNAL_ERROR", "服务器内部错误", "")
}

// handleStoreErr 统一处理仓储错误，已写响应时返回 true
func (s *Server) handleStoreErr(w http.ResponseWriter, err error) bool {
	switch {
	case err == nil:
		return false
	case errors.Is(err, issue.ErrNotFound):
		writeError(w, http.StatusNotFound, "NOT_FOUND", "问题不存在", "id")
	default:
		internalError(w, err)
	}
	return true
}

// decodeJSON 解析请求体，失败时写 400 并返回 false
func decodeJSON(w http.ResponseWriter, r *http.Request, dst any) bool {
	r.Body = http.MaxBytesReader(w, r.Body, maxBodyBytes)
	dec := json.NewDecoder(r.Body)
	if err := dec.Decode(dst); err != nil {
		var typeErr *json.UnmarshalTypeError
		var maxErr *http.MaxBytesError
		switch {
		case errors.Is(err, io.EOF):
			writeError(w, http.StatusBadRequest, "INVALID_JSON", "请求体不能为空", "")
		case errors.As(err, &typeErr):
			writeError(w, http.StatusBadRequest, "VALIDATION_ERROR",
				fmt.Sprintf("字段 %s 类型错误", typeErr.Field), typeErr.Field)
		case errors.As(err, &maxErr):
			writeError(w, http.StatusRequestEntityTooLarge, "BODY_TOO_LARGE", "请求体过大", "")
		default:
			writeError(w, http.StatusBadRequest, "INVALID_JSON", "JSON 格式错误", "")
		}
		return false
	}
	return true
}

// ---------- 中间件 ----------

func (s *Server) withCORS(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Access-Control-Allow-Origin", s.allowOrigin)
		w.Header().Set("Access-Control-Allow-Methods", "GET, POST, PATCH, DELETE, OPTIONS")
		w.Header().Set("Access-Control-Allow-Headers", "Content-Type, Accept, Authorization, Cache-Control, X-Requested-With")
		w.Header().Set("Access-Control-Max-Age", "600")
		if r.Method == http.MethodOptions {
			w.WriteHeader(http.StatusNoContent)
			return
		}
		next.ServeHTTP(w, r)
	})
}

type statusRecorder struct {
	http.ResponseWriter
	code int
}

func (r *statusRecorder) WriteHeader(code int) { r.code = code; r.ResponseWriter.WriteHeader(code) }

// Flush 透传，保证 SSE 在日志中间件包裹下仍可用
func (r *statusRecorder) Flush() {
	if f, ok := r.ResponseWriter.(http.Flusher); ok {
		f.Flush()
	}
}

func withLogging(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		start := time.Now()
		rec := &statusRecorder{ResponseWriter: w, code: http.StatusOK}
		next.ServeHTTP(rec, r)
		log.Printf("%s %s %d %s (%s)", r.Method, r.URL.RequestURI(), rec.code, time.Since(start).Round(time.Millisecond), r.RemoteAddr)
	})
}
