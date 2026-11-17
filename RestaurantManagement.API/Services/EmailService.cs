using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using RestaurantManagement.API.Configuration;

namespace RestaurantManagement.API.Services;

public sealed class EmailService(IOptions<AppSettings> options)
{
    private readonly EmailSettings _settings = options.Value.Email;

    public async Task SendAsync(string to, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(_settings.SmtpHost) || string.IsNullOrWhiteSpace(_settings.FromEmail))
            throw new InvalidOperationException("SMTP is not configured. Set App:Email settings or environment variables.");
        using var message = new MailMessage
        {
            From = new MailAddress(_settings.FromEmail, _settings.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(to);
        using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
        {
            EnableSsl = _settings.EnableSsl,
            Credentials = string.IsNullOrWhiteSpace(_settings.UserName) ? CredentialCache.DefaultNetworkCredentials : new NetworkCredential(_settings.UserName, _settings.Password)
        };
        await client.SendMailAsync(message);
    }
}