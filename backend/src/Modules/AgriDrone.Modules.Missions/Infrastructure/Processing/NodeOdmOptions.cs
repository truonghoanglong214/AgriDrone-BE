namespace AgriDrone.Modules.Missions.Infrastructure.Processing;

public sealed class NodeOdmOptions
{
    public const string SectionName = "NodeODM";

    public string Endpoint { get; set; } = "http://localhost:3000";

    /// <summary>
    /// Giảm chất lượng trích xuất đặc trưng để tiết kiệm RAM.
    /// Giá trị: lowest, low, medium, high, ultra. Khuyến nghị máy 8GB RAM: lowest.
    /// </summary>
    public string FeatureQuality { get; set; } = "lowest";

    /// <summary>
    /// Thu nhỏ ảnh đầu vào (Quan trọng nhất chống tràn RAM).
    /// Khuyến nghị: 1200 (hoặc 1000).
    /// </summary>
    public int ResizeTo { get; set; } = 1200;

    /// <summary>
    /// Giảm chất lượng đám mây điểm Point Cloud.
    /// Khuyến nghị: lowest.
    /// </summary>
    public string PcQuality { get; set; } = "lowest";

    /// <summary>
    /// Số luồng xử lý song song tối đa để kiểm soát tải CPU và RAM.
    /// Khuyến nghị: 4.
    /// </summary>
    public int MaxConcurrency { get; set; } = 4;

    /// <summary>
    /// Tạo orthophoto 2.5D trực tiếp, bỏ qua dựng lưới 3D mesh nặng nề.
    /// </summary>
    public bool FastOrthophoto { get; set; } = true;

    /// <summary>
    /// Thời gian chờ tối đa cho 1 tác vụ ODM (phút).
    /// </summary>
    public int TimeoutMinutes { get; set; } = 120;
}
