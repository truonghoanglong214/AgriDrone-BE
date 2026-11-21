using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using AgriDrone.Modules.Surveys.Application.Features.Reports;
namespace AgriDrone.Api.Reporting;

internal static class RevenueWorkbook
{
    public static byte[] Create(MonthlyFinancialReport report)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            Add(zip, "[Content_Types].xml", """<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>""");
            Add(zip, "_rels/.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Add(zip, "xl/workbook.xml", """<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Revenue" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            Add(zip, "xl/_rels/workbook.xml.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>""");
            using var output = zip.CreateEntry("xl/worksheets/sheet1.xml").Open();
            using var xml = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false) });
            xml.WriteStartElement("worksheet", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
            xml.WriteStartElement("sheetData");
            Row(xml, "Period", $"{report.Year:D4}-{report.Month:D2}", "Service", report.Service ?? "ALL", "Timezone", "UTC+7", "Currency", "VND");
            Row(xml, "Fingerprint", report.ReportFingerprint, "PendingPayments", report.PendingPayments);
            Row(xml, "Formula", report.FormulaVersion, "GeneratedAt", report.GeneratedAt.ToString("O", CultureInfo.InvariantCulture));
            Row(xml, "HistoryAvailableFrom", report.HistoryAvailableFrom.ToString("O", CultureInfo.InvariantCulture), "HistoricalBalancesComplete", report.HistoricalBalancesComplete.ToString());
            Row(xml, "PublishedServiceValue", report.PublishedServiceValue, "ServiceRefunds", report.ServiceRefunds, "NetServiceRevenue", report.NetServiceRevenue);
            Row(xml, "CashReceipts", report.CashReceipts, "CashRefunds", report.CashRefunds, "NetCash", report.NetCash);
            Row(xml, "DepositsHeld", report.DepositsHeld, "RefundableExcess", report.RefundableExcess, "RefundPayable", report.RefundPayable);
            Row(xml, "Retention", report.CancellationRetention, "Outstanding", report.OutstandingBalances, "PublishedOrders", report.PublishedOrders);
            Row(xml, "UnpricedOrders", report.UnpricedOrders, "ReconciliationOrders", report.ReconciliationOrders);
            Row(xml, "OrderId", "Order", "Service", "ServiceValue", "ServiceRefunds", "CashReceipts", "CashRefunds", "DepositsHeld", "RefundableExcess", "RefundPayable", "Retention", "Outstanding", "Published", "Unpriced", "Reconciliation");
            foreach (var line in report.Lines)
                Row(xml, line.OrderId.ToString(), line.OrderNumber, line.Service, line.PublishedServiceValue, line.ServiceRefunds, line.CashReceipts, line.CashRefunds, line.DepositsHeld, line.RefundableExcess, line.RefundPayable, line.CancellationRetention, line.OutstandingBalance, line.PublishedInPeriod.ToString(), line.Unpriced.ToString(), line.NeedsReconciliation.ToString());
            xml.WriteEndElement(); xml.WriteEndElement();
        }
        return stream.ToArray();
    }
    private static void Row(XmlWriter xml, params object[] values)
    {
        xml.WriteStartElement("row");
        foreach (var value in values)
        {
            xml.WriteStartElement("c");
            if (value is decimal or int) xml.WriteElementString("v", Convert.ToString(value, CultureInfo.InvariantCulture));
            else
            {
                // Inline strings cannot execute formulas, even if the first character is =, +, - or @.
                xml.WriteAttributeString("t", "inlineStr"); xml.WriteStartElement("is");
                var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                xml.WriteElementString("t", new string(text.Where(XmlConvert.IsXmlChar).ToArray()));
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
        }
        xml.WriteEndElement();
    }
    private static void Add(ZipArchive zip, string path, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
