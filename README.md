# Google NAS — Windows x64

Bộ cài và nguồn cập nhật chính thức cho Google NAS.

[Tải bộ cài mới nhất](https://github.com/cyan881990/google-nas-updates/releases/latest/download/GoogleNasSetup.exe)

## Cập nhật từ phiên bản cũ

Nhập `cyan881990/google-nas-updates` vào ô repository trong trang Cập nhật, lưu cấu hình và bấm kiểm tra cập nhật. Từ bản 1.2.2, nguồn này đã được gắn sẵn và ẩn trong giao diện.

## Các tệp phát hành

- `GoogleNasSetup.exe`: bộ cài Windows x64 tích hợp rclone, WinFsp và .NET Framework 4.8 offline.
- `GoogleNas.exe` và `GoogleNas.exe.sha256`: các tệp cho updater ứng dụng.
- `GoogleNasSetup.exe.sha256`: mã SHA256 của bộ cài.

Google NAS gắn Google Drive thành ổ đĩa Explorer và hỗ trợ chia sẻ thư mục ổ cứng qua Tailscale cùng SMB mã hóa. Tailscale được tải từ nhà cung cấp khi bật tính năng NAS từ xa.

Cập nhật ứng dụng giữ runtime hiện có; dùng bộ cài đầy đủ nếu cần nâng runtime hoặc driver. Thoát an toàn trước khi chạy bộ cài. Dữ liệu tài khoản, cấu hình và cache được giữ nguyên.
