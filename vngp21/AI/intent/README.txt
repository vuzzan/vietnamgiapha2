Prompt intent cho Qwen — VietNamGiaPha
========================================

Thư mục: ai\intent\ (cạnh file .exe khi chạy)

Rule engine (ưu tiên): ai\rules\ — xem README trong đó.
Qwen (khi rule fail): bấm 📁 mở ai\, sửa intent\*.txt, ↻ Rules.
(Sửa trong project ai\intent\ thì build lại để copy bản mới ra bin\Debug\ai\intent\.)

Các file:

  system-prompt.txt    Vai trò, phạm vi cho phép/cấm, quy tắc JSON
  intent-mapping.txt   Ánh xạ câu tiếng Việt → kind (mỗi dòng: Kind: mẫu câu)
  examples.txt         Ví dụ few-shot (Câu: ... rồi dòng JSON)
  user-prompt.txt      Phần đầu prompt gửi kèm câu hỏi (trước ngữ cảnh phả đồ)
  blocked-keywords.txt Từ khóa ngoài phạm vi gia phả (mỗi dòng 1 cụm, không dấu)
                       Dòng bắt đầu # là ghi chú

Phần tự động ghép vào system prompt (không sửa trong txt):
  - WHITELIST kind (đồng bộ code GiaPhaIntentKinds)
  - SCHEMA JSON mẫu

Log theo dõi: logs\ai-intent-trace.log
