using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domovoy.Domain.Events;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Domovoy.Notification.Service.Infrastructure.External.Adapters;

/// <summary>
/// ACL адаптер для взаимодействия с внешним SMTP почтовым сервером (MailKit/MimeKit).
/// Переводит доменное намерение NotificationRequested в протокольное представление MimeMessage.
/// </summary>
public class EmailNotificationAdapter : INotificationAdapter
{
    private readonly IConfiguration? _configuration;
    private readonly INotificationSender? _notificationSender;
    private readonly ILogger<EmailNotificationAdapter> _logger;

    public string ChannelType => "Email";

    public EmailNotificationAdapter(
        ILogger<EmailNotificationAdapter> logger,
        IConfiguration? configuration = null,
        IEnumerable<INotificationSender>? senders = null)
    {
        _logger = logger;
        _configuration = configuration;
        _notificationSender = senders?.FirstOrDefault(s => s.ChannelType == "Email");
    }

    public async Task SendNotificationAsync(NotificationRequested notification, CancellationToken cancellationToken = default)
    {
        if (notification == null) throw new ArgumentNullException(nameof(notification));

        if (_notificationSender != null)
        {
            await _notificationSender.SendAsync(notification.RecipientAddress, notification.Title, notification.Message);
            _logger.LogInformation("✅ [ACL EmailAdapter] Sent email notification to {Email}", notification.RecipientAddress);
            return;
        }

        if (_configuration != null)
        {
            var smtpHost = _configuration["Smtp:Host"] ?? throw new InvalidOperationException("Smtp:Host not configured");
            var smtpPort = int.Parse(_configuration["Smtp:Port"] ?? "587");
            var smtpUser = _configuration["Smtp:User"] ?? "";
            var smtpPass = _configuration["Smtp:Pass"] ?? "";
            var fromEmail = _configuration["Smtp:FromEmail"] ?? "noreply@domovoy.local";

            var mimeMessage = new MimeMessage();
            mimeMessage.From.Add(MailboxAddress.Parse(fromEmail));
            mimeMessage.To.Add(MailboxAddress.Parse(notification.RecipientAddress));
            mimeMessage.Subject = $"[Domovoy] {notification.Title}";
            mimeMessage.Body = new TextPart("html")
            {
                Text = $"<h3>{notification.Title}</h3><p>{notification.Message}</p>"
            };

            using var client = new SmtpClient();
            await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls, cancellationToken);
            if (!string.IsNullOrEmpty(smtpUser))
            {
                await client.AuthenticateAsync(smtpUser, smtpPass, cancellationToken);
            }
            await client.SendAsync(mimeMessage, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("✅ [ACL EmailAdapter] Sent email notification via SMTP to {Email}", notification.RecipientAddress);
        }
        else
        {
            _logger.LogWarning("⚠️ [ACL EmailAdapter] Neither IConfiguration nor INotificationSender registered for Email");
        }
    }
}
