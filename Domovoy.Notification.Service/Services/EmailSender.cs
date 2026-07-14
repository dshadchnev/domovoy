using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Logging;

namespace Domovoy.Notification.Service.Services;

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
        var smtpHost = _configuration["Smtp:Host"] ?? throw new InvalidOperationException("Smtp:Host not configured");
        var smtpPort = int.Parse(_configuration["Smtp:Port"] ?? "587");
        var smtpUser = _configuration["Smtp:User"] ?? "";
        var smtpPass = _configuration["Smtp:Pass"] ?? "";
        var fromEmail = _configuration["Smtp:FromEmail"] ?? "noreply@domovoy.local";

        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(MailboxAddress.Parse(fromEmail));
        mimeMessage.To.Add(MailboxAddress.Parse(email));
        mimeMessage.Subject = $"[Domovoy] {subject}";
        mimeMessage.Body = new TextPart("html")
        {
            Text = $"<h3>{subject}</h3><p>{message}</p>"
        };

        using var client = new SmtpClient();
        await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(smtpUser, smtpPass);
        await client.SendAsync(mimeMessage);
        await client.DisconnectAsync(true);

        _logger.LogInformation("✅ Email sent to {Email}", email);
    }
}