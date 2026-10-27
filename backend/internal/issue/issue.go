// Package issue 定义巡检问题的数据结构、枚举值和参数校验规则。
// 三端（Unity / Go / React）使用相同的 JSON 字段名。
package issue

import (
	"errors"
	"strings"
	"unicode/utf8"
)

// 优先级枚举
const (
	PriorityLow    = "low"
	PriorityMedium = "medium"
	PriorityHigh   = "high"
)

// 处理状态枚举
const (
	StatusOpen       = "open"
	StatusInProgress = "in_progress"
	StatusResolved   = "resolved"
)

// MaxTitleLen 标题最大字符数（按 Unicode 字符计）
const MaxTitleLen = 100

// MaxDescriptionLen 描述最大字符数
const MaxDescriptionLen = 1000

// Position 标记在 AR 会话坐标系中的位置（单位：米）
type Position struct {
	X float64 `json:"x"`
	Y float64 `json:"y"`
	Z float64 `json:"z"`
}

// Issue 巡检问题
type Issue struct {
	ID          string   `json:"id"`
	Title       string   `json:"title"`
	Description string   `json:"description"`
	Priority    string   `json:"priority"`
	Status      string   `json:"status"`
	Position    Position `json:"position"`
	CreatedAt   string   `json:"createdAt"`
	UpdatedAt   string   `json:"updatedAt"`
}

// CreateInput 新建问题的请求体。
// Position 使用指针，用来区分"没传"和"传了 0,0,0"。
type CreateInput struct {
	Title       string    `json:"title"`
	Description string    `json:"description"`
	Priority    string    `json:"priority"`
	Position    *Position `json:"position"`
}

// UpdateStatusInput 修改状态的请求体
type UpdateStatusInput struct {
	Status string `json:"status"`
}

// ValidationError 参数校验错误，Field 指出是哪个字段
type ValidationError struct {
	Field   string `json:"field"`
	Message string `json:"message"`
}

func (e *ValidationError) Error() string { return e.Field + ": " + e.Message }

// IsValidPriority 判断优先级是否合法
func IsValidPriority(p string) bool {
	return p == PriorityLow || p == PriorityMedium || p == PriorityHigh
}

// IsValidStatus 判断状态是否合法
func IsValidStatus(s string) bool {
	return s == StatusOpen || s == StatusInProgress || s == StatusResolved
}

// Normalize 去掉标题和描述首尾空白，然后校验。
func (in *CreateInput) Normalize() error {
	in.Title = strings.TrimSpace(in.Title)
	in.Description = strings.TrimSpace(in.Description)
	in.Priority = strings.TrimSpace(in.Priority)

	switch {
	case in.Title == "":
		return &ValidationError{"title", "标题不能为空"}
	case utf8.RuneCountInString(in.Title) > MaxTitleLen:
		return &ValidationError{"title", "标题不能超过 100 个字符"}
	case utf8.RuneCountInString(in.Description) > MaxDescriptionLen:
		return &ValidationError{"description", "描述不能超过 1000 个字符"}
	case !IsValidPriority(in.Priority):
		return &ValidationError{"priority", "优先级必须是 low、medium、high 之一"}
	case in.Position == nil:
		return &ValidationError{"position", "缺少标记坐标 position"}
	}
	return nil
}

// Validate 校验状态修改请求
func (in *UpdateStatusInput) Validate() error {
	in.Status = strings.TrimSpace(in.Status)
	if !IsValidStatus(in.Status) {
		return &ValidationError{"status", "状态必须是 open、in_progress、resolved 之一"}
	}
	return nil
}

// ErrNotFound 问题不存在
var ErrNotFound = errors.New("issue not found")
