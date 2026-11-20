using DoctorEngineApp.Localization;
using Microsoft.AspNetCore.Identity;

namespace DoctorEngineApp.Helpers
{
    /// <summary>ASP.NET Identity xəta mesajları — azərbaycanca yazılır, L.T ilə istifadəçinin dilinə (az/en/ru/tr) çevrilir.</summary>
    public class AzIdentityErrorDescriber : IdentityErrorDescriber
    {
        private static IdentityError E(string code, string text, params object?[] args) => new() { Code = code, Description = L.T(text, args) };

        public override IdentityError DefaultError() => E(nameof(DefaultError), "Naməlum xəta baş verdi.");
        public override IdentityError ConcurrencyFailure() => E(nameof(ConcurrencyFailure), "Məlumat başqa yerdən dəyişdirilib. Yenidən cəhd edin.");
        public override IdentityError PasswordMismatch() => E(nameof(PasswordMismatch), "Şifrə yanlışdır.");
        public override IdentityError InvalidToken() => E(nameof(InvalidToken), "Link etibarsızdır və ya vaxtı bitib. Yenisini tələb edin.");
        public override IdentityError LoginAlreadyAssociated() => E(nameof(LoginAlreadyAssociated), "Bu giriş artıq başqa hesaba bağlıdır.");
        public override IdentityError InvalidUserName(string? userName) => E(nameof(InvalidUserName), "İstifadəçi adı \"{0}\" yanlışdır. Yalnız hərf və rəqəm istifadə edin.", userName);
        public override IdentityError InvalidEmail(string? email) => E(nameof(InvalidEmail), "Email \"{0}\" düzgün deyil.", email);
        public override IdentityError DuplicateUserName(string userName) => E(nameof(DuplicateUserName), "\"{0}\" istifadəçi adı artıq tutulub.", userName);
        public override IdentityError DuplicateEmail(string email) => E(nameof(DuplicateEmail), "\"{0}\" ünvanı ilə artıq qeydiyyat var.", email);
        public override IdentityError InvalidRoleName(string? role) => E(nameof(InvalidRoleName), "Rol adı \"{0}\" yanlışdır.", role);
        public override IdentityError DuplicateRoleName(string role) => E(nameof(DuplicateRoleName), "\"{0}\" rolu artıq mövcuddur.", role);
        public override IdentityError UserAlreadyHasPassword() => E(nameof(UserAlreadyHasPassword), "İstifadəçinin artıq şifrəsi var.");
        public override IdentityError UserLockoutNotEnabled() => E(nameof(UserLockoutNotEnabled), "Bu istifadəçi üçün bloklama aktiv deyil.");
        public override IdentityError UserAlreadyInRole(string role) => E(nameof(UserAlreadyInRole), "İstifadəçi artıq \"{0}\" rolundadır.", role);
        public override IdentityError UserNotInRole(string role) => E(nameof(UserNotInRole), "İstifadəçi \"{0}\" rolunda deyil.", role);
        public override IdentityError PasswordTooShort(int length) => E(nameof(PasswordTooShort), "Şifrə minimum {0} simvol olmalıdır.", length);
        public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => E(nameof(PasswordRequiresUniqueChars), "Şifrədə ən azı {0} fərqli simvol olmalıdır.", uniqueChars);
        public override IdentityError PasswordRequiresNonAlphanumeric() => E(nameof(PasswordRequiresNonAlphanumeric), "Şifrədə ən azı bir xüsusi simvol olmalıdır (məs: ! @ # $).");
        public override IdentityError PasswordRequiresDigit() => E(nameof(PasswordRequiresDigit), "Şifrədə ən azı bir rəqəm olmalıdır (0–9).");
        public override IdentityError PasswordRequiresLower() => E(nameof(PasswordRequiresLower), "Şifrədə ən azı bir kiçik hərf olmalıdır (a–z).");
        public override IdentityError PasswordRequiresUpper() => E(nameof(PasswordRequiresUpper), "Şifrədə ən azı bir böyük hərf olmalıdır (A–Z).");
    }
}
