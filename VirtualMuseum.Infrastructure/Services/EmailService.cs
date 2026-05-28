using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using VirtualMuseum.Application.Interfaces;

namespace VirtualMuseum.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var smtpSection = _configuration.GetSection("Smtp");
        var enabled = bool.TryParse(smtpSection["Enabled"], out var e) && e;
        if (!enabled)
        {
            // Development-friendly: allow running without SMTP by disabling sending.
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(
            smtpSection["SenderName"] ?? "Virtual Museum",
            smtpSection["SenderEmail"] ?? smtpSection["Username"] ?? "no-reply@virtualmuseum.local"));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        var server = smtpSection["SmtpServer"] ?? throw new InvalidOperationException("Smtp:SmtpServer is not configured");
        var port = int.TryParse(smtpSection["Port"], out var p) ? p : 587;
        var username = smtpSection["Username"];
        var password = smtpSection["Password"];

        // Defaults:
        // - Port 1025 is commonly used for local/dev SMTP servers (Mailpit/MailHog) with no TLS.
        // - For typical providers on 587, use StartTLS.
        var socketOptions = port == 1025 ? SecureSocketOptions.None : SecureSocketOptions.StartTls;

        await client.ConnectAsync(server, port, socketOptions, cancellationToken);
        if (!string.IsNullOrWhiteSpace(username))
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("Smtp:Password must be configured when Smtp:Username is set");

            await client.AuthenticateAsync(username, password, cancellationToken);
        }
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}

