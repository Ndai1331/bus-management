namespace HCS.Blazor.Client.Pages;

using System.Linq;

internal sealed record FeatureDefinition(
    string Title,
    string Description,
    string Endpoint,
    string Icon,
    bool CanLoad = true);

internal static class FeatureCatalog
{
    public static FeatureDefinition Resolve(string relativeUrl)
    {
        var path = relativeUrl.Split('?', '#')[0].Trim('/').ToLowerInvariant();
        return path switch
        {
            "workspace" => F("Không gian làm việc", "Tổng quan công việc, văn bản và hoạt động cần xử lý.", "/api/dashboard", "fa fa-gauge-high"),
            "manage-documents" or "my-documents" or "document-assignments" or "document-files" or "document-histories" => F("Quản lý văn bản", "Tra cứu, theo dõi luân chuyển, tệp và lịch sử văn bản.", "/api/documents", "fa fa-folder-open"),
            "document-signing" => F("Ký duyệt", "Hàng đợi ký điện tử và ký số.", "/api/signing", "fa fa-file-signature"),
            "workflow-definitions" => F("Loại quy trình", "Danh mục loại quy trình.", "/api/workflows/definitions", "fa fa-diagram-project"),
            "workflow-lists" or "workflow-detail" => F("Quy trình", "Thiết kế các bước xử lý và tệp mẫu.", "/api/workflows/definitions", "fa fa-code-branch"),
            "document-workflow-instances" or "workflow-instances" => F("Hồ sơ quy trình", "Theo dõi trạng thái và nhiệm vụ phê duyệt.", "/api/workflows/instances", "fa fa-folder-open"),
            "projects" or "project-detail" => F("Dự án", "Quản lý tiến độ, thành viên và phạm vi dự án.", "/api/projects", "fa fa-diagram-project"),
            "tasks" or "project-task-detail" => F("Công việc", "Theo dõi phân công, ưu tiên và tiến độ thực hiện.", "/api/project-tasks", "fa fa-list-check"),
            "calendar-events" or "calendar-event-detail" => F("Lịch công tác", "Lịch cá nhân, đơn vị và các mốc liên quan công việc.", "/api/calendar", "fa fa-calendar-days"),
            "survey-results" or "survey-sessions" or "survey-collections" => F("Khảo sát", "Quản lý đợt khảo sát và tổng hợp kết quả.", "/api/surveys/sessions", "fa fa-square-poll-vertical"),
            "bus-management" => F("Quản lý bến xe", "Tổng hợp vận hành, doanh thu, chi phí, mặt bằng và đối soát theo phạm vi bến được gán.", "/api/bus-management/dashboard", "fa fa-bus"),
            "survey-locations" => F("Địa điểm khảo sát", "Danh mục địa điểm thu thập khảo sát.", "/api/surveys/locations", "fa fa-location-dot"),
            "survey-criterias" => F("Tiêu chí khảo sát", "Danh mục tiêu chí chấm điểm khảo sát.", "/api/surveys/criteria", "fa fa-list-ol"),
            "departments" => F("Phòng ban", "Cơ cấu phòng ban và quan hệ cấp trên.", "/api/organization/departments", "fa fa-sitemap"),
            "unit-lists" => F("Đơn vị", "Danh mục cơ quan, đơn vị phát hành và tiếp nhận.", "/api/organization/units", "fa fa-building"),
            "positions" => F("Chức vụ", "Danh mục chức danh dùng trong phân công và quy trình.", "/api/organization/positions", "fa fa-id-badge"),
            "master-datas" => F("Danh mục dùng chung", "Quản trị dữ liệu tham chiếu tập trung.", "/api/organization/master-data", "fa fa-layer-group"),
            "document-types" => Master("Loại văn bản", "DocumentType"),
            "sectors" => Master("Lĩnh vực", "Sector"),
            "urgency-levels" => Master("Độ khẩn", "UrgencyLevel"),
            "confidentiality-levels" => Master("Độ mật", "ConfidentialityLevel"),
            "processing-methods" => Master("Phương thức xử lý", "ProcessingMethod"),
            "document-status" => Master("Trạng thái văn bản", "DocumentStatus"),
            "signing-methods" => Master("Phương thức ký", "SigningMethod"),
            "even-types" or "event-types" => Master("Loại sự kiện", "EventType"),
            "signature-settings" => F("Cấu hình chữ ký", "Thông tin xác thực được che và gửi an toàn đến dịch vụ ký.", "/api/signing/credentials/current", "fa fa-key"),
            "user-signatures" => F("Chữ ký cá nhân", "Quản lý mẫu chữ ký và quyền sử dụng.", "/api/signing/signatures", "fa fa-signature"),
            "signing-kpi-report" => F("Báo cáo ký số", "Theo dõi kết quả ký và lỗi xác minh.", "/api/reports?dimension=signing", "fa fa-chart-pie"),
            "reports" or "report-web-frame" => F("Báo cáo", "Các mô hình đọc tổng hợp từ dữ liệu nghiệp vụ.", "/api/reports", "fa fa-chart-column"),
            "notification-receivers" => F("Thông báo", "Thông báo nghiệp vụ và trạng thái đã đọc.", "/api/notifications", "fa fa-bell", false),
            "chat" or "chat1" => F("Trao đổi", "Trao đổi trực tiếp, nhóm, dự án và công việc theo thời gian thực.", "/api/chat", "fa fa-comments", false),
            _ when path.StartsWith("workflow-detail/") =>
                F("Chi tiết quy trình", "Cấu hình bước xử lý, tệp mẫu và người thực hiện.", "/api/workflows/definitions", "fa fa-diagram-project"),
            _ when path.StartsWith("document-workflow-instances/") =>
                F("Hồ sơ quy trình", "Theo dõi trạng thái và nhiệm vụ phê duyệt.", "/api/workflows/instances", "fa fa-folder-open"),
            _ when path.StartsWith("project-detail/") =>
                F("Chi tiết dự án", "Thành viên, công việc và phạm vi dự án.", "/api/projects", "fa fa-diagram-project"),
            _ when path.StartsWith("survey-collections/") =>
                F("Thu thập khảo sát", "Ghi nhận kết quả theo tiêu chí cho địa điểm đang chọn.", "/api/surveys/sessions", "fa fa-square-poll-vertical"),
            _ when path.StartsWith("document-detail/") || path.StartsWith("view-document-detail/") =>
                F("Chi tiết văn bản", "Thông tin, tệp, phân công và lịch sử xử lý.", $"/api/documents/{path.Split('/').Last()}", "fa fa-file-lines"),
            _ when path.StartsWith("chat/") || path.StartsWith("chat1/") =>
                F("Trao đổi", "Mở cuộc trao đổi được liên kết từ thông báo.", "/api/chat", "fa fa-comments", false),
            _ => F("Chức năng HCS", "Chức năng nghiệp vụ được truy cập qua Web Gateway.", "/api/dashboard", "fa fa-layer-group")
        };
    }

    private static FeatureDefinition Master(string title, string type) =>
        F(title, $"Quản trị danh mục {title.ToLowerInvariant()} dùng chung.", $"/api/organization/master-data?type={type}", "fa fa-tags");

    private static FeatureDefinition F(string title, string description, string endpoint, string icon, bool canLoad = true) =>
        new(title, description, endpoint, icon, canLoad);
}
