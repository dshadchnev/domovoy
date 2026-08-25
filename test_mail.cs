using System;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

class Program
{
    static async Task Main()
    {
        var host = "smtp.mail.ru";
        var port = 465;
        var user = "p_romantsov@mail.ru";
        var pass = "Ikc7P86YtkmL8ca30zgj";
        var from = "p_romantsov@mail.ru";
        var to = "p_romantsov@mail.ru";

        Console.WriteLine($"Connecting to {host}:{port}...");
        using var client = new SmtpClient();
        
        // Port 465 uses SSL on connect, Port 587 uses STARTTLS
        var options = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        
        try
        {
            await client.ConnectAsync(host, port, options);
            Console.WriteLine("Connected! Authenticating...");
            await client.AuthenticateAsync(user, pass);
            Console.WriteLine("Authenticated! Sending test email...");

            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress("Домовой Smart Home", from));
            msg.To.Add(MailboxAddress.Parse(to));
            msg.Subject = "[Domovoy] Тестовое уведомление системы умного дома";
            msg.Body = new TextPart("html")
            {
                Text = "<h3>🏠 Система «Домовой»</h3><p>Уведомление успешно доставлено через SMTP Mail.ru!</p><p>Текущее время: <b>" + DateTime.UtcNow.ToString("O") + "</b></p>"
            };

            await client.SendAsync(msg);
            await client.DisconnectAsync(true);
            Console.WriteLine("SUCCESS: Email delivered!");
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: " + ex.ToString());
        }
    }
}