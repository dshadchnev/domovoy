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
        var smtpHost = _configuration["Smtp:Host"] ?? "smtp.mail.ru";
        var smtpPort = int.TryParse(_configuration["Smtp:Port"], out var p) ? p : 465;
        var smtpUser = _configuration["Smtp:User"] ?? "";
        var smtpPass = _configuration["Smtp:Pass"] ?? "";
        
        var fromEmail = _configuration["Smtp:FromEmail"];
        if (string.IsNullOrWhiteSpace(fromEmail) || fromEmail.EndsWith(".local") || !fromEmail.Contains("@"))
        {
            fromEmail = !string.IsNullOrWhiteSpace(smtpUser) && smtpUser.Contains("@") ? smtpUser : email;
        }

        var html = $"<h3>{subject}</h3><p>{message.Replace("\n", "<br/>")}</p>";

        await SendEmailInternalAsync(smtpHost, smtpPort, smtpUser, smtpPass, fromEmail, email, subject, html);
        _logger.LogInformation("[Success] Email sent to {Email}", email);
    }

    public static async Task SendEmailInternalAsync(
        string smtpHost,
        int smtpPort,
        string smtpUser,
        string smtpPass,
        string fromEmail,
        string toEmail,
        string subject,
        string htmlBody)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress("Домовой Smart Home", fromEmail));
        mimeMessage.To.Add(MailboxAddress.Parse(toEmail));
        mimeMessage.Subject = $"[Domovoy] {subject}";
        mimeMessage.Body = new TextPart("html")
        {
            Text = htmlBody
        };

        using var client = new SmtpClient();
        
        // Port 465 = SslOnConnect, Port 587 = StartTls, otherwise Auto
        var secureOptions = smtpPort == 465
            ? SecureSocketOptions.SslOnConnect
            : (smtpPort == 587 ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);

        client.ServerCertificateValidationCallback = (s, c, h, e) => true;

        await client.ConnectAsync(smtpHost, smtpPort, secureOptions);
        
        if (!string.IsNullOrEmpty(smtpUser) && !string.IsNullOrEmpty(smtpPass))
        {
            await client.AuthenticateAsync(smtpUser, smtpPass);
        }

        await client.SendAsync(mimeMessage);
        await client.DisconnectAsync(true);
    }
}