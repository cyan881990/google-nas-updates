# Bắt đầu với CYAN NAS

## Cài đặt và đăng nhập
Tải CyanNasSetup.exe tại trang sản phẩm. Thoát an toàn bản cũ trước khi nâng cấp. Bộ cài tích hợp rclone, WinFsp và .NET Framework 4.8 offline. Chọn **Tài khoản CYANPRO** trên thanh tiêu đề để đăng nhập bằng email và mật khẩu tài khoản CYANPRO, không phải mật khẩu NAS hay mã PIN Windows.

Mật khẩu CYANPRO không được lưu. Refresh token và thông tin license được mã hóa bằng Windows DPAPI. License được kiểm tra khi mở app và mỗi 6 giờ; khi mất mạng có thể dùng lần xác thực hợp lệ tối đa 72 giờ, trong giới hạn hạn dùng license. Thiết bị bị thu hồi hoặc license hết hạn cần xác thực lại. Các ổ đang làm việc không bị cưỡng chế tháo vì lỗi license; bạn vẫn có thể ngắt ổ và thoát an toàn.

## SERVER — chia sẻ ổ cứng
1. Mở tab SERVER, chọn **Thêm chia sẻ** và chọn thư mục hoặc ổ dữ liệu.
2. Đặt tên chia sẻ; chọn tài khoản Windows/NAS và quyền đọc hoặc đọc/ghi.
3. Với tài khoản NAS mới, dùng mật khẩu đủ mạnh. Xác nhận quyền quản trị Windows để tạo chia sẻ.
4. Các ổ đã bật xuất hiện trong danh sách. Chọn từng ổ để xem dung lượng và thao tác.

Không chia sẻ thư mục hệ thống hoặc thư mục chứa cấu hình riêng của ứng dụng. Chia sẻ nhiều thư mục cùng một ổ không làm tăng dung lượng thực. Dừng NAS có kiểm tra file đang được truy cập; không ép đóng file.

## Kết nối từ xa qua Tailscale
Cài và đăng nhập Tailscale trên cả hai máy trong cùng mạng riêng. Máy chứa ổ phải bật và không ngủ. Tại máy CLIENT, nhập IP Tailscale, tên chia sẻ và tài khoản/mật khẩu NAS do máy SERVER cung cấp. Mật khẩu Windows/NAS khác với tài khoản CYANPRO; mã PIN Windows không thay thế mật khẩu NAS.

## CLIENT — gắn NAS, máy chủ và cloud
1. Mở tab CLIENT, bấm **Thêm ổ đĩa**.
2. Chọn nguồn: SMB/NAS, FTP, FTPS, SFTP, WebDAV hoặc một nền tảng cloud.
3. Nhập địa chỉ, đường dẫn và thông tin xác thực; với cloud OAuth, cấp quyền trong trình duyệt.
4. Kiểm tra kết nối, chọn ký tự ổ trống, lưu và kết nối.

SFTP cần known_hosts đã xác minh. FTPS kiểm tra chứng chỉ TLS. Cấu hình tự kết nối chỉ hoạt động sau khi xác thực thành công. **Tạm ngắt** được lưu; dùng **Kết nối lại** khi muốn bật lại. App cố phục hồi sau khi đổi mạng, nhưng không tự bỏ cache đang có dữ liệu chờ upload.

## Dung lượng và trạng thái
SERVER đọc dung lượng ổ vật lý chứa thư mục. CLIENT hiển thị quota do nguồn cung cấp; một số giao thức không có quota. Trạng thái không đọc được dung lượng không đồng nghĩa ổ đã hết chỗ.

## License và thiết bị
Mở **Tài khoản / license** để xem gói, hạn dùng, trạng thái và số thiết bị. **Kiểm tra license** xác thực lại với server. **Đăng xuất thiết bị** trả lại suất thiết bị trên CYANPRO; ngắt các ổ đang dùng trước khi chuyển sang tài khoản khác. Giới hạn và chính sách gói do CYANPRO quản lý, không cố định trong app.

## Cập nhật và thoát an toàn
Vào **Cài đặt → Kiểm tra cập nhật**. CYANPRO cung cấp phiên bản, ghi chú và link bộ cài; file tải hiện lưu trên GitHub Releases. App xác minh SHA256 và số phiên bản. Thoát an toàn trước khi chạy bộ cài. Không kết thúc rclone bằng Task Manager khi còn dữ liệu chờ upload.
