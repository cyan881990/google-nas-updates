# CYAN NAS cho Windows — v1.4.1

v1.4.1 nhận sự kiện thay đổi địa chỉ mạng, mất/có mạng và máy thức dậy; gộp sự kiện trong 3 giây rồi kiểm tra ngay các NAS tự kết nối. Lỗi mạng tạm thời thử lại sau 10 giây thay vì 60 giây, dùng đăng nhập đã lưu. Xử lý ký tự ổ SMB cũ (mã 85) chỉ khi đúng mapping của NAS và không ép đóng file đang mở. Mỗi ổ giữ một worker, kể cả khi đổi mạng liên tục. Không tự bật lại ổ đã Tạm ngắt hoặc cấu hình chưa xác thực; không mở hộp đăng nhập Windows. Kiểm thử mô phỏng mất Wi-Fi, mạng phục hồi, mapping cũ, ổ bận và xung đột ký tự ổ.

v1.4.0 quản lý nhiều NAS bằng danh sách riêng: ổ chia sẻ local và ổ gắn từ máy khác. Trạng thái từng ổ (đang chia sẻ, đã kết nối, chờ mạng, tạm ngắt, cần sửa đăng nhập, ký tự bị chiếm) nằm ngay trong danh sách; chọn một dòng để xem cấu hình. NAS đã bật không thể bật trùng, phần cấu hình local được khóa khi đang chia sẻ. Chia sẻ mới giữ các ổ cũ; Làm mới trên trang chia sẻ đọc các NAS thuộc đúng tài khoản Windows đang quản lý (có UAC). Tự gắn lại các mapping đã lưu khi mở tool và retry mạng mỗi phút; mỗi ổ có một worker riêng, ổ chậm không chặn ổ khác hoặc UI. Không chiếm ký tự ổ khác. Tạm ngắt được lưu qua lần khởi động sau; Gắn lại bật tự kết nối. Khi Windows từ chối đăng nhập, dừng tự thử để tránh khóa tài khoản. Lưu đăng nhập trong app mã hóa bằng DPAPI, chỉ bật auto sau lần gắn thành công; không hiện Windows Security/VPS. Lần cấu hình NAS đầu bật khởi chạy cùng Windows, có thể tắt tại Cài đặt. Giữ cấu hình từ phiên bản trước. Tailscale cần đã đăng nhập và máy chứa ổ cần bật để truy cập; SMB share local vốn được Windows lưu bền vững.

v1.3.3 khôi phục hộp đăng nhập Windows Security khi gắn NAS lần đầu hoặc khi chưa có mật khẩu phù hợp; có nút Đăng nhập lại để chủ động mở lại. Thông tin chỉ được lưu bằng DPAPI và Credential Manager sau khi SMB kết nối thành công; bấm Hủy không thay thông tin cũ. Kiểm tra nền không mở hộp đăng nhập. Thêm Đặt lại mật khẩu Windows cho tài khoản NAS riêng do tool tạo, có xác nhận trước khi thực hiện và UAC; không cần dừng chia sẻ, không xóa tài khoản, không ngắt phiên đang mở. Chặn tài khoản cá nhân, tài khoản đang điều khiển tool, tài khoản bị vô hiệu hóa và NAS không thuộc người quản lý. Nút Dừng/Kiểm tra dùng tên NAS đã lưu, không dùng ô tên mới đang sửa; gắn NAS từ xa không ghi đè cấu hình chia sẻ local. Dừng NAS vẫn bảo vệ file đang mở và không xóa tài khoản.

v1.3.2 bổ sung Kiểm tra mật khẩu Windows trực tiếp trên máy chia sẻ bằng đăng nhập mạng local, không đổi mật khẩu hoặc quyền. Thêm Sao chép tài khoản lấy tên máy Windows thật, có thể khác tên hiển thị Tailscale. Lỗi kết nối giữ mã Windows và phân biệt mật khẩu, thiếu quyền SMB/NTFS, khóa/vô hiệu hóa tài khoản, giới hạn đăng nhập, kết nối tài khoản khác. Không tự thử lại mật khẩu hoặc xóa credential/kết nối khác. Thành công kiểm tra mật khẩu local chưa xác nhận toàn bộ quyền truy cập SMB từ máy khác.

v1.3.1 sửa lỗi giao diện khi Windows phóng to DPI: chỉ co giãn một lần lúc khởi động, chờ co giãn toàn bộ cây control rồi mới sắp xếp thẻ. Khắc phục ô nhập biến mất/co nhỏ, thẻ kéo dài và thanh bên bị cắt chữ. Kiểm thử thêm co giãn trước lần mở cửa sổ đầu tiên và dùng manifest DPI thật của app.

v1.3.0 thiết kế lại giao diện desktop phong cách Apple: cửa sổ không viền Windows, nút đóng/thu nhỏ/phóng to đỏ-vàng-xanh, thanh bên sáng, thẻ/nút/ô nhập bo góc. Icon riêng kết hợp đám mây và ổ lưu trữ, đồng bộ cho app, taskbar, khay hệ thống, shortcut và bộ cài; tệp ICO có 9 kích thước 16–256 px. Thanh bên tự thu gọn khi cửa sổ hẹp hoặc DPI cao. Có thể kéo thanh tiêu đề và đổi kích thước từ cạnh/góc cửa sổ. Thiết kế quanh hai chức năng chính: Google Drive và Ổ cứng thành NAS. Gộp cấu hình Google Drive cùng trạng thái ổ; tách riêng Kết nối NAS, Mạng Tailscale, Cài đặt và Trợ giúp. Bố cục hai cột trên PC, tự chuyển một cột ở cửa sổ hẹp, cuộn dọc và hỗ trợ DPI 100–200%. Mật khẩu có nút hiện/ẩn ngay cạnh ô nhập, tên cấu hình Google nâng cao được thu gọn. Thao tác người dùng được chờ tác vụ kiểm tra nền thay vì bỏ qua lần bấm. Giữ cấu hình, dữ liệu và thông tin đăng nhập từ các bản trước.

v1.2.9 thêm tài khoản/mật khẩu tại Gắn ổ từ máy khác. Nhập lần đầu rồi bấm Gắn / kết nối lại: chỉ khi kết nối thành công, thông tin được lưu bằng DPAPI trong nas-passwords.dat, gắn với đúng IP và tên share, đồng thời lưu credential cho IP đó trong Windows Credential Manager. Gắn lại và kiểm tra nền dùng thông tin đã lưu, không bật hộp hỏi mật khẩu. Có nút Hiện mật khẩu đã lưu. Đổi tài khoản hoặc mật khẩu trên máy chứa NAS thì cần nhập thông tin mới; tool không ép ngắt kết nối tài khoản khác. Nếu chính sách Windows không cho lưu credential, tool báo rõ và tiếp tục dùng bản mã hóa của tool khi ứng dụng chạy.

v1.2.8 chuyển kiểm tra ổ mạng và kiểm tra cache sang nền, giới hạn chờ kiểm tra ổ để cửa sổ vẫn mở lại được khi ổ mất kết nối. Khôi phục biểu tượng khay khi Explorer khởi động lại. Thêm Hiện mật khẩu, Ẩn mật khẩu và Lưu mật khẩu cho tài khoản NAS. Khi tạo tài khoản mới thành công, mật khẩu được lưu tự động trong %LOCALAPPDATA%\GoogleNas\nas-passwords.dat bằng Windows DPAPI CurrentUser. Tệp nằm trong thư mục dữ liệu riêng có ACL, chỉ giải mã dưới cùng tài khoản Windows trên máy đã lưu. Tài khoản NAS đã tạo ở bản cũ cần nhập và lưu mật khẩu một lần; tool không đọc được mật khẩu gốc từ Windows. Lưu mật khẩu của tài khoản có sẵn không đổi mật khẩu Windows.

v1.2.7 kiểm tra các ổ NAS từ xa mỗi phút và tự khôi phục kết nối khi mạng trở lại bằng thông tin đăng nhập Windows đã lưu. Kiểm tra chạy nền, không chặn giao diện, không ép ngắt file hoặc thay ổ khác. Bản nâng cấp tiếp tục giám sát ổ NAS cuối đã lưu; các ổ khác cần bấm Gắn / kết nối lại một lần để được ghi nhận. Máy chứa NAS phải đang bật, Tailscale và quyền truy cập vẫn còn hiệu lực.

v1.2.6 đổi tên phần mềm, bộ cài, khay hệ thống và shortcut thành CYAN NAS. Giữ định danh dữ liệu và tài sản cập nhật cũ để nâng cấp từ Google NAS mà không mất cấu hình.

v1.2.5 sửa dừng NAS SMB bị chặn do handle thư mục Explorer; chỉ chặn file thật và hiển thị máy/tài khoản đang giữ file. Bản này cũng sửa cài đè bị lỗi thay tệp: dùng bản sao lưu riêng mỗi lần cài, bỏ qua tệp không đổi và báo rõ đường dẫn bị khóa. Bản này cũng sửa lỗi Thoát an toàn bị chặn khi ổ đang rảnh: không dùng số tham chiếu VFS để suy ra file đang mở, đồng thời kiểm tra metadata chưa upload và file cache đang bị giữ. Bản này ẩn ô nhập repository khỏi giao diện Cập nhật. Nguồn cập nhật được gắn trong `Core.UpdateRepository` khi đóng gói. Nguồn cập nhật chính thức: `cyan881990/google-nas-updates`. Bản cũ có ô repository nhập giá trị này để cập nhật.

v1.2.0 bổ sung **NAS từ xa**: chia sẻ thư mục/ổ cứng NTFS/ReFS trên máy này và gắn NAS từ máy khác qua Tailscale. Cài trên cả hai máy; không cần mở port trên router. Tool tự tải MSI Tailscale 1.104.1 từ nguồn chính thức khi bấm **Cài Tailscale**, kiểm tra SHA256 và chữ ký Tailscale Inc. Bộ cài Google Drive vẫn chứa các thành phần offline; Tailscale cần Internet trong lần cài đầu và không được đóng gói lại vào EXE.

## NAS ổ cứng qua Internet

1. Trên cả hai máy: mở **Mạng Tailscale → Cài Tailscale → Đăng nhập**. Đăng nhập vào cùng mạng Tailscale. Mật khẩu tài khoản Google/Tailscale chỉ nhập trong trình duyệt.
2. Máy chứa ổ: chọn thư mục hoặc ổ dữ liệu, đặt tên NAS và tài khoản local Windows. Mặc định tạo tài khoản NAS riêng; nhập mật khẩu ≥12 ký tự gồm chữ hoa, thường, số và ký hiệu. Nếu dùng tài khoản local đã tồn tại, bỏ chọn tạo tài khoản; tool không đổi mật khẩu của tài khoản đó. Chọn chỉ đọc nếu cần, bấm **Bật chia sẻ NAS** và chấp nhận UAC dưới cùng tài khoản Windows quản trị đang chạy tool.
3. Tool hiển thị `\\100.x.x.x\TenNAS` và `TEN-MAY\taikhoan`. Trên máy từ xa: nhập IP/tên NAS, chọn ký tự ổ trống → **Kết nối NAS → Gắn / kết nối lại**. Nhập tài khoản/mật khẩu NAS trong hộp thoại Windows; mã PIN Windows không thay thế mật khẩu. Kết nối ổ được ghi vào profile Windows để thử kết nối lại sau khi đăng nhập.
4. Máy chứa ổ phải bật và không ngủ. Chia sẻ SMB tồn tại độc lập với GUI. Bấm **Chạy khi đăng xuất** để bật Tailscale unattended nếu muốn truy cập sau khi người dùng đăng xuất. Không tự thay thiết lập ngủ/điện hoặc chính sách key expiry của Tailscale.
5. **Dừng chia sẻ** chỉ xóa chia sẻ do tool tạo cho tài khoản này và rule firewall tương ứng; từ chối khi còn file mở. Tài khoản local và quyền NTFS đã cấp được giữ. **Ngắt ổ NAS** không ép đóng file và không ngắt ổ trỏ tới share khác.

SMB share bật mã hóa SMB3, quyền share chỉ cấp cho tài khoản được chọn. Rule TCP 445 do tool tạo giới hạn IP/interface Tailscale và dải 100.64.0.0/10. Rule chia sẻ khác đã tồn tại của Windows không bị thay đổi; quyền NTFS/share và chính sách tailnet vẫn quyết định truy cập. Không hỗ trợ FAT/exFAT, junction/symlink/cloud placeholder hoặc chia sẻ toàn bộ ổ hệ thống C:. Tài khoản dùng Tailscale phải được bạn đăng nhập; không lưu auth key trong tool.

Mật khẩu tạo NAS chỉ chuyển cho helper bằng Windows DPAPI trong thư mục dữ liệu có ACL riêng, không ghi plaintext vào config, log hay command line. Nếu tạo NAS thất bại, tool hoàn tác share/rule/account mới và ACL thư mục. Gỡ GUI không tự xóa tài khoản hay share NAS đang dùng: dừng chia sẻ trong app trước khi gỡ nếu muốn tắt NAS.

Nguồn tích hợp: [Tailscale CLI](https://tailscale.com/docs/reference/tailscale-cli), [MSI chính thức](https://tailscale.com/docs/install/windows/msi), [Windows SMB](https://learn.microsoft.com/powershell/module/smbshare/).

Kiểm thử NAS gồm validate địa chỉ/tên, parse trạng thái đăng nhập, config không chứa mật khẩu, DPAPI, workflow admin với mock cmdlet mạng/tài khoản và ACL thật trong thư mục fixture. Chưa xác nhận truyền file giữa hai máy ở hai mạng Internet khác nhau; cần hai máy đã đăng nhập Tailscale để kiểm tra thực tế.

v1.1.3 bổ sung mốc DPI 96 cho giao diện ứng dụng/bộ cài, icon nhiều kích thước trong file EXE ứng dụng, setup và uninstaller. Đóng cửa sổ thu nhỏ ứng dụng trên taskbar; mở shortcut khi ứng dụng đã chạy sẽ mở lại cửa sổ. Nhấp icon khay hệ thống để mở, hoặc chọn Thông tin → Thoát an toàn để đóng ứng dụng.

v1.1.2 sửa lỗi Windows Script Host đổi tên shortcut “Gỡ CYAN NAS” thành “G? CYAN NAS”. Shortcut gỡ cài đặt dùng tên ASCII “Uninstall CYAN NAS”; đăng ký gỡ cài đặt và khởi chạy cùng Windows trước khi tạo shortcut. Nếu Start Menu không cho ghi, bộ cài hoàn tất và ghi cảnh báo tại `%LOCALAPPDATA%\GoogleNasSetup\logs\shortcuts.log`.

v1.1.1 sửa lỗi đứng ở bước WinFsp do truyền tham số msiexec trong ngoặc kép. Bộ cài hiển thị số giây chờ, giới hạn chờ WinFsp ở 3 phút và .NET ở 20 phút. Nếu quá hạn, giữ file tạm và không ép dừng tiến trình đang cài để tránh làm hỏng driver; kiểm tra nhật ký tại `%LOCALAPPDATA%\GoogleNasSetup\logs` trước khi thử lại.

Gắn Google Drive và NAS từ xa thành ổ đĩa trong Explorer. Giao diện tiếng Việt có Tổng quan, Cấu hình ổ đĩa, NAS từ xa, Cập nhật và Thông tin. Ứng dụng hỗ trợ tự gắn Google Drive sau khi đăng nhập Windows và cập nhật từ GitHub Releases.

## Cài đặt với một file

Chạy **CyanNasSetup.exe** trong thư mục `dist`, bấm **Cài đặt**. Không cần giải nén, tải rclone, tải WinFsp hay chạy setup thư viện riêng. File này chứa sẵn:

- rclone 1.75.2 x64, dùng trực tiếp từ thư mục `runtime` của ứng dụng.
- Bộ cài WinFsp 2.1.25156 chính thức, được cài tự động nếu chưa có driver.
- Bộ cài .NET Framework 4.8 offline, chỉ chạy nếu Windows chưa có 4.8 trở lên.
- Giấy phép rclone/WinFsp và mã nguồn WinFsp v2.1.

Bộ cài hoạt động offline. Đăng nhập Google, truy cập Drive và kiểm tra bản cập nhật cần Internet. Windows có thể hiển thị UAC để bạn cấp quyền cài driver/runtime. Nếu Windows yêu cầu khởi động lại, bộ cài thông báo và không tự ép reboot. Yêu cầu Windows 10/11 x64; không hỗ trợ ARM64 hoặc Windows 32-bit.

Ứng dụng cài cho tài khoản Windows đang chạy bộ cài tại `%LOCALAPPDATA%\Programs\GoogleNas`, có shortcut Start Menu và mục gỡ trong Windows Settings. Chỉ các thành phần hệ thống mới được nâng quyền, nên việc nhập tài khoản quản trị khác trong UAC không đổi tài khoản nhận ứng dụng. Không dùng “Run as different user” để mở chính bộ cài nếu muốn cài cho tài khoản hiện tại.

Nếu bản cũ đang chạy: bấm **^** gần đồng hồ → nhấp phải biểu tượng **CYAN NAS** → **Thoát an toàn**, rồi bấm Cài đặt lại. Từ v1.1.3 cũng có thể chọn **Thông tin → Thoát an toàn**. Bấm X chỉ thu nhỏ, chưa thoát. Bộ cài không cưỡng chế ngắt upload. Tài khoản, cấu hình và cache hiện có được giữ nguyên.

## Kết nối Google Drive

1. Mở CYAN NAS → **Kết nối Google**, đăng nhập trong trình duyệt và cấp quyền. Mặc định dùng My Drive và quyền đọc/ghi. Cửa sổ xác thực rclone có thể xuất hiện trong lần kết nối đầu tiên.
2. Trong **Cấu hình ổ đĩa**, chọn ký tự còn trống; để trống thư mục để dùng toàn bộ Drive, hoặc nhập `Documents/Projects` để chỉ gắn một thư mục. Chọn giới hạn cache và lưu.
3. Ở **Tổng quan**, bấm **Gắn ổ đĩa** → **Mở trong Explorer**.
4. Startup được bật mặc định khi cài (có thể bỏ chọn). Tùy chọn **Tự gắn ổ** chỉ thực hiện sau khi đã kết nối Google. Ứng dụng chạy sau khi người dùng đăng nhập Windows, không chạy trước màn hình đăng nhập.

Đóng cửa sổ để tiếp tục chạy ở tray. Chọn **Thoát an toàn** từ tray để ngắt ổ rồi thoát. Nếu còn file mở, hàng đợi upload hoặc lỗi upload, ứng dụng yêu cầu xử lý trước khi ngắt. Xem trạng thái upload/cache ở Tổng quan và nhật ký trong Thông tin.

## Cập nhật và GitHub

Trong **Cập nhật**, bấm kiểm tra hoặc bật tự cập nhật (mỗi 6 giờ). Nguồn được gắn sẵn trong bộ cài, không hiển thị link hay ô nhập repository. Updater lấy bản ổn định mới nhất từ GitHub Releases public; repository private chưa được hỗ trợ. Nếu bản cài chưa có nguồn cập nhật, ứng dụng thông báo rõ khi bấm kiểm tra.

Release cần có `GoogleNas.exe` và `GoogleNas.exe.sha256`. Updater kiểm tra nguồn URL, SHA256 và version trong EXE; chờ ổ an toàn, thay file atomic, giữ `.previous`, chạy health check và khởi động lại. Nếu health check thất bại, khôi phục bản cũ. Cập nhật ứng dụng giữ runtime rclone hiện có; để nâng runtime/driver, dùng bộ cài đầy đủ mới. SHA256 kiểm tra toàn vẹn, không thay thế chữ ký nhà phát hành. EXE của dự án hiện chưa ký Authenticode; MSI WinFsp và bộ cài .NET nguyên bản có chữ ký nhà cung cấp.

Workflow `.github/workflows/release.yml` build/test trên Windows. Push tag `vX.Y.Z` khớp `Core.Version` để xuất EXE ứng dụng và EXE setup cùng SHA256 lên Release. Thư mục này chưa có remote GitHub; cần URL repository trước khi đồng bộ mã nguồn.

## Build từ mã nguồn

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1 -Test -OfflineInstaller
```

Compiler .NET Framework có sẵn trong Windows; không cần NuGet hay .NET SDK. Lần build đầu cần mạng để chuẩn bị các gói được pin version và đối chiếu SHA256. Đây là tải ở máy phát triển, không phải tải khi người dùng cài. Dependency cache nằm trong `build\dependencies`. Đầu ra chính: `dist\GoogleNasSetup.exe`; ứng dụng và runtime để thử trực tiếp nằm ở `dist`.

Sửa version trong `src/Core.cs`, build/test, commit/push và push tag tương ứng. Không đưa token, cache hoặc rclone.conf lên GitHub; `.gitignore` đã loại dữ liệu này.

Kiểm thử bao gồm logic cấu hình/quoting/version/ngắt ổ, health check, update/rollback/checksum, giải nén toàn bộ payload offline, đối chiếu dependency và thay file/rollback bộ cài. Chế độ test chỉ ghi vào thư mục dự án; không cài driver/runtime thật hoặc thay registry startup. Giao diện được render để kiểm tra các trang. OAuth, đọc/ghi Drive thật, UAC và startup sau reboot cần kiểm tra với tài khoản/Windows thực tế.

## Dữ liệu và giới hạn

- `%LOCALAPPDATA%\GoogleNas` chứa token OAuth, cấu hình, cache và log; ACL giới hạn cho tài khoản Windows hiện tại và SYSTEM. Token theo định dạng rclone, chưa có lớp DPAPI bổ sung.
- VFS cache `full`: tải file khi truy cập và upload thay đổi sau khi lưu. Giới hạn cache là giới hạn mềm; file mở/chờ upload có thể làm vượt mức. Không xóa cache khi còn dữ liệu chưa upload.
- Ngắt ổ kiểm tra `inUse`, `uploadsQueued`, `uploadsInProgress`, `erroredFiles` hai lần cách nhau 6 giây. Tránh mở/ghi file trong lúc ngắt vì đây không phải khóa chống ghi đồng thời.
- Đây là ổ đĩa cloud trên máy Windows hiện tại; chưa có SMB chia sẻ LAN, snapshot NAS, khóa file đa máy hoặc tải toàn bộ Drive để dùng offline. Tốc độ và xung đột sửa cùng file phụ thuộc mạng/Google Drive. Google Docs/Sheets không tương đương file Office thông thường.
- Khi shutdown, upload có thể chưa hoàn tất; cache được giữ để tiếp tục khi gắn lại. Ứng dụng tự thử gắn lại sau 30 giây nếu rclone dừng. RC chỉ bind loopback, dùng mật khẩu ngẫu nhiên.
- Shared Drives/OAuth client riêng có thể cấu hình qua rclone CLI với `--config "%LOCALAPPDATA%\GoogleNas\rclone.conf"`; giao diện mặc định dùng My Drive.

## Gỡ cài đặt

Thoát an toàn trước, vào Windows Settings → Apps → CYAN NAS → Uninstall, hoặc Start Menu → Gỡ CYAN NAS. Cấu hình/cache và driver/runtime dùng chung được giữ lại. Nếu muốn gỡ WinFsp, thực hiện riêng trong Windows Settings sau khi bảo đảm ứng dụng khác không cần driver.

## Thành phần và nguồn chính thức

WinFsp - Windows File System Proxy, Copyright (C) Bill Zissimopoulos. Bộ cài MSI được phân phối nguyên bản theo GPLv3, kèm toàn bộ giấy phép và nguồn v2.1 trong `licenses`. rclone được phân phối theo MIT. .NET Framework có giấy phép Microsoft nằm trong redistributable chính thức.

- [WinFsp](https://github.com/winfsp/winfsp)
- [rclone mount](https://rclone.org/commands/rclone_mount/)
- [rclone Google Drive](https://rclone.org/drive/)
- [.NET Framework 4.8 offline](https://dotnet.microsoft.com/en-us/download/dotnet-framework/thank-you/net48-offline-installer)
- [GitHub Releases API](https://docs.github.com/en/rest/releases/releases)
