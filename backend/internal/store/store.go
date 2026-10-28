// Package store 负责问题数据的持久化，使用 SQLite（纯 Go 驱动，无需 CGO）。
package store

import (
	"context"
	"crypto/rand"
	"database/sql"
	"encoding/hex"
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"time"

	"github.com/holocode/ar-inspection/backend/internal/issue"

	_ "modernc.org/sqlite"
)

// timeLayout 固定 UTC + 毫秒格式，保证字符串排序等价于时间排序
const timeLayout = "2006-01-02T15:04:05.000Z07:00"

const schema = `
CREATE TABLE IF NOT EXISTS issues (
	id          TEXT PRIMARY KEY,
	title       TEXT NOT NULL,
	description TEXT NOT NULL DEFAULT '',
	priority    TEXT NOT NULL CHECK (priority IN ('low','medium','high')),
	status      TEXT NOT NULL CHECK (status IN ('open','in_progress','resolved')),
	pos_x       REAL NOT NULL,
	pos_y       REAL NOT NULL,
	pos_z       REAL NOT NULL,
	created_at  TEXT NOT NULL,
	updated_at  TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_issues_created_at ON issues(created_at);
`

// Store 问题仓储
type Store struct {
	db  *sql.DB
	now func() time.Time // 便于测试替换
}

// ListFilter 列表筛选条件，空字符串表示不筛选
type ListFilter struct {
	Status   string
	Priority string
}

// Open 打开（不存在则创建）SQLite 数据库并建表。
// 对应任务书 3.3.7: 数据在 Go 服务重启后仍然存在。
// 技术选型考量：
// 1. 采用 pure-Go modernc.org/sqlite 驱动，无需安装 GCC/CGO 工具链，跨平台开箱即用；
// 2. 启用 WAL (Write-Ahead Logging) 模式，大幅提升读写性能并杜绝写入并发死锁；
// 3. 设置 busy_timeout=5000，遇到锁争用时自动等待而非立即报错。
func Open(path string) (*Store, error) {
	if dir := filepath.Dir(path); dir != "" {
		if err := os.MkdirAll(dir, 0o755); err != nil {
			return nil, fmt.Errorf("创建数据目录失败: %w", err)
		}
	}
	dsn := fmt.Sprintf("file:%s?_pragma=journal_mode(WAL)&_pragma=busy_timeout(5000)&_pragma=foreign_keys(1)", filepath.ToSlash(path))
	db, err := sql.Open("sqlite", dsn)
	if err != nil {
		return nil, err
	}
	// SQLite 单写者，限制为 1 个连接最简单可靠
	db.SetMaxOpenConns(1)
	if _, err := db.Exec(schema); err != nil {
		db.Close()
		return nil, fmt.Errorf("初始化表结构失败: %w", err)
	}
	return &Store{db: db, now: time.Now}, nil
}

// Close 关闭数据库
func (s *Store) Close() error { return s.db.Close() }

// Ping 检查数据库是否可用（健康检查用）
func (s *Store) Ping(ctx context.Context) error { return s.db.PingContext(ctx) }

// Count 问题总数
func (s *Store) Count(ctx context.Context) (int, error) {
	var n int
	err := s.db.QueryRowContext(ctx, `SELECT COUNT(*) FROM issues`).Scan(&n)
	return n, err
}

// Create 新建问题：由后端生成 ID、时间戳，状态默认 open
func (s *Store) Create(ctx context.Context, in issue.CreateInput) (issue.Issue, error) {
	now := s.now().UTC().Format(timeLayout)
	it := issue.Issue{
		ID:          newID(),
		Title:       in.Title,
		Description: in.Description,
		Priority:    in.Priority,
		Status:      issue.StatusOpen,
		Position:    *in.Position,
		CreatedAt:   now,
		UpdatedAt:   now,
	}
	_, err := s.db.ExecContext(ctx, `
		INSERT INTO issues (id, title, description, priority, status, pos_x, pos_y, pos_z, created_at, updated_at)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
		it.ID, it.Title, it.Description, it.Priority, it.Status,
		it.Position.X, it.Position.Y, it.Position.Z, it.CreatedAt, it.UpdatedAt)
	if err != nil {
		return issue.Issue{}, err
	}
	return it, nil
}

// List 按创建时间倒序返回问题列表
func (s *Store) List(ctx context.Context, f ListFilter) ([]issue.Issue, error) {
	q := `SELECT id, title, description, priority, status, pos_x, pos_y, pos_z, created_at, updated_at FROM issues WHERE 1=1`
	var args []any
	if f.Status != "" {
		q += ` AND status = ?`
		args = append(args, f.Status)
	}
	if f.Priority != "" {
		q += ` AND priority = ?`
		args = append(args, f.Priority)
	}
	q += ` ORDER BY created_at DESC`

	rows, err := s.db.QueryContext(ctx, q, args...)
	if err != nil {
		return nil, err
	}
	defer rows.Close()

	items := make([]issue.Issue, 0) // 保证空列表序列化为 [] 而不是 null
	for rows.Next() {
		it, err := scan(rows)
		if err != nil {
			return nil, err
		}
		items = append(items, it)
	}
	return items, rows.Err()
}

// Get 按 ID 查询
func (s *Store) Get(ctx context.Context, id string) (issue.Issue, error) {
	row := s.db.QueryRowContext(ctx, `
		SELECT id, title, description, priority, status, pos_x, pos_y, pos_z, created_at, updated_at
		FROM issues WHERE id = ?`, id)
	it, err := scan(row)
	if errors.Is(err, sql.ErrNoRows) {
		return issue.Issue{}, issue.ErrNotFound
	}
	return it, err
}

// UpdateStatus 修改状态并刷新 updatedAt
func (s *Store) UpdateStatus(ctx context.Context, id, status string) (issue.Issue, error) {
	now := s.now().UTC().Format(timeLayout)
	res, err := s.db.ExecContext(ctx,
		`UPDATE issues SET status = ?, updated_at = ? WHERE id = ?`, status, now, id)
	if err != nil {
		return issue.Issue{}, err
	}
	if n, _ := res.RowsAffected(); n == 0 {
		return issue.Issue{}, issue.ErrNotFound
	}
	return s.Get(ctx, id)
}

// Delete 删除问题
func (s *Store) Delete(ctx context.Context, id string) error {
	res, err := s.db.ExecContext(ctx, `DELETE FROM issues WHERE id = ?`, id)
	if err != nil {
		return err
	}
	if n, _ := res.RowsAffected(); n == 0 {
		return issue.ErrNotFound
	}
	return nil
}

type scanner interface{ Scan(dest ...any) error }

func scan(r scanner) (issue.Issue, error) {
	var it issue.Issue
	err := r.Scan(&it.ID, &it.Title, &it.Description, &it.Priority, &it.Status,
		&it.Position.X, &it.Position.Y, &it.Position.Z, &it.CreatedAt, &it.UpdatedAt)
	return it, err
}

// newID 生成 UUID v4 字符串（基于 crypto/rand，不引入额外依赖）
func newID() string {
	var b [16]byte
	if _, err := rand.Read(b[:]); err != nil {
		panic(err) // crypto/rand 失败属于系统级故障
	}
	b[6] = (b[6] & 0x0f) | 0x40 // version 4
	b[8] = (b[8] & 0x3f) | 0x80 // variant 10
	h := hex.EncodeToString(b[:])
	return h[0:8] + "-" + h[8:12] + "-" + h[12:16] + "-" + h[16:20] + "-" + h[20:32]
}
