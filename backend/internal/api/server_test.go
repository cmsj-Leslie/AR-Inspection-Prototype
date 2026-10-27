package api

import (
	"bytes"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"path/filepath"
	"testing"

	"github.com/holocode/ar-inspection/backend/internal/issue"
	"github.com/holocode/ar-inspection/backend/internal/store"
)

func newTestServer(t *testing.T, dbPath string) (http.Handler, *store.Store) {
	t.Helper()
	st, err := store.Open(dbPath)
	if err != nil {
		t.Fatalf("open store: %v", err)
	}
	return NewServer(st, "*").Handler(), st
}

func do(t *testing.T, h http.Handler, method, path string, body any) *httptest.ResponseRecorder {
	t.Helper()
	var buf bytes.Buffer
	if body != nil {
		if s, ok := body.(string); ok {
			buf.WriteString(s)
		} else {
			_ = json.NewEncoder(&buf).Encode(body)
		}
	}
	req := httptest.NewRequest(method, path, &buf)
	req.Header.Set("Content-Type", "application/json")
	rec := httptest.NewRecorder()
	h.ServeHTTP(rec, req)
	return rec
}

func validBody() map[string]any {
	return map[string]any{
		"title": "入口墙面破损", "description": "左侧墙体存在裂缝", "priority": "high",
		"position": map[string]float64{"x": 0.42, "y": 0.03, "z": 1.26},
	}
}

func TestHealth(t *testing.T) {
	h, st := newTestServer(t, filepath.Join(t.TempDir(), "t.db"))
	defer st.Close()
	rec := do(t, h, "GET", "/api/health", nil)
	if rec.Code != 200 {
		t.Fatalf("health code = %d", rec.Code)
	}
}

func TestCreateListUpdate(t *testing.T) {
	h, st := newTestServer(t, filepath.Join(t.TempDir(), "t.db"))
	defer st.Close()

	rec := do(t, h, "POST", "/api/issues", validBody())
	if rec.Code != http.StatusCreated {
		t.Fatalf("create code = %d body=%s", rec.Code, rec.Body)
	}
	var created issue.Issue
	_ = json.Unmarshal(rec.Body.Bytes(), &created)
	if created.ID == "" || created.Status != "open" || created.CreatedAt == "" || created.UpdatedAt == "" {
		t.Fatalf("后端应生成 id/时间戳并默认 open: %+v", created)
	}
	if created.Position.X != 0.42 || created.Position.Z != 1.26 {
		t.Fatalf("坐标不一致: %+v", created.Position)
	}

	rec = do(t, h, "GET", "/api/issues", nil)
	var list struct {
		Items []issue.Issue `json:"items"`
		Total int           `json:"total"`
	}
	_ = json.Unmarshal(rec.Body.Bytes(), &list)
	if list.Total != 1 || list.Items[0].ID != created.ID {
		t.Fatalf("list = %+v", list)
	}

	rec = do(t, h, "PATCH", "/api/issues/"+created.ID+"/status", map[string]string{"status": "in_progress"})
	if rec.Code != 200 {
		t.Fatalf("update code = %d body=%s", rec.Code, rec.Body)
	}
	var updated issue.Issue
	_ = json.Unmarshal(rec.Body.Bytes(), &updated)
	if updated.Status != "in_progress" {
		t.Fatalf("status = %s", updated.Status)
	}

	// 按状态筛选
	rec = do(t, h, "GET", "/api/issues?status=open", nil)
	_ = json.Unmarshal(rec.Body.Bytes(), &list)
	if list.Total != 0 {
		t.Fatalf("筛选 open 应为 0，实际 %d", list.Total)
	}
}

func TestValidation(t *testing.T) {
	h, st := newTestServer(t, filepath.Join(t.TempDir(), "t.db"))
	defer st.Close()

	cases := []struct {
		name  string
		body  any
		field string
	}{
		{"空标题", func() map[string]any { b := validBody(); b["title"] = "   "; return b }(), "title"},
		{"缺标题", func() map[string]any { b := validBody(); delete(b, "title"); return b }(), "title"},
		{"错误优先级", func() map[string]any { b := validBody(); b["priority"] = "urgent"; return b }(), "priority"},
		{"缺坐标", func() map[string]any { b := validBody(); delete(b, "position"); return b }(), "position"},
		{"坐标类型错误", func() map[string]any { b := validBody(); b["position"] = "abc"; return b }(), "position"},
		{"非法 JSON", "{bad json", ""},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			rec := do(t, h, "POST", "/api/issues", c.body)
			if rec.Code != http.StatusBadRequest {
				t.Fatalf("code = %d body=%s", rec.Code, rec.Body)
			}
			var e errorBody
			_ = json.Unmarshal(rec.Body.Bytes(), &e)
			if e.Error.Code == "" || e.Error.Field != c.field {
				t.Fatalf("错误信息不明确: %s", rec.Body)
			}
		})
	}

	// 状态枚举错误
	rec := do(t, h, "POST", "/api/issues", validBody())
	var created issue.Issue
	_ = json.Unmarshal(rec.Body.Bytes(), &created)
	rec = do(t, h, "PATCH", "/api/issues/"+created.ID+"/status", map[string]string{"status": "done"})
	if rec.Code != http.StatusBadRequest {
		t.Fatalf("错误状态应返回 400，实际 %d", rec.Code)
	}
	// 不存在的 ID
	rec = do(t, h, "PATCH", "/api/issues/not-exist/status", map[string]string{"status": "resolved"})
	if rec.Code != http.StatusNotFound {
		t.Fatalf("不存在的 ID 应返回 404，实际 %d", rec.Code)
	}
}

// 模拟 Go 服务重启：关闭后用同一个数据库文件重新打开，数据仍在
func TestPersistenceAcrossRestart(t *testing.T) {
	db := filepath.Join(t.TempDir(), "t.db")
	h, st := newTestServer(t, db)
	rec := do(t, h, "POST", "/api/issues", validBody())
	var created issue.Issue
	_ = json.Unmarshal(rec.Body.Bytes(), &created)
	st.Close()

	h2, st2 := newTestServer(t, db)
	defer st2.Close()
	rec = do(t, h2, "GET", "/api/issues/"+created.ID, nil)
	if rec.Code != 200 {
		t.Fatalf("重启后找不到数据: %d %s", rec.Code, rec.Body)
	}
}

func TestCORSPreflight(t *testing.T) {
	h, st := newTestServer(t, filepath.Join(t.TempDir(), "t.db"))
	defer st.Close()
	rec := do(t, h, "OPTIONS", "/api/issues/x/status", nil)
	if rec.Code != http.StatusNoContent || rec.Header().Get("Access-Control-Allow-Origin") == "" {
		t.Fatalf("CORS 预检失败: %d %v", rec.Code, rec.Header())
	}
}
