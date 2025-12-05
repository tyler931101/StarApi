using System.Threading.Tasks;

namespace StarApi.Services
{
    public interface IEmailService
    {
        Task SendVerificationEmailAsync(string email, string verificationToken);
        Task SendPasswordResetEmailAsync(string email, string resetToken);
    }
}