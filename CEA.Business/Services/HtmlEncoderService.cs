using System.Net;
using System.Text.RegularExpressions;

namespace CEA.Business.Services
{
    public interface IHtmlEncoderService
    {
        string Encode(string? input);
        string SanitizeForEmail(string? input);
    }

    public class HtmlEncoderService : IHtmlEncoderService
    {
        public string Encode(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return WebUtility.HtmlEncode(input);
        }

        public string SanitizeForEmail(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // HTML taglerini temizle
            var sanitized = Regex.Replace(input, "<.*?>", string.Empty);

            // SQL injection riskli karakterleri temizle (basit seviye)
            sanitized = sanitized.Replace("'", "&#39;")
                                .Replace("\"", "&quot;")
                                .Replace("<", "&lt;")
                                .Replace(">", "&gt;");

            return sanitized;
        }
    }
}