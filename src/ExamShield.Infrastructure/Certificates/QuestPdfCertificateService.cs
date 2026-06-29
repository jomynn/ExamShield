using ExamShield.Domain.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ExamShield.Infrastructure.Certificates;

public sealed class QuestPdfCertificateService : IStudentCertificateService
{
    static QuestPdfCertificateService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Generate(CertificateData data)
    {
        var pass = data.Percentage >= 50.0;
        var grade = data.Percentage switch
        {
            >= 90 => "A",
            >= 80 => "B",
            >= 70 => "C",
            >= 60 => "D",
            _     => "F"
        };

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(40);
                page.Background().Background(Color.FromHex("#0D1117"));

                page.Content().Column(col =>
                {
                    col.Spacing(16);

                    // Header band
                    col.Item().Background(Color.FromHex("#161B22"))
                        .Padding(20)
                        .Row(row =>
                        {
                            row.RelativeItem().Column(inner =>
                            {
                                inner.Item()
                                    .Text("EXAMSHIELD")
                                    .FontSize(11).FontColor(Color.FromHex("#00BFFF"))
                                    .Bold().LetterSpacing(0.15f);
                                inner.Item()
                                    .Text("SECURE EXAMINATION SYSTEM")
                                    .FontSize(8).FontColor(Color.FromHex("#8B949E"))
                                    .LetterSpacing(0.1f);
                            });
                            row.AutoItem()
                                .AlignRight()
                                .AlignMiddle()
                                .Text(pass ? "✓ VERIFIED" : "✗ NOT PASSED")
                                .FontSize(10)
                                .FontColor(pass ? Color.FromHex("#3FB950") : Color.FromHex("#F85149"))
                                .Bold();
                        });

                    // Title
                    col.Item().PaddingTop(8).AlignCenter()
                        .Text("CERTIFICATE OF EXAMINATION RESULT")
                        .FontSize(22).FontColor(Colors.White).Bold()
                        .LetterSpacing(0.05f);

                    col.Item().AlignCenter()
                        .Text("This document certifies the following examination result.")
                        .FontSize(10).FontColor(Color.FromHex("#8B949E"));

                    // Score card
                    col.Item().PaddingHorizontal(40)
                        .Background(Color.FromHex("#161B22"))
                        .Border(1).BorderColor(Color.FromHex("#30363D"))
                        .Padding(24)
                        .Row(row =>
                        {
                            row.RelativeItem(3).Column(left =>
                            {
                                left.Spacing(10);
                                _Field(left, "Examination", data.ExamName);
                                _Field(left, "Student ID", data.StudentId);
                                _Field(left, "Capture ID", data.CaptureId);
                                _Field(left, "Issued On", data.ScoredAt.ToString("dd MMMM yyyy"));
                            });

                            row.ConstantItem(1).Background(Color.FromHex("#30363D"));

                            row.RelativeItem(2).Column(right =>
                            {
                                right.Spacing(4);
                                right.Item().AlignCenter()
                                    .Text($"{data.Percentage:F1}%")
                                    .FontSize(48).FontColor(Color.FromHex("#00BFFF")).Bold();
                                right.Item().AlignCenter()
                                    .Text($"{data.CorrectAnswers} / {data.TotalQuestions} correct")
                                    .FontSize(12).FontColor(Color.FromHex("#8B949E"));
                                right.Item().PaddingTop(8).AlignCenter()
                                    .Text($"Grade: {grade}")
                                    .FontSize(20).FontColor(Colors.White).Bold();
                            });
                        });

                    // Verification footer
                    col.Item().PaddingHorizontal(40).Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item()
                                .Text("INTEGRITY VERIFICATION")
                                .FontSize(8).FontColor(Color.FromHex("#00BFFF"))
                                .Bold().LetterSpacing(0.1f);
                            left.Item()
                                .Text($"Capture: {data.CaptureId}")
                                .FontSize(7).FontColor(Color.FromHex("#8B949E"));
                            left.Item()
                                .Text("Verify this certificate at: /public/verify")
                                .FontSize(7).FontColor(Color.FromHex("#8B949E"));
                        });
                        row.AutoItem().AlignRight().AlignBottom()
                            .Text($"Generated {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC")
                            .FontSize(7).FontColor(Color.FromHex("#484F58"));
                    });
                });
            });
        });

        return doc.GeneratePdf();
    }

    private static void _Field(ColumnDescriptor col, string label, string value)
    {
        col.Item().Row(row =>
        {
            row.ConstantItem(90)
                .Text(label.ToUpperInvariant())
                .FontSize(7).FontColor(Color.FromHex("#8B949E"))
                .Bold().LetterSpacing(0.08f);
            row.RelativeItem()
                .Text(value)
                .FontSize(11).FontColor(Colors.White);
        });
    }
}
