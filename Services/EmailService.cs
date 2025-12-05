using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace StarApi.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            return Regex.IsMatch(email,
                @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
                RegexOptions.IgnoreCase);
        }

        public async Task SendVerificationEmailAsync(string toEmail, string verificationToken)
        {
            try
            {
                if (!_config.GetValue<bool>("EmailSettings:Enabled", true))
                {
                    _logger.LogInformation("Email sending disabled (EmailSettings:Enabled = false). Skipping email send.");
                    return;
                }

                if (!IsValidEmail(toEmail))
                {
                    _logger.LogWarning("Invalid recipient email: {Email}", toEmail);
                    return;
                }

                var fromEmail = _config["EmailSettings:FromEmail"]
                                ?? throw new InvalidOperationException("Missing FromEmail config.");
                if (!IsValidEmail(fromEmail))
                {
                    _logger.LogWarning("Invalid sender email: {Email}", fromEmail);
                    return;
                }

                var smtpHost = _config["EmailSettings:SmtpHost"]
                               ?? throw new InvalidOperationException("Missing SmtpHost config.");
                var smtpPort = int.Parse(_config["EmailSettings:SmtpPort"] ?? "587");
                var smtpUser = _config["EmailSettings:SmtpUser"]
                               ?? throw new InvalidOperationException("Missing SmtpUser config.");
                var smtpPass = _config["EmailSettings:SmtpPass"]
                               ?? throw new InvalidOperationException("Missing SmtpPass config.");
                var frontendUrl = _config["AppSettings:FrontendUrl"]
                                  ?? throw new InvalidOperationException("Missing FrontendUrl config.");

                var verificationLink = $"{frontendUrl}/verify-email?token={verificationToken}";
                var subject = "Verify your StarAPI account";
                var body = $@"
                    <h3>Welcome to StarAPI!</h3>
                    <p>Click the link below to verify your email address:</p>
                    <a href='{verificationLink}'>{verificationLink}</a>
                    <p>This link will expire in 24 hours.</p>";

                using var message = new MailMessage
                {
                    From = new MailAddress(fromEmail, "StarAPI Support"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                message.To.Add(toEmail);

                using var smtpClient = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    EnableSsl = true
                };

                await smtpClient.SendMailAsync(message);
                _logger.LogInformation("Verification email sent to {Email}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send verification email to {Email}", toEmail);
            }
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string resetToken)
        {
            try
            {
                if (!_config.GetValue<bool>("EmailSettings:Enabled", true))
                {
                    _logger.LogInformation("Email sending disabled (EmailSettings:Enabled = false). Skipping password reset email send.");
                    return;
                }

                if (!IsValidEmail(toEmail))
                {
                    _logger.LogWarning("Invalid recipient email: {Email}", toEmail);
                    return;
                }

                var fromEmail = _config["EmailSettings:FromEmail"]
                                ?? throw new InvalidOperationException("Missing FromEmail config.");
                if (!IsValidEmail(fromEmail))
                {
                    _logger.LogWarning("Invalid sender email: {Email}", fromEmail);
                    return;
                }

                var smtpHost = _config["EmailSettings:SmtpHost"]
                               ?? throw new InvalidOperationException("Missing SmtpHost config.");
                var smtpPort = int.Parse(_config["EmailSettings:SmtpPort"] ?? "587");
                var smtpUser = _config["EmailSettings:SmtpUser"]
                               ?? throw new InvalidOperationException("Missing SmtpUser config.");
                var smtpPass = _config["EmailSettings:SmtpPass"]
                               ?? throw new InvalidOperationException("Missing SmtpPass config.");
                var frontendUrl = _config["AppSettings:FrontendUrl"]
                                  ?? throw new InvalidOperationException("Missing FrontendUrl config.");

                var resetLink = $"{frontendUrl}/reset-password?token={resetToken}";
                var subject = "Reset your StarAPI password";
                var body = $@"
                    <h3>Password Reset Request</h3>
                    <p>You requested to reset your password for StarAPI.</p>
                    <p>Click the link below to set a new password:</p>
                    <a href='{resetLink}'>{resetLink}</a>
                    <p>If you didn't request this, please ignore this email.</p>
                    <p>This link will expire in 1 hour.</p>";

                using var message = new MailMessage
                {
                    From = new MailAddress(fromEmail, "StarAPI Support"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                message.To.Add(toEmail);

                using var smtpClient = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    EnableSsl = true
                };

                await smtpClient.SendMailAsync(message);
                _logger.LogInformation("Password reset email sent to {Email}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset email to {Email}", toEmail);
            }
        }

        public Task SendEmailAsync(string to, string subject, string body)
        {
            _logger.LogInformation("[EmailService] Send email to {Email} subject {Subject}", to, subject);
            return Task.CompletedTask;
        }
    }
}