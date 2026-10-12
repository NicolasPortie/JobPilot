namespace JobPilot.Api.Services;

public static partial class ResumeExportRenderer
{
    private static string BuildCss(ResumeLayoutProfile profile) =>
        $$"""
        @page {
          size: Letter;
          margin: {{Css(profile.PageMarginIn)}}in;
        }

        * {
          box-sizing: border-box;
        }

        html,
        body {
          margin: 0;
          padding: 0;
          background: #eef2f7;
          color: #111827;
          font-family: {{profile.FontFamily}};
          font-size: {{Css(profile.FontSizePt)}}pt;
          line-height: {{Css(profile.LineHeight)}};
        }

        .resume-sheet {
          width: 8.5in;
          min-height: 11in;
          margin: 24px auto;
          padding: {{Css(profile.PageMarginIn)}}in;
          background: #ffffff;
          box-shadow: 0 18px 45px rgba(15, 23, 42, 0.14);
        }

        p {
          margin: 0 0 {{Css(profile.ParagraphGapPt)}}pt;
        }

        .name {
          margin-bottom: 2pt;
          color: #0f172a;
          font-size: {{Css(profile.NameSizePt)}}pt;
          line-height: 1.15;
          font-weight: 700;
        }

        .headline {
          margin-bottom: 4pt;
          color: #1f2937;
          font-size: {{Css(profile.HeadlineSizePt)}}pt;
          line-height: 1.2;
          font-weight: 600;
        }

        .contact {
          margin-bottom: 1.5pt;
          color: #1f2937;
          font-size: {{Css(profile.ContactSizePt)}}pt;
        }

        .contact-links {
          margin-bottom: {{Css(profile.SectionBottomPt)}}pt;
          color: #1f2937;
          font-size: {{Css(profile.ContactSizePt)}}pt;
        }

        .contact-label {
          font-weight: 700;
        }

        h2 {
          margin: {{Css(profile.SectionTopPt)}}pt 0 {{Css(profile.SectionBottomPt)}}pt;
          padding: 0;
          color: #0f172a;
          font-size: {{Css(profile.HeadingSizePt)}}pt;
          line-height: 1.2;
          font-weight: 700;
          text-transform: uppercase;
          break-after: avoid;
          page-break-after: avoid;
        }

        ul {
          margin: 0 0 {{Css(profile.ParagraphGapPt)}}pt 13pt;
          padding: 0;
        }

        li {
          margin: 0 0 2pt;
          padding-left: 1pt;
        }

        .entry-heading {
          margin: {{Css(Math.Max(profile.ParagraphGapPt, 2))}}pt 0 1.5pt;
          color: #0f172a;
          font-size: {{Css(profile.FontSizePt + 0.7m)}}pt;
          line-height: 1.2;
          font-weight: 700;
          break-after: avoid;
          page-break-after: avoid;
        }

        a {
          color: #111827;
          text-decoration: none;
        }

        .resume-space {
          height: {{Css(profile.SpacePt)}}pt;
        }

        @media print {
          html,
          body {
            width: auto;
            min-height: 0;
            background: #ffffff;
            overflow: visible;
          }

          .resume-sheet {
            width: auto;
            min-height: auto;
            margin: 0;
            padding: 0;
            box-shadow: none;
            overflow: visible;
          }
        }
        """;

    private sealed record ResumeLayoutProfile(
        string Key,
        string FontFamily,
        decimal FontSizePt,
        decimal ContactSizePt,
        decimal NameSizePt,
        decimal HeadlineSizePt,
        decimal HeadingSizePt,
        decimal LineHeight,
        decimal PageMarginIn,
        decimal ParagraphGapPt,
        decimal SectionTopPt,
        decimal SectionBottomPt,
        decimal SpacePt)
    {
        public static ResumeLayoutProfile For(string layout)
        {
            var normalized = ResumeTextSanitizer.NormalizeLayout(layout);
            return normalized switch
            {
                "human_review" => new("human_review", "Calibri, Arial, sans-serif", 11, 10, 16, 11, 11, 1.32m, 0.7m, 3.5m, 10, 4, 4),
                "ats_standard" => new("ats_standard", "Arial, Helvetica, sans-serif", 10.5m, 9.5m, 15, 10.5m, 10.5m, 1.26m, 0.65m, 3, 8, 3, 3),
                _ => new("ats_compact", "Arial, Helvetica, sans-serif", 10, 9, 14, 10, 10, 1.18m, 0.5m, 2, 6, 2, 2)
            };
        }
    }
}
