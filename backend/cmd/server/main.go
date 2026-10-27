// 巡检问题标注后端入口
package main

import (
	"context"
	"errors"
	"flag"
	"log"
	"net"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"github.com/holocode/ar-inspection/backend/internal/api"
	"github.com/holocode/ar-inspection/backend/internal/store"
)

func main() {
	addr := flag.String("addr", envOr("ADDR", ":8080"), "监听地址；:8080 表示监听所有网卡，手机可通过局域网 IP 访问")
	dbPath := flag.String("db", envOr("DB_PATH", "data/issues.db"), "SQLite 数据库文件路径")
	origin := flag.String("cors", envOr("CORS_ORIGIN", "*"), "CORS 允许的来源")
	flag.Parse()

	st, err := store.Open(*dbPath)
	if err != nil {
		log.Fatalf("打开数据库失败: %v", err)
	}
	defer st.Close()

	srv := &http.Server{
		Addr:              *addr,
		Handler:           api.NewServer(st, *origin).Handler(),
		ReadHeaderTimeout: 5 * time.Second,
		// 不设置 WriteTimeout，否则 SSE 长连接会被强制断开
	}

	go func() {
		log.Printf("服务已启动 %s，数据库 %s", *addr, *dbPath)
		printLANAddrs(*addr)
		if err := srv.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
			log.Fatalf("监听失败: %v", err)
		}
	}()

	// 优雅退出：Ctrl+C 后等待进行中的请求完成，再关闭数据库
	quit := make(chan os.Signal, 1)
	signal.Notify(quit, os.Interrupt, syscall.SIGTERM)
	<-quit
	log.Println("正在关闭服务...")
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	_ = srv.Shutdown(ctx)
}

func envOr(key, def string) string {
	if v := os.Getenv(key); v != "" {
		return v
	}
	return def
}

// printLANAddrs 打印本机局域网 IPv4，方便在 Unity 里填写后端地址
func printLANAddrs(addr string) {
	_, port, err := net.SplitHostPort(addr)
	if err != nil {
		return
	}
	ifaces, _ := net.Interfaces()
	for _, ifc := range ifaces {
		if ifc.Flags&net.FlagUp == 0 || ifc.Flags&net.FlagLoopback != 0 {
			continue
		}
		addrs, _ := ifc.Addrs()
		for _, a := range addrs {
			if ipn, ok := a.(*net.IPNet); ok && ipn.IP.To4() != nil && ipn.IP.IsPrivate() {
				log.Printf("  局域网地址（%s）: http://%s:%s", ifc.Name, ipn.IP, port)
			}
		}
	}
}
