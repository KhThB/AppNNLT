using System;

namespace Mobile.Models;

public class QrHistoryModel
{
    public string PoiId { get; set; } = string.Empty;
    public string PoiName { get; set; } = string.Empty;
    public DateTime ScanTime { get; set; }

    public string FormattedScanTime => ScanTime.ToString("dd/MM/yyyy HH:mm");
}
