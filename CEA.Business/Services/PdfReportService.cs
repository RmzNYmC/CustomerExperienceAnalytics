using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using CEA.Core.ViewModels;
using CEA.Core.Entities;

namespace CEA.Business.Services
{
    public interface IPdfReportService
    {
        byte[] GenerateAnalyticsReport(PeriodAnalysisResult analysis, string surveyTitle);
        byte[] GenerateComplaintReport(Complaint complaint);
        byte[] GenerateSurveyResultsReport(Survey survey, List<PeriodAnalysisResult> analyses);
    }

    public class PdfReportService : IPdfReportService
    {
        public byte[] GenerateAnalyticsReport(PeriodAnalysisResult analysis, string surveyTitle)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    // Header
                    page.Header().Element(ComposeHeader);

                    // Content
                    page.Content().Element(container => ComposeContent(container, analysis, surveyTitle));

                    // Footer
                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Sayfa ");
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            });

            return document.GeneratePdf();

            void ComposeHeader(IContainer container)
            {
                container.Row(row =>
                {
                    row.RelativeItem().Column(column =>
                    {
                        column.Item().Text("MÜŞTERİ DENEYİMİ ANALİZ RAPORU")
                            .FontSize(20).Bold().FontColor(Colors.Blue.Darken2);
                        column.Item().Text($"Oluşturulma: {DateTime.Now:dd.MM.yyyy HH:mm}")
                            .FontSize(10).FontColor(Colors.Grey.Medium);
                    });
                });
            }

            void ComposeContent(IContainer container, PeriodAnalysisResult data, string title)
            {
                container.PaddingVertical(1, Unit.Centimetre).Column(column =>
                {
                    // Anket Bilgisi
                    column.Item().BorderBottom(1).PaddingBottom(10).Column(col =>
                    {
                        col.Item().Text(title).FontSize(16).Bold();
                        col.Item().Text($"Dönem: {data.PeriodLabel}").FontSize(12);
                    });

                    // Özet Kartlar
                    column.Item().PaddingTop(20).Row(row =>
                    {
                        row.RelativeItem().Border(1).Padding(10).Background(Colors.Grey.Lighten4)
                            .Column(c => {
                                c.Item().Text("Toplam Yanıt").FontSize(10).FontColor(Colors.Grey.Medium);
                                c.Item().Text(data.TotalResponses.ToString()).FontSize(24).Bold().FontColor(Colors.Blue.Medium);
                            });

                        row.RelativeItem().Border(1).Padding(10).Background(Colors.Grey.Lighten4)
                            .Column(c => {
                                c.Item().Text("Ortalama Memnuniyet").FontSize(10).FontColor(Colors.Grey.Medium);
                                c.Item().Text($"{data.AverageSatisfaction:F1}/5").FontSize(24).Bold()
                                    .FontColor(data.AverageSatisfaction >= 4 ? Colors.Green.Darken1 : Colors.Orange.Darken1);
                            });

                        row.RelativeItem().Border(1).Padding(10).Background(Colors.Grey.Lighten4)
                            .Column(c => {
                                c.Item().Text("NPS Skoru").FontSize(10).FontColor(Colors.Grey.Medium);
                                c.Item().Text(data.NpsScore.ToString("F0")).FontSize(24).Bold()
                                    .FontColor(data.NpsScore >= 50 ? Colors.Green.Darken1 : data.NpsScore >= 0 ? Colors.Orange.Darken1 : Colors.Red.Darken1);
                            });

                        row.RelativeItem().Border(1).Padding(10).Background(Colors.Grey.Lighten4)
                            .Column(c => {
                                c.Item().Text("Şikayet Sayısı").FontSize(10).FontColor(Colors.Grey.Medium);
                                c.Item().Text(data.ComplaintCount.ToString()).FontSize(24).Bold()
                                    .FontColor(data.ComplaintCount == 0 ? Colors.Green.Darken1 : Colors.Red.Darken1);
                            });
                    });

                    // Soru Analizleri
                    column.Item().PaddingTop(30).Text("DETAYLI SORU ANALİZLERİ").FontSize(14).Bold();

                    foreach (var question in data.QuestionBreakdown)
                    {
                        column.Item().PaddingTop(15).Border(1).Padding(10).Column(qCol =>
                        {
                            qCol.Item().Text(question.QuestionText).FontSize(12).Bold();
                            qCol.Item().PaddingTop(5).Text($"Tip: {question.Type} | Ortalama: {question.AverageScore:F2} | Yanıt: {question.ResponseCount}")
                                .FontSize(10).FontColor(Colors.Grey.Medium);

                            // Dağılım tablosu
                            if (question.Distribution.Any())
                            {
                                qCol.Item().PaddingTop(8).Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(3);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Text("Seçenek").Bold();
                                        header.Cell().AlignRight().Text("Sayı").Bold();
                                        header.Cell().AlignRight().Text("Yüzde").Bold();
                                    });

                                    foreach (var dist in question.Distribution)
                                    {
                                        table.Cell().Text(dist.Label);
                                        table.Cell().AlignRight().Text(dist.Count.ToString());
                                        table.Cell().AlignRight().Text($"%{dist.Percentage:F1}");
                                    }
                                });
                            }
                        });
                    }

                    // Notlar
                    column.Item().PaddingTop(30).Background(Colors.Grey.Lighten4).Padding(10)
                        .Text("Bu rapor Müşteri Deneyimi Analiz Sistemi tarafından otomatik olarak oluşturulmuştur.")
                        .FontSize(9).FontColor(Colors.Grey.Medium).Italic();
                });
            }
        }

        public byte[] GenerateComplaintReport(Complaint complaint)
        {
            // Benzer yapıda şikayet raporu...
            throw new NotImplementedException();
        }

        public byte[] GenerateSurveyResultsReport(Survey survey, List<PeriodAnalysisResult> analyses)
        {
            // Tüm dönemleri kapsayan kapsamlı rapor...
            throw new NotImplementedException();
        }
    }
}