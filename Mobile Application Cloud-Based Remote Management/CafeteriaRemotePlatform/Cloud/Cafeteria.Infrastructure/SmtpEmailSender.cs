using System.Net;
using System.Net.Mail;
using Cafeteria.Application;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cafeteria.Infrastructure;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendInvitationAsync(
        string email,
        string inviteCode,
        string tenantName,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning(
                "SMTP is disabled. Invitation email to {Email} was not sent; " +
                "the administrator can share the generated code manually.",
                email);
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.Host) ||
            string.IsNullOrWhiteSpace(_options.FromAddress))
            throw new InvalidOperationException(
                "SMTP is enabled but Host or FromAddress is not configured.");

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_options.FromAddress, _options.FromName),
                Subject = $"Invitation to {tenantName} - Smart Cafeteria",
                Body =
                    $"You have been invited to {tenantName} on Smart Cafeteria.{Environment.NewLine}{Environment.NewLine}" +
                    $"Invitation code: {inviteCode}{Environment.NewLine}{Environment.NewLine}" +
                    "Open the Smart Cafeteria mobile app and use this code to join the company.",
                IsBodyHtml = false
            };
            message.To.Add(email);

            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.UseSsl,
                Credentials = string.IsNullOrWhiteSpace(_options.UserName)
                    ? CredentialCache.DefaultNetworkCredentials
                    : new NetworkCredential(_options.UserName, _options.Password)
            };

            await client.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("Invitation email sent to {Email}.", email);
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException)
        {
            _logger.LogError(ex, "Failed to send invitation email to {Email}.", email);
        }
    }
}
