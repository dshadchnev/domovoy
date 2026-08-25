using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Logging;

namespace Domovoy.Notification.Service.Infrastructure.External;

public class EmailSender : INotificationSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailSender> _logger;

    public string ChannelType => "Email";

    public EmailSender(IConfiguration configuration, ILogger<EmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(string email, string subject, string message)
    {
        await SendCustomAsync(null, 587, null, null, null, email, subject, message);
    }

    public async Task SendCustomAsync(string? host, int? port, string? user, string? pass, string? fromEmail, string toEmail, string subject, string message)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
            throw new InvalidOperationException("Recipient email is not configured");

        var smtpHost = !string.IsNullOrWhiteSpace(host) ? host : (_configuration["Smtp:Host"] ?? "smtp.gmail.com");
        var smtpPort = port.HasValue && port.Value > 0 ? port.Value : int.Parse(_configuration["Smtp:Port"] ?? "587");
        var smtpUser = !string.IsNullOrWhiteSpace(user) ? user : (_configuration["Smtp:User"] ?? "");
        var smtpPass = !string.IsNullOrWhiteSpace(pass) ? pass : (_configuration["Smtp:Pass"] ?? "");
        var fromAddr = !string.IsNullOrWhiteSpace(fromEmail) ? fromEmail : "noreply@domovoy.local";

        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress("Система управления умным домом «Домовой»", fromAddr));
        mimeMessage.To.Add(MailboxAddress.Parse(toEmail));
        mimeMessage.Subject = $"[Domovoy] {subject}";
        mimeMessage.Body = new TextPart("html")
        {
            Text = $"<div style='font-family:sans-serif;padding:1rem;'><h2 style='color:#06b6d4;'>🏠 Domovoy Notification</h2><h3>{subject}</h3><p style='font-size:1.1rem;'>{message.Replace("\n", "<br/>")}</p></div>"
        };

        using var client = new SmtpClient();
        await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.Auto);
        if (!string.IsNullOrEmpty(smtpUser) && !string.IsNullOrEmpty(smtpPass))
        {
            await client.AuthenticateAsync(smtpUser, smtpPass);
        }
        await client.SendAsync(mimeMessage);
        await client.DisconnectAsync(true);

        _logger.LogInformation("✅ Email sent to {Email} via SMTP {Host}:{Port}", toEmail, smtpHost, smtpPort);
    }
}