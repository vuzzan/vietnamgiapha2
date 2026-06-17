Rule base (engine C#) — VietNamGiaPha
======================================

Thư mục: ai\rules\ (cạnh file .exe)

Rule engine đọc file mỗi lần parse — sau khi sửa: chat AI → ↻ Rules.

Các file:

  top-intent.txt           Intent không cần tên (ThuyTo, CountPeople, CurrentFamily...)
  detail-intent.txt        Intent theo tên / chi tiết (Children, Spouse, Search...)
  edit-actions.txt         Lệnh sửa gia phả (AddPerson, AddChildFamily, RenamePerson...)
  extract-name-prefixes.txt  Bỏ đầu câu khi tách tên (mỗi dòng 1 cụm, có dấu OK)
  extract-name-suffixes.txt  Bỏ cuối câu khi tách tên
  stopwords.txt            Chào hỏi / không phải tên — rule fail → Qwen

Định dạng top-intent.txt / detail-intent.txt:

  Kind: cụm1, cụm2, cụm3
  Kind|ancestorLevels=2: ong noi, ba noi
  Kind|spouse=Vợ: vo cua, vo la ai
  Kind|useCurrentFamily: nguoi nay, gia dinh nay

  - Cụm từ không dấu (app tự bỏ dấu câu hỏi trước khi so khớp)
  - Dòng # là ghi chú
  - Thứ tự dòng = ưu tiên (dòng trên khớp trước)

Prefix/suffix:
  - App tự sắp dài → ngắn khi load
  - Giữ cả bản có dấu và không dấu nếu cần

Vẫn nằm trong code (chưa đưa ra txt):
  - Regex năm sinh/mất, đời số (doi 4)
  - Chuẩn hóa câu dài (TryNormalizeAiLaQuestion)
  - Quan hệ 2 tên (A và B)

Qwen prompt: ai\intent\ (tách riêng — chỉ khi rule fail)
