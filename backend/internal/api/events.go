package api

import (
	"encoding/json"
	"sync"
)

// Event 推送给前端的实时事件
type Event struct {
	Type string `json:"type"` // issue.created / issue.updated / issue.deleted
	Data any    `json:"data"`
}

// Broker 简单的 SSE 广播器：每个订阅者一个带缓冲的 channel
type Broker struct {
	mu   sync.Mutex
	subs map[chan []byte]struct{}
}

// NewBroker 创建广播器
func NewBroker() *Broker {
	return &Broker{subs: make(map[chan []byte]struct{})}
}

// Subscribe 订阅，返回接收 channel 和取消函数
func (b *Broker) Subscribe() (<-chan []byte, func()) {
	ch := make(chan []byte, 16)
	b.mu.Lock()
	b.subs[ch] = struct{}{}
	b.mu.Unlock()
	return ch, func() {
		b.mu.Lock()
		if _, ok := b.subs[ch]; ok {
			delete(b.subs, ch)
			close(ch)
		}
		b.mu.Unlock()
	}
}

// Publish 广播事件；订阅者处理不过来时丢弃该条，避免阻塞业务请求
func (b *Broker) Publish(e Event) {
	payload, err := json.Marshal(e)
	if err != nil {
		return
	}
	b.mu.Lock()
	defer b.mu.Unlock()
	for ch := range b.subs {
		select {
		case ch <- payload:
		default:
		}
	}
}

// Count 当前订阅者数量
func (b *Broker) Count() int {
	b.mu.Lock()
	defer b.mu.Unlock()
	return len(b.subs)
}
