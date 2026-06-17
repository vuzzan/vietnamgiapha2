AI local (Qwen qua llama-server) — VietNamGiaPha
================================================

Chương trình tự khởi động llama-server khi bạn chọn chế độ
"Cài đặt AI → Qwen trên máy" và gửi câu hỏi. Bạn KHÔNG cần mở terminal.

Cấu trúc thư mục (cạnh VietNamGiaPha2.exe):

  ai\
    llama-server.exe     ← tải từ bản release llama.cpp (Windows x64)
    manifest.json        ← cổng và tên file model mặc định
    rules\               ← rule base engine (top/detail intent, tách tên) — ↻ Rules
    intent\              ← prompt Qwen khi rule fail (file .txt)
    models\
      *.gguf             ← model Qwen3 (ví dụ qwen3-4b-instruct-q4_k_m.gguf)

Tải file (hoặc dùng nút trong app: Cài đặt AI → Qwen trên máy):
  • llama-server: Cài đặt AI → "Tải llama-server" (GitHub releases, win x64, b5092+)
  • model GGUF: "Tải model Qwen3-4B GGUF" — copy file .gguf vào ai\models\

Gợi ý RAM:
  - Qwen3-1.7B / 4B Q4_K_M: máy 8–16 GB RAM
  - Qwen3-8B Q4: nên 16 GB+ RAM

Sau khi copy file, mở app → Cài đặt AI → Qwen trên máy → "Khởi động & thử".
